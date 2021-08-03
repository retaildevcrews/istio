use log::{debug, warn};
use proxy_wasm::{
    traits::*,
    types::*,
};
use serde::Deserialize;
use std::{cell::RefCell, collections::HashMap, time::Duration};

const CACHE_KEY: &str = "burst_metrics";
const USER_AGENT: &str = "user-agent";
const INITIALIZATION_TICK: Duration = Duration::from_secs(2);

#[derive(Deserialize, Debug)]
#[serde(default)]
struct FilterConfig {
    /// The Envoy cluster name
    service_cluster: String,

    /// The path to call on the HTTP service providing headers.
    service_path: String,

    /// The authority to set when calling the HTTP service providing headers.
    service_authority: String,

    /// user agent
    user_agent: String,

    /// Cache duration in seconds
    cache_seconds: u64,

    /// Namespace of this app
    namespace: String,

    /// Name of this deployment
    deployment: String
}

// the plug-in will fail if no config
impl Default for FilterConfig {
    fn default() -> Self {
        FilterConfig {
            service_cluster: "".to_owned(),
            service_path: "".to_owned(),
            service_authority: "".to_owned(),
            user_agent: "".to_owned(),
            cache_seconds: 60 * 60 * 24,
            namespace: "".to_owned(),
            deployment: "".to_owned()
        }
    }
}

thread_local! {
    static CONFIGS: RefCell<HashMap<u32, FilterConfig>> = RefCell::new(HashMap::new())
}

#[no_mangle]
pub fn _start() {
    proxy_wasm::set_log_level(LogLevel::Trace);

    // load config into root context
    proxy_wasm::set_root_context(|context_id| -> Box<dyn RootContext> {
        CONFIGS.with(|configs| {
            configs
                .borrow_mut()
                .insert(context_id, FilterConfig::default());
        });

        Box::new(RootHandler { context_id, config: FilterConfig::default() })
    });

    // set http context for proxy
    proxy_wasm::set_http_context(|_context_id, _root_context_id| -> Box<dyn HttpContext> {
        Box::new(RequestContext { add_header: false })
    })
}

struct RootHandler {
    context_id: u32,
    config: FilterConfig,
}

impl RootContext for RootHandler {
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

                    let agent = self.config.user_agent.clone();
                    let _ok = self.set_shared_data(USER_AGENT, Some(&agent.as_bytes()), None).is_ok();
                }
                Err(e) => {
                    warn!("failed to parse configuration: {:?}", e);

                    return false;
                }
            }
        }

        // Configure an initialization tick
        self.set_tick_period(INITIALIZATION_TICK);

        // configure the cache
        self.set_shared_data(CACHE_KEY, None, None).is_ok()
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
        )
        .map_err(|e| {
            // Reset to an initialization tick for a quick retry.
            self.set_tick_period(INITIALIZATION_TICK);

            warn!("failed calling header providing service: {:?}", e)
        }).is_ok();
    }
}

impl Context for RootHandler {
    fn on_http_call_response(
        &mut self,
        _token_id: u32,
        _num_headers: usize,
        body_size: usize,
        _num_trailers: usize,
    ) {
        // Gather the response body of metrics server call
        let body = match self.get_http_call_response_body(0, body_size) {
            Some(body) => body,
            None => {
                warn!("header providing service returned empty body");
                return;
            }
        };

        // Store the body in the shared cache
        match self.set_shared_data(CACHE_KEY, Some(&body), None) {
            Ok(()) => debug!(
                "refreshed header cache with: {}",
                String::from_utf8(body.clone()).unwrap()
            ),

            Err(e) => {
                warn!("failed storing header cache: {:?}", e);

                // Reset to an initialisation tick for a quick retry.
                self.set_tick_period(INITIALIZATION_TICK)
            }
        }
    }
}

struct RequestContext {
    add_header: bool,
}

impl HttpContext for RequestContext {

    // check headers for user-agent match
    fn on_http_request_headers(&mut self, _: usize) -> Action {
        match self.get_shared_data(USER_AGENT) {
            (Some(agent), _) => {
                if self.get_http_request_header(USER_AGENT).unwrap_or_default() == String::from_utf8(agent.clone()).unwrap_or_default() {
                    self.add_header = true;
                }
            }
            (None, _) => {}
        }


        Action::Continue
    }
    
    fn on_http_response_headers(&mut self, _num_headers: usize) -> Action {

        if self.add_header {
            // get the header from shared data
            match self.get_shared_data(CACHE_KEY) {
                (Some(cache), _) => {
                    debug!("using existing header cache: {}", String::from_utf8(cache.clone()).unwrap());

                    let mystr = String::from_utf8(cache.clone()).unwrap();
                    self.set_http_response_header("X-Load-Feedback",Some(&mystr));
                }
                (None, _) => {
                    warn!("filter not initialized");
                }
            }
        }

        Action::Continue
    }
}

impl Context for RequestContext {}
