# Envoy filter with Rust and WebAssembly

This is a demo accompanying a blogpost about building Envoy filters with Rust and WebAssembly.

<https://github.com/proxy-wasm/proxy-wasm-rust-sdk>

## Getting started

### Install WebAssembly target for rust

   ```bash

   # not necessary with Codespaces

   rustup update
   rustup target add wasm32-unknown-unknown

   ```

### Install [wasme](https://docs.solo.io/web-assembly-hub/latest/reference/cli/)

   ```bash

   # not necessary with Codespaces

   curl -sL https://run.solo.io/wasme/install | sh
   export PATH=$HOME/.wasme/bin:$PATH

   ```

### Clone the repo and use the makefile to build and run the demo:

   ```bash

   make build
   make run

   ```

### Start a new terminal

```bash

curl -i localhost:8080/healthz

```
