// Copyright 2020 Google LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

use log::{debug};
use proxy_wasm::traits::*;
use proxy_wasm::types::*;
use std::time::Duration;

#[no_mangle]
pub fn _start() {
    proxy_wasm::set_log_level(LogLevel::Trace);
    proxy_wasm::set_http_context(|context_id, _| -> Box<dyn HttpContext> {
        Box::new(HttpHeaders { context_id })
    });
}

struct HttpHeaders {
    context_id: u32,
}

impl HttpContext for HttpHeaders {
    fn on_http_request_headers(&mut self, _: usize) -> Action {
        for (name, value) in &self.get_http_request_headers() {
            debug!("#{} -> {}: {}", self.context_id, name, value);
        }

        // match on http path
        match self.get_http_request_header(":path") {
            // burst-check
            Some(path) if path == "/burst-check" => {
                
                self.send_http_response(
                    200,
                    vec![("X-Load-Feedback", "service: ngsa-memory, current-load: 27, target-load: 60, max-load: 85")],
                    Some(b"Pass\n"),
                );
                Action::Pause
            },

            // proxy test
            Some(path) if path == "/test" => {
                // dispatch the web servie call
                // this has to be registered in envoy-bootstrap.yml
                let dis = self.dispatch_http_call(
                    "burst",
                    vec![
                        (":method", "GET"),
                        (":path", "/burst-check"),
                        (":authority", "localhost"),
                    ],
                    None,
                    vec![],
                    Duration::from_secs(5));

                // return 500 if dispatch fails
                if dis.is_err() {
                    self.send_http_response(
                        500,
                        vec![],
                        Some(b"dispatch failed"),
                    );
                }

                Action::Pause
            }
            // ignore
            _ => Action::Continue,
        }
    }

    // not needed - debugging only
    fn on_http_response_headers(&mut self, _: usize) -> Action {
        for (name, value) in &self.get_http_response_headers() {
            debug!("#{} <- {}: {}", self.context_id, name, value);
        }
        Action::Continue
    }

    // not needed - debugging only
    fn on_log(&mut self) {
        debug!("#{} completed.", self.context_id);
    }
}

impl Context for HttpHeaders {
    // http callback
    fn on_http_call_response(&mut self, _: u32, _: usize, body_size: usize, _: usize) {
        // todo - this is just a sample
        if let Some(body) = self.get_http_call_response_body(0, body_size) {
            if !body.is_empty() {

                // todo - this shouldn't be needed
                self.send_http_response(
                    200,
                    vec![],
                    Some(b"success!\n"),
                );

                // without the send_http, this returns 404
                // the result of the call should be proxied but it's not
                self.resume_http_request();

                return;
            }
        }

        // request failed
        self.send_http_response(
            500,
            vec![],
            Some(b"callback failed\n"),
        );
    }
}
