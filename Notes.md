# Notes

## Content Overview

- ** WCNP Overview --> various components e.g. ngsa, burst? --> 15~30 mins
  - Burst Metrics Service and WASM Filter Intro
- Need of Istio Envoy Filter and Burst Metrics
- How they operate (Diagram)
- Show Deployment configuration
  - Show Codespaces init script which is integral to the cluster creation
  - Show Makefile targets and describe important commands
  - Istio Svc mesh model (sidecar vs no-sidecar)
  - Role of Envoy filter and how they are injected currently
- Explain rust code and filer yaml file
  - Briefly describe the WASM VM configuration
- Demo overall burst metrics service
  - Explain how each of the pieces work: e.g. from curl-ing an endpoint to filter and burst service
  - Prometheus custom metrics: explain milli in custom metrics
- Present challenges

## Challenges

- [ ] Currently we are patching the pod/deployment to inject istio filter (see ./Makefile) with inline json. Inject the filter with a patch file?
  - Requires:
    - Create a proper yaml or json to apply the patch
  - Solution: see https://github.com/retaildevcrews/istio/blob/feature/ingress-filter/deploy/istio-ingress/ingressgateway-patch.yaml
- [ ] Apply the Envoy Filter without any patching using `remote` download wasm file?
  - Requires:
    - Fork the repo and create a new branch
    - In filter.yaml, instead of `local.filename` use `remote`. See examples: https://istio.io/latest/docs/reference/config/networking/envoy-filter 
  - Solution: see https://github.com/retaildevcrews/istio/blob/feature/remote-wasm/spikes/remote-wasm-filter/remote-filter.yaml
- [ ] We are injecting the filter into a istio-proxy sidecar. How can we do the same but at an ingressgateway?
  - Requires:
    - ngsa envoy filter VM config change
    see https://istio.io/latest/docs/reference/config/networking/envoy-filter/#EnvoyFilter-PatchContext
  - Solution: see https://github.com/retaildevcrews/istio/tree/feature/ingress-filter/spikes/istio-ingress-filter
- [ ] For the sidecar, without passing the deployment and namespace value in EnvoyFilter, use downwardAPI to automatically read those values from env
  - Requires:
    - Changes in rust code to read environment variables
    - envoy filter VM config change to allow env variable into filter
    - needs ngsa deployment yaml change for sidecar env modification
  - Solution: see https://github.com/retaildevcrews/istio/tree/spike/envoy-env/spikes/sidecar-env
