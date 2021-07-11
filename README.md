# Istio Filter

> Sample Istio filter with Rust and Web Assembly

## TODO

- For now, you have to create a new Codespace from this branch
  - the kind-rust image has been updated
  - post-install.sh has been updated
- Not working yet
  - LodeRunner
  - Prometheus
  - Grafana

## Run Istio Web Assembly

- Create the kind cluster

   ```bash

   make create

   ```

### Set env vars

```bash

# load new env vars
# exit and start new shell will also work
source ~/.bashrc

```

### Verify the setup

```bash

# may have to retry a couple of times
make test-all

```

## Links

- Building Envoy filters with Rust and WebAssembly - <https://github.com/proxy-wasm/proxy-wasm-rust-sdk>
- OIDC Sample <https://docs.eupraxia.io/docs/how-to-guides/deploy-rust-based-envoy-filter/#building-of-the-http-filter>
