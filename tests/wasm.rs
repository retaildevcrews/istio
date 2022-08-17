#[cfg(test)]

#[path="../src/lib.rs"]
mod burst_header;

#[path="../src/config.rs"]
mod config;

use wasm_bindgen_test::*;

// upstream bug in proxy_wasm::*, currently errors out
#[wasm_bindgen_test]
fn it_works() {
    assert_eq!(2 + 2, 4);
}
