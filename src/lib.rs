use log::{debug, error, info, warn};
use proxy_wasm::hostcalls::get_property;
use proxy_wasm::{
    traits::{Context, HttpContext, RootContext},
    types::{Action, ContextType, LogLevel},
};
use crate::config::{FilterConfig, FilterConfigError};
use protobuf::well_known_types::Struct;
use protobuf::Message;
use serde_json::{Map, Value};
use std::convert::TryFrom;
use std::error::Error;
use std::time::Duration;

pub mod config;

const USER_AGENT: &str = "user-agent";
const HEADER_NAME: &str = "X-Load-Feedback";
macro_rules! KEY_FORMAT {
    () => {
        "{}/{}"
    };
}
// root handler holds config
struct RootHandler {
    config: Option<FilterConfig>,
    /// Cluster svc to burst header map
    cluster_map: Option<Map<String, Value>>
}

impl RootHandler {
    pub(crate) fn new() -> Self {
        Self {
            config: None,
            cluster_map: Some(Map::new()),
        }
    }
}

// each request gets burst_header and user_agent from root
pub struct RequestContext {
    add_header: bool,
    user_agent: String,
    host_svc_header: String,
}

#[no_mangle]
pub fn _start() {
    proxy_wasm::set_log_level(LogLevel::Warn);

    info!("starting bursting wasm");

    // create root context and load config
    proxy_wasm::set_root_context(|_context_id| -> Box<dyn RootContext> {
        Box::new(RootHandler::new())
    });
}

// Root Handler implementation

impl RootHandler {
    fn parse_configuration(&mut self) -> Result<(), Box<dyn Error>> {
        let config = self
            .get_configuration()
            .ok_or(FilterConfigError::Missing)?;
        let config = Struct::parse_from_bytes(config.as_slice())?;
        let config = FilterConfig::try_from(config)?;

        self.config = Some(config);
        Ok(())
    }
}

// Root Context implementation

impl Context for RootHandler {
    // async http request callback from metrics service
    fn on_http_call_response(
        &mut self,
        _token_id: u32,
        _num_headers: usize,
        body_size: usize,
        _num_trailers: usize,
    ) {
        let config = self.config.as_ref().unwrap();
        // get the response body of metrics server call
        match self.get_http_call_response_body(0, body_size) {
            Some(body) => {
                let result = String::from_utf8(body).ok().and_then(|v| {
                    let parsed: Option<Value> = serde_json::from_str(v.as_str()).ok();
                    parsed
                });
                if let Some(v) = result {
                    if let Some(obj) = v.as_object() {
                        self.cluster_map = Some(obj.clone());
                    } else {
                        warn!(
                            "Invalid JSON body from service cluster ({}/{})",
                            config.service_authority, config.service_cluster
                        );
                    }
                }
            }
            None => {
                // log an error
                error!("header providing service returned empty body");
            }
        };
    }
}

impl RootContext for RootHandler {
    // create http context for new requests
    fn create_http_context(&self, _context_id: u32) -> Option<Box<dyn HttpContext>> {
        let config = self.config.as_ref().unwrap();
        let host_svc_header = if config.burst_header.is_empty() {
            let host_addr = match get_property(vec![
                "cluster_metadata",
                "filter_metadata",
                "istio",
                "services",
                "0",
                "host",
            ]) {
                Ok(data) => data.and_then(|d| String::from_utf8(d).ok()),
                Err(_) => None,
            }
            .unwrap_or_default();

            let mut key = String::new();

            if host_addr.is_empty()
            {
                warn!("host_addr is empty.");
            }
            else{
                // host_addr format: service.namespace.svc.cluster.local
                // cluster.local portion can be different based on which
                // cluster the service is in
                // But for now we're interesting about service and namespace only
                let tokens: Vec<&str> = host_addr.split(".").collect();

                // we check to make sure tokens has at least 2 items.
                if tokens.len() >= 2
                {
                    key = format!(KEY_FORMAT!(), tokens[1], tokens[0]);
                }
                else
                {
                    warn!("host_addr does not match the expected format: '{}'", host_addr )
                }
            }

            match self.cluster_map.as_ref().unwrap().get(key.as_str()) {
                Some(val) => val.as_str().unwrap(),
                _ => "",
            }
        } else {
            config.burst_header.as_str()
        };

        info!("Host header: {}", host_svc_header);
        Some(Box::new(RequestContext {
            add_header: false,
            // copy the values from root config to request
            user_agent: config.user_agent.to_string().clone(),
            host_svc_header: host_svc_header.to_string(),
        }))
    }

    // required for create_http_context to work
    fn get_type(&self) -> Option<ContextType> {
        Some(ContextType::HttpContext)
    }

    // read the config and store in self.config
    fn on_configure(&mut self, _config_size: usize) -> bool {
        // set a short duration to cause the timer to fire quickly
        self.set_tick_period(Duration::from_secs(1));

        match self.parse_configuration() {
            Ok(_) => true,
            Err(e) => {
                // fail on invalid config
                error!("failed to parse configuration: {:?}", e);
                false
            }
        }
    }

    // refresh the cache on a timer
    fn on_tick(&mut self) {
        if self.config.is_none() {
            return;
        }

        let config = self.config.as_ref().unwrap();
        self.set_tick_period(config.cache_seconds);

        // dispatch an async HTTP call to the configured cluster
        // response is handled in Context::on_http_call_response
        let res = self.dispatch_http_call(
            &config.service_cluster,
            vec![
                (":method", "GET"),
                (":path", &format!("{}", config.service_path)),
                (":authority", &config.service_authority),
            ],
            None,
            vec![],
            Duration::from_secs(5),
        );

        match res {
            Err(e) => {
                warn!("metrics service request failed: {:?}", e);

                // retry quickly
                self.set_tick_period(Duration::from_secs(2));
            }
            Ok(_) => {}
        }
    }
}

// http request implemenetation

// nothing implemented
impl Context for RequestContext {}

impl HttpContext for RequestContext {
    // check headers for user-agent match and store in self
    fn on_http_request_headers(&mut self, _: usize) -> Action {
        self.add_header = true;
        if !self.user_agent.as_str().is_empty() &&
            !self
            .get_http_request_header(USER_AGENT)
            .unwrap_or_default()
            .starts_with(self.user_agent.as_str())
        {
            self.add_header = false;
        } else {
            debug!("Not adding header. User agent mismatch");
        }
        Action::Continue
    }

    // add the header if user-agent matched
    fn on_http_response_headers(&mut self, _num_headers: usize) -> Action {
        if self.add_header && !self.host_svc_header.trim().is_empty() {
            self.set_http_response_header(HEADER_NAME, Some(&self.host_svc_header));
        } else {
            debug!("Not adding header. Not host svc header received from Burst service");
        }
        Action::Continue
    }
}
