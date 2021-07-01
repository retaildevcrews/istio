# Istio Filter

> Sample Istio filter with Rust and Web Assembly

## Runnning hello

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

# sample endpoint
curl -i localhost:8080/burst-check

# proxied endpoint
curl -i localhost:8080/test

```

## Links

- Building Envoy filters with Rust and WebAssembly - <https://github.com/proxy-wasm/proxy-wasm-rust-sdk>
- OIDC Sample <https://docs.eupraxia.io/docs/how-to-guides/deploy-rust-based-envoy-filter/#building-of-the-http-filter>
