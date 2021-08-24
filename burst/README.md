# Burst Metrics Service

Burst metrics service is responsible for reading HPA metrics from a Kubernetes(k8s) API and return a formatted burst metrics header. It is implemented as a REST service.

Currently it will capture current pod count and target pod count from an HPA.

## Deployment/usage

TODO: add recently pushed `make` usage

## API Endpoints

### `/burstmetrics/{namespace}/{HPA}`

This is the only endpoint exposed by this service.
This endpoint will check the k8s `{namespace}` for the specified `{HPA}`.

If the `{HPA}` is found and is accessible, it will return the metrics below in a formatted fashion:

- service=namespace/HPA-name
- current-load=current pod count
- max-load=target hpa pod count
- target-load=80% of max-load <!-- WIP:Configure the percentage -->

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

In-cluster configuration (as well as default kube-config) for this service requires permissions to call the `autoscaling` API. Shown below is an example of a `ClusterRole` with `autoscaling` API groups added.

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  namespace: default
  name: burst-role
rules:
- apiGroups: [""]
  resources: ["*"]
  verbs:
  - list # list and get. Only add what we need
  - get
- apiGroups:
  - autoscaling # required
  - "*"
  resources:
  - "*"
  verbs:
  - list
  - get
```

For a complete example see [burst.yaml](./deploy/burst/burst.yaml).

## Implementation details

A fixed-period timer is implemented in a ASP.NET service (as singleton, see `ConfigureServices` function in [Startup.cs](./src/Core/Startup.cs)), where it gets HPA information from the K8s API. It uses the in-cluster or default configuration to access the k8s API.

- The in-cluster configuration enables burst metrics service to be run as a pod and still access the k8s API from within.

- Default configuration is the same which is used by `kubectl` to get the context and API.

The timer calls the K8s api periodically and saves <!--triage to implement caching--> all HPA data (which it has access to) and simply serves them when the endpoints are called with specific namespace and HPA name.

> Service source code [src/K8sApi/K8sHPAMetricsService.cs](./src/K8sApi/K8sHPAMetricsService.cs)
