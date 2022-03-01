# Notes

## Content Overview

0. Introductions / Logistics / Verify People have access to the org and codespaces (have someone assist getting access)
1. [Joseph, 15 mins] WCNP Overview --> various components? --> 15 mins~20min

2. [Joaquin/AK 45~60 min] CSE Labs - Kubernetes in Codespaces Lab
  
   1. Start codespaces from repo https://github.com/cse-labs/kubernetes-in-codespaces
   2. Build and deploy k3d cluster
   3. Intro to k9s
   4. Jumpbox
   5. Port forwarding in codespaces
   6. NGSA App Deploy and review
   7. Prometheus and Grafana Dashboards
   8. Run Load Test and see the load in Grafana
   9. Introduction to FluentBit logs
   10. How Codespaces is Built

>[15 min break]

3. [Kushal] Istio Rust WebAssembly Hands-on Labs (CSE Labs)

  1. [K/S 3~5min] Ask them to start codespaces, from the repo link: https://github.com/retaildevcrews/istio/
  2. [K/S~2min, optional?] Need of Istio Envoy Filter and Burst Metrics, how they operate (Diagram)
    1. Istio Svc mesh model (sidecar vs no-sidecar)??
       [Q?] Should we delve into gateway model and explain?
    2. Role of Envoy filter and how they are injected currently (not planning to go into the rust code)
    3. Explain the current working item: using service dns name to call burstmetrics service.
       Currently we are using deployment name (assuming its same as HPA name) and calling BMS with that info
  3. [K/S ~5min] Show deployment configuration, show `clusteradm/Makefile` and `./Makefile`
    1. Show Makefile targets and describe important commands
    2. After deployment is completed, these pods are created
    3. Before the deployment, there are no burst header when we curl/http ngsa endpoint
    4. After deployment, we have burst metrics (describe a little?)
    5. Internally burstmetrics service is called by wasm filter: `curl http://localhost:30081/burstmetrics/default/ngsa`, describe `default` and `ngsa`
    6. Explain the current working item: using service dns name to call burstmetrics service.
       Currently we are using deployment name (assuming its same as HPA name) and calling BMS with that info
  4. [K/S ~8min] Now lets do a load test (describe how we are running the load)
    1. Run the load test
    2. Create a split terminal and do a `watch -n 1 k get pod`, and another one with hpa, show them live scaling
    3. Also show them burst metrics with `make check`
    4. Scale down
  5. [K/S ~10min, optional, not too focused on wasm filter] Now lets show custom metrics from prometheus
    1. Deploy prometheus metrics server
    2. Explain the custom metrics in yaml file which prometheus is getting from NGSA /metrics endpoint
    3. Show scale and burst metrics with another load using custom metrics
  6. [K/S~1hr, optional] Present challenges
    1. Describe the challenges
    2. Give them pointers and reading materials or sample materials

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
