# Istio Filter

> Sample Istio filter with Rust and Web Assembly

## Issues with Web Assembly

- Currently, `TcpStream` is not supported in Rust Web Assembly
  - no external TCP calls are supported
  - we can't use Rust Web Assembly for this project
- There is a simple sample that works

### Runnning hello

> This is not a Web Assembly

This is a simple Rust hello app that uses curl to retrieve from a website

- Run by pressing `F5`
- Run from terminal with `cargo run`

### Run Istio Web Assembly

   ```bash

   cd proxy
   make run

   ```

### Start a new terminal

```bash

# This works
curl -i localhost:8080/burst-check

# This doesn't
curl -i localhost:8080/this-will-fail

```

## Links

- Building Envoy filters with Rust and WebAssembly - <https://github.com/proxy-wasm/proxy-wasm-rust-sdk>
