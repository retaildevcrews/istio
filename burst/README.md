# Burst Metrics Service

Burst metrics service is responsible for reading HPA metrics from a Kubernetes(k8s) API and return a formatted burst metrics header. It is implemented as a REST service.

The service captures current pod count and target pod count from an HPA.

## Deployment/usage

Deploying in a local cluster is easy and straightforward.

`make deploy` to build and deploy burst service.

Couple of caveats to note:

- Burst service is a NodePort type service and uses 30081 port to expose the service externally (e.g. k3d, kind).
- If used in a local cluster the port needs to be opened/exposed when configuring (k3d, kind, minicube etc) the cluster.
- For a standard service, delete `nodePort: 30081` and `type: NodePort` lines in `burst.yaml`

## API Endpoints

### `/burstmetrics/{namespace}/{HPA}`

This is the only endpoint exposed by this service.
This endpoint will check the k8s `{namespace}` for the specified `{HPA}`.

If the `{HPA}` is found and is accessible, it will return the metrics below in a formatted fashion:

- service=namespace/HPA-name
- current-load=current pod count
- max-load=target hpa pod count
- target-load=80% of max-load

If it can't find the `{HPA}` or is inaccessible, or any exception occurs during the API call, it will simply return `HTTP 204 No Content`.

#### Example 1: 200 OK

```http
HTTP/1.1 200 OK
content-type: text/plain; charset=utf-8
date: Mon, 23 Aug 2021 16:29:51 GMT
server: istio-envoy
transfer-encoding: chunked
x-envoy-upstream-service-time: 1

service=default/ngsa, current-load=2, target-load=5, max-load=6
```

#### Example 2: 204 No Content

```http
HTTP/1.1 204 No Content
date: Mon, 23 Aug 2021 16:30:26 GMT
server: istio-envoy
x-envoy-upstream-service-time: 16
```

### RBAC requirements

In-cluster configuration (as well as default kube-config) for this service requires additional permission to call the `autoscaling` API.

For a complete RBAC example, see [burst.yaml](./../deploy/burst/burst.yaml).

### HPA Requirements

Autoscaling is an approach to automatically scale up or down workloads based on the resource usage.
The Horizontal Pod Autoscaler (HPA) controller retrieves metrics from a series of APIs and adapters (see [this](https://kubernetes.io/docs/tasks/run-application/horizontal-pod-autoscale/#support-for-metrics-apis)).

These metrics can be provided by [K8s metrics server](https://github.com/kubernetes-sigs/metrics-server) or other custom metrics adapter (e.g. [prometheus adapter](https://github.com/kubernetes-sigs/prometheus-adapter)).

It is assumed that the HPA is properly setup with at least one metrics server.

## Implementation details

A fixed-period timer is implemented in a ASP.NET service (as singleton, see `ConfigureServices` function in [Startup.cs](./src/Core/Startup.cs)), where it gets HPA information from the K8s API. It uses the in-cluster or default configuration to access the k8s API.

- The in-cluster configuration enables burst metrics service to run inside a pod and still access the k8s API from within.

- Default configuration is the same as the configuration used by `kubectl` to get the context and k8s API.

The timer calls K8s api periodically and saves all HPA data (which it has access to) and simply serves them when the burst metrics API endpoint is called with specific namespace and HPA name.

> Service source code [src/K8sApi/K8sHPAMetricsService.cs](./src/K8sApi/K8sHPAMetricsService.cs)
