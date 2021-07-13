# Istio Filter

> Sample Istio filter with Rust and Web Assembly

## TODO

- Not working yet
  - Prometheus
  - Grafana
  - need to figure out what has to be deployed vs make create
    - make clean
    - make deploy

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
make check

```

### Add the wasm filter

> this doesn't have to be a separate step

- the above deployed everything but the WebAssembly is not enabled, so the `x-load-feedback` header isn't added

```bash

# enable the WebAssembly
kubectl apply -f deploy/filter.yaml

# verify the new header (may have to retry a couple of times)
make check

# remove the assembly
kubectl delete -f deploy/filter.yaml

# verify the header isn't added (may have to retry a couple of times)
make check

```

## Links

- Building Envoy filters with Rust and WebAssembly - <https://github.com/proxy-wasm/proxy-wasm-rust-sdk>
- OIDC Sample <https://docs.eupraxia.io/docs/how-to-guides/deploy-rust-based-envoy-filter/#building-of-the-http-filter>
