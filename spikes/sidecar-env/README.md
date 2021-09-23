# Istio sidecar proxy spike

Using envrionment varibles we are trying to avoid "manually" specifying
deployment and namespace in EnvoyFiler.

Also we are trying to automate the istio sidecar configuration without patching
(or using annotation)

## Usage

```bash
# First remove old ngsa deployment and filter
# CWD: $REPOT_ROOT/spikes/sidecar-env/ 
make clean-old

# Now deploy spike deployment
# It will install proper rust target wasm32-wasi
# Wasi is required for std::env, and it also is being standardized in WebAssembly CG
# See: https://wasi.dev/
make deploy

# Check if deployed properly
kubectl get po -l app=ngsa # Check for Ready 2/2

# Check burst header
make check

```

## Impl details

We have removed namespace and deployment in EnvoyFilter
(see [filter.yaml](./yamls/filter.yaml)) and rather we depend on two environment
variables
