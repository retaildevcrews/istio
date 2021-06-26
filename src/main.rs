extern crate curl;

use std::str;
use curl::easy::Easy;

fn main() {
    println!("{}", get_header());
}

fn get_header() -> String {
    let mut headers = Vec::new();
    let mut handle = Easy::new();

    handle.url("https://www.rust-lang.org/").unwrap();
    
    {
        let mut transfer = handle.transfer();
        transfer.header_function(|header| {
             let h = str::from_utf8(header).unwrap();
             if h.starts_with("X-Cache:") {
                 headers.push(String::from(h[8..].trim()));
             }
            true
        }).unwrap();
        transfer.perform().unwrap();
    }
    
    let mut ret = String::new();

    if headers.len() > 0 {
        ret = headers[0].to_string();
    }

    return ret;
}
