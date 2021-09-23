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
variables `NGSA_NAMESPACE` and `NGSA_HPA_NAME`.

Instead of patching the deployment, we added standard container yaml for istio-sidecar
and let istio figure out the configuration.

Since we named the 2nd container `istio-proxy` (see [ngsa-mem-sidecar.yaml](./yaml/ngsa-mem-sidecar.yaml)), istio will
use the spec as configuration for istio-sidecar.

See [istio sidecar injector documentation](https://istio.io/latest/docs/setup/additional-setup/sidecar-injection/) for more info.

Also we read environemnt variables from the wasm and used `wasm32-wasi` target to use rust's `std::env`.

## Impl variation

Instead of setting `NGSA_NAMESPACE` and `NGSA_HPA_NAME` we can use istio sidecars
existing environment which provies the namespace and deployment name.

We can use `POD_NAMESPACE` in lieu of `NGSA_NAMESPACE` and `ISTIO_META_WORKLOAD_NAME`
instead of `NGSA_HPA_NAME`.

There is another variable called `ISTIO_META_OWNER` (example value kubernetes://apis/apps/v1/namespaces/__default__/deployments/__ngsa__)
which provides both namespace and deployment name, but has to be regex-ed in wasm.

Pros of using istio specific vars:

- No need for other variables
- As long as using specific istio version, it can be guaranteed that these vars will exist
- If used `ISTIO_META_OWNER` envoy filter will depend only on one variable
  - Hence only one variable needs to be exposed to the VM

Cons:

- ISTIO_META* variables are not documented and is subject to change
- Hard dependency on Istio
  - Since the envoy filter mostly will live in istio-sidecar, might not be a disadvantage

## Discussion

### Pros

- Applicable for any deployment, no "manual" configmap value needed
- Uses environment variables
- Using standard k8s container spec to configure sidecar instead of annotation
- Can embed wasm image directly into custom sidecar image
- Since, we're depending on ENV vars, we can change the variables in deployment yaml to set to different values
  - In addition, we can also set the env vars in EnvoyFilter and skip deployment. See: [envoy wasm doc](https://www.envoyproxy.io/docs/envoy/latest/api-v3/extensions/wasm/v3/wasm.proto#extensions-wasm-v3-environmentvariables)

### Cons

- Expose specific env variable name to wasm VM
- Depends on convention: label `app` is the same as the deployment name
  - Additionally, our deployment depends on another convention: deployment name is same as corresponding hpa name.
- Environment variable requires wasm32-wasi target.
  - Not sure if that is a disadvantage, coz according to [wasi page](https://wasi.dev/) its being standardized and is more portable
