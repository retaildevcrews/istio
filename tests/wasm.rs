#[cfg(test)]

#[path="../src/lib.rs"]
mod burst_header;

use burst_header::FilterConfig ;
use wasm_bindgen_test::*;

#[wasm_bindgen_test]
fn it_works() {
    assert_eq!(2 + 2, 4);
}
