use log::{error, info, warn};
use proxy_wasm::hostcalls::get_property;
use proxy_wasm::{
    traits::{Context, HttpContext, RootContext},
    types::{Action, ContextType, LogLevel},
};
use std::convert::TryFrom;
use std::error::Error;
use std::rc::Rc;

use crate::config::{Configuration, ConfigurationError};
use protobuf::well_known_types::Struct;
use protobuf::Message;
use serde_json::{Map, Value};
use std::time::Duration;

pub mod config;

const USER_AGENT: &str = "user-agent";
const HEADER_NAME: &str = "X-Load-Feedback";

// root handler holds config
struct RootHandler {
    config: Option<Configuration>,
    cluster_map: Option<Map<String, Value>>,
    header_value: Option<Rc<String>>,
}

impl RootHandler {
    pub(crate) fn new() -> Self {
        Self {
            config: None,
            cluster_map: None,
            header_value: None,
        }
    }
}

// each request gets burst_header and user_agent from root
pub struct RequestContext {
    add_header: bool,
    burst_header: Option<Rc<String>>,
    user_agent: Option<Rc<String>>,
    cluster_map: Option<Map<String, Value>>,
    is_gw: bool,
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
                // Store the header in self.config
                if config.is_gw {
                    let result = String::from_utf8(body).ok().and_then(|v| {
                        let parsed: Option<Value> = serde_json::from_str(v.as_str()).ok();
                        parsed
                    });
                    if let Some(v) = result {
                        if let Some(obj) = v.as_object() {
                            self.cluster_map = Some(obj.clone());
                        }
                    }
                } else {
                    let result = String::from_utf8(body.clone()).ok();
                    if let Some(v) = result {
                        self.header_value = Some(Rc::new(v));
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
        Some(Box::new(RequestContext {
            add_header: false,
            user_agent: self.config.as_ref().map(|cfg| cfg.user_agent.clone()),
            burst_header: self.header_value.clone(),
            cluster_map: self.cluster_map.clone(),
            is_gw: self.config.as_ref().map(|cfg| cfg.is_gw).unwrap_or(true),
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
                error!("Could not parse configuration: {:?}", e);
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

        self.set_tick_period(config.cache_refresh_seconds);

        // dispatch an async HTTP call to the configured cluster
        // response is handled in Context::on_http_call_response
        let path = if config.is_gw {
            format!("{}", config.service_path)
        } else {
            format!(
                "{}/{}/{}",
                config.service_path, config.namespace, config.deployment
            )
        };
        let res = self.dispatch_http_call(
            &config.service_cluster,
            vec![
                (":method", "GET"),
                (":path", &path),
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

impl RootHandler {
    fn parse_configuration(&mut self) -> Result<(), Box<dyn Error>> {
        let config = self
            .get_configuration()
            .ok_or(ConfigurationError::Missing)?;
        let config = Struct::parse_from_bytes(config.as_slice())?;
        let config = Configuration::try_from(config)?;

        self.config = Some(config);
        Ok(())
    }
}

impl HttpContext for RequestContext {
    // check headers for user-agent match and store in self
    fn on_http_request_headers(&mut self, _: usize) -> Action {
        self.get_http_request_header(USER_AGENT).map(|h| {
            if let Some(cfg) = self.user_agent.clone() {
                if h.starts_with(cfg.as_str()) {
                    self.add_header = true;
                }
            }
        });

        Action::Continue
    }

    // add the header if user-agent matched
    fn on_http_response_headers(&mut self, _num_headers: usize) -> Action {
        if self.add_header {
            if self.is_gw {
                let header_value = match get_property(vec![
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
                .and_then(|name| {
                    self.cluster_map
                        .as_ref()
                        .and_then(|map| map.get(name.as_str()))
                })
                .and_then(|v| v.as_str());

                self.set_http_response_header(HEADER_NAME, header_value)
            } else {
                let header_value = self.burst_header.as_ref().map(|v| v.as_str());
                self.set_http_response_header(HEADER_NAME, header_value);
            };
        }
        Action::Continue
    }
}
