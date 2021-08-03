use log::*;
use proxy_wasm::traits::*;
use proxy_wasm::types::*;
use serde::*;
use std::time::*;

const USER_AGENT: &str = "user-agent";
const INITIALIZATION_TICK: Duration = Duration::from_secs(2);

// root handler holds config map
struct RootHandler {
    config: FilterConfig,
}

// each request gets burst_header and user_agent from root
struct RequestContext {
    add_header: bool,
    burst_header: String,
    user_agent: String,
}

// config map structure
#[derive(Deserialize, Debug)]
#[serde(default)]
struct FilterConfig {
    /// current burst header - gets updated on time
    burst_header: String,

    /// Cache duration in seconds
    cache_seconds: u64,

    /// Name of this deployment
    deployment: String,

    /// Namespace of this app
    namespace: String,

    /// The authority to set when calling the HTTP service providing headers.
    service_authority: String,

    /// The Envoy cluster name
    service_cluster: String,

    /// The path to call on the HTTP service providing headers.
    service_path: String,

    /// user agent
    user_agent: String,
}

// default values for config
impl Default for FilterConfig {
    fn default() -> Self {
        FilterConfig {
            burst_header: "".to_owned(),
            cache_seconds: 60 * 60 * 24,
            deployment: "".to_owned(),
            namespace: "".to_owned(),
            service_authority: "".to_owned(),
            service_cluster: "".to_owned(),
            service_path: "".to_owned(),
            user_agent: "".to_owned(),
        }
    }
}

#[no_mangle]
pub fn _start() {
    proxy_wasm::set_log_level(LogLevel::Warn);

    // load config map into root context
    proxy_wasm::set_root_context(|_context_id| -> Box<dyn RootContext> {
        Box::new(RootHandler { config: FilterConfig::default() })
    });
}

impl Context for RootHandler {
    // async http request callback
    fn on_http_call_response(
        &mut self,
        _token_id: u32,
        _num_headers: usize,
        body_size: usize,
        _num_trailers: usize,
    ) {
        // get the response body of metrics server call
        match self.get_http_call_response_body(0, body_size) {
            Some(body) => {
                // Store the header in self.config
                self.config.burst_header = String::from_utf8(body.clone()).unwrap();
            },
            None => {
                warn!("header providing service returned empty body");
            }
        };
    }
}

impl RootContext for RootHandler {
    // create http context for each request
    fn create_http_context(&self, _context_id: u32) -> Option<Box<dyn HttpContext>> {
        Some(Box::new(RequestContext {
            add_header: false,
            // copy the values from config map
            user_agent: self.config.user_agent.clone(),
            burst_header: self.config.burst_header.clone(),
        }))
    }

    // required!
    fn get_type(&self) -> Option<ContextType> {
        Some(ContextType::HttpContext)
    }

    // read the config map and store in self.config
    fn on_configure(&mut self, _config_size: usize) -> bool {

        if self.config.service_cluster == "" {
            // Check for the mandatory filter configuration
            let configuration: Vec<u8> = match self.get_configuration() {
                Some(c) => c,
                None => {
                    warn!("configuration missing");

                    return false;
                }
            };

            // Parse and store the configuration
            match serde_json::from_slice::<FilterConfig>(configuration.as_ref()) {
                Ok(config) => {
                    self.config = config;
                }
                Err(e) => {
                    warn!("failed to parse configuration: {:?}", e);

                    return false;
                }
            }
        }

        // Configure an initialization tick
        self.set_tick_period(INITIALIZATION_TICK);

        return true;
    }

    // refresh the cache on a timer
    fn on_tick(&mut self) {
        self.set_tick_period(Duration::from_secs(self.config.cache_seconds));

        // Dispatch an async HTTP call to the configured cluster
        let _z = self.dispatch_http_call(
            &self.config.service_cluster,
            vec![
                (":method", "GET"),
                (":path", &format!("{}/{}/{}", self.config.service_path, self.config.namespace, self.config.deployment)),
                (":authority", &self.config.service_authority),
            ],
            None,
            vec![],
            Duration::from_secs(5),
        ).map_err(|e| {
            // Reset to an initialization tick for a quick retry.
            self.set_tick_period(INITIALIZATION_TICK);

            warn!("failed calling header providing service: {:?}", e)
        }).is_ok();
    }
}

impl Context for RequestContext {}

impl HttpContext for RequestContext {

    // check headers for user-agent match
    fn on_http_request_headers(&mut self, _: usize) -> Action {
        if self.get_http_request_header(USER_AGENT).unwrap_or_default() == self.user_agent {
            self.add_header = true;
        }
        Action::Continue
    }
    
    // add the header if user-agent matched
    fn on_http_response_headers(&mut self, _num_headers: usize) -> Action {
        if self.add_header {
            self.set_http_response_header("X-Load-Feedback",Some(&self.burst_header));
        }
        Action::Continue
    }
}
