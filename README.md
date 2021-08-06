# Istio Filter

> Sample Istio filter with Rust and Web Assembly

## Errors

- `cargo test --target wasm32-unknown-unknown` is currently failing
  - upstream bug in proxy_wasm::*

## Run Istio Web Assembly

- Create the kind cluster

   ```bash

   make create

   ```

### Set env vars

> Do not forget this step!

```bash

# load new env vars
# exit and start new shell will also work
source ~/.bashrc

```

### Verify the setup

```bash

# may have to retry a couple of times
make check

```

### Add load

- Start a new terminal window

```bash

make test

```

- Go back to previous terminal

```bash

make check

```

## Request Flow

![Request Flow](images/flow.png)

## Links

- Building Envoy filters with Rust and WebAssembly - <https://github.com/proxy-wasm/proxy-wasm-rust-sdk>
- OIDC Sample <https://docs.eupraxia.io/docs/how-to-guides/deploy-rust-based-envoy-filter/#building-of-the-http-filter>
- Unit testing with `wasm-bindgen-test` - <https://rustwasm.github.io/docs/wasm-bindgen/wasm-bindgen-test/index.html>
