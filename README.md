# Istio Filter

> Sample Istio filter with Rust and Web Assembly

## TODO

- For now, you have to create a new Codespace from this branch
  - the kind-rust image has been updated
  - post-install.sh has been updated

## Run Istio Web Assembly

- Create the kind cluster

   ```bash

   make build

   ```

### Set env vars

```bash

### TODO - automate this

source tcall.sh

# edit cmdemoyml/filter.yml
# edit src/lib.rs
# change the IP and port as needed

```

### Complete the setup

```bash

make finish

# repeat until the pods are ready
### TODO - using wait doesn't work yet
kubectl get po

# verify deployment
# may have to retry a couple of times
make check

```

## Links

- Building Envoy filters with Rust and WebAssembly - <https://github.com/proxy-wasm/proxy-wasm-rust-sdk>
- OIDC Sample <https://docs.eupraxia.io/docs/how-to-guides/deploy-rust-based-envoy-filter/#building-of-the-http-filter>
