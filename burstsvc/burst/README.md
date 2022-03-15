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

### `/burstmetrics/{target-type}s`

This endpoint exposes the three different targets supported by this service.

The supported targets includes:

- Service
- Deployment
- HPA

Note that, target-type endpoints are plural in nature.

Examples:

- For service(s): `/burstmetrics/services`
- For deployment(s): `/burstmetrics/deployments`
- For hpa(s): `/burstmetrics/hpas`

All of these target endpoints return json in below format:

```json

{
  "{namespace-1/target-name-1}" :"formatted burst header for target-name-1",
  "{namespace-1/target-name-2}" :"formatted burst header for target-name-2",
  "{namespace-a/target-name-b}" :"formatted burst header for target-name-b"
}

```

> The target names are either a type of hpa, deployment or service
> Depends on which target is called (e.g. hpas, services etc.)

For the burst header format see [this section](#burstmetricstarget-typesnamespacetarget-name)

#### API Details

- **Method** : `GET`
- **Permissions/Auth** : None
- **Success Response**
  - **Code** : `200 OK`
    > If at least one target is returned.

    **Content Type** : application/json; charset=utf-8

    **Content Format** :

    ```json
    {
      "{ns-1}/{target-name-1}" : "service={ns}/{name}, current-load=<int>, target-load=<int>, max-load=<int>",
      "{ns-2}/{target-name-2}" : "service={ns}/{name}, current-load=<int>, target-load=<int>, max-load=<int>"
    }
    ```

    See [examples](#example-1-200-ok) for detailed API content format.

  - **Code** : `204 No Content`
    > If there are no burst headers for that specific target, but the API call was successful

For any unhandled exception during the API call, it will return `HTTP 500 Internal Server Error`.

### `/burstmetrics/{target-type}s/{namespace}/{target-name}`

This endpoints gets burst header for a specific target of specific target types.

Targets included:

- Service
- Deployment
- HPA

The target types are plural in nature.

Examples:

- For service(s): `/burstmetrics/services/{namespace}/{service-name}`
- For deployment(s): `/burstmetrics/deployments/{namespace}/{deployment-name}`
- For hpa(s): `/burstmetrics/hpas/{namespace}/{hpa-name}`

The burst header follows the below format::

- service={namespace}/{deployment-name}
- current-load=current pod count
- max-load=target hpa pod count
- target-load=80% of max-load

#### API Details

- **Method** : `GET`
- **Permissions/Auth** : None
- **Success Response**
  - **Code** : `200 OK`
    > When the `{hpa}` deployment/HPA is found in `{ns}` namespace

    **Content Type** : text/plain

    **Content Format** :

    `service={ns}/{name}, current-load=<int>, target-load=<int>, max-load=<int>`

    See [examples](#example-1-200-ok) for detailed API content format.
  - **Code** : `204 No Content`
    > When an `{hpa}` deployment/HPA is not found in `{ns}` namespace but the API call itself is successful

For any unhandled exception during the API call, it will return `HTTP 500 Internal Server Error`.

### Examples

Suppose we have two deployments named ngsa-1, ngsa-2.

Associated services and HPA are as follows:

| Deployment Name | Namespace |Service Name | HPA Name   |
|:----------------|:----------|:------------|:-----------|
| ngsa-1          | default   |ngsa-svc-1   | ngsa-hpa-1 |
| ngsa-2          | ngsa      |ngsa-svc-2   | ngsa-hpa-2 |

#### Example 1: 200 OK

> `http localhost:8080/burstmetrics/deployments/default/ngsa-1`

```http

HTTP/1.1 200 OK
content-type: text/plain; charset=utf-8
date: Thu, 03 Mar 2022 11:29:51 GMT
server: istio-envoy
transfer-encoding: chunked
x-envoy-upstream-service-time: 1

service=default/ngsa-1, current-load=1, target-load=3, max-load=5
```

> `http localhost:8080/burstmetrics/services/ngsa/ngsa-svc-2`

```http
HTTP/1.1 200 OK
content-type: text/plain; charset=utf-8
date: Thu, 03 Mar 2022 13:14:13 GMT
server: istio-envoy
transfer-encoding: chunked
x-envoy-upstream-service-time: 1

service=ngsa/ngsa-2, current-load=2, target-load=4, max-load=6
```

> `http localhost:8080/burstmetrics/services/`

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
Date: Thu, 03 Mar 2022 20:24:37 GMT
Server: Kestrel
Transfer-Encoding: chunked

{
    "default/ngsa-svc-1": "service=default/ngsa-1, current-load=1, target-load=3, max-load=5",
    "ngsa/ngsa-svc-2": "service=ngsa/ngsa-2, current-load=2, target-load=4, max-load=6"
}
```

> `http localhost:8080/burstmetrics/hpas/`

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
Date: Thu, 03 Mar 2022 20:24:37 GMT
Server: Kestrel
Transfer-Encoding: chunked

{
    "default/ngsa-hpa-1": "service=default/ngsa-1, current-load=1, target-load=3, max-load=5",
    "ngsa/ngsa-hpa-2": "service=ngsa/ngsa-2, current-load=2, target-load=4, max-load=6"
}
```

#### Example 2: 204 No Content

> `http localhost:8080/burstmetrics/services/default/ngsa-non-existing`

```http
HTTP/1.1 204 No Content
date: Thu, 01 Mar 2022 16:30:26 GMT
server: istio-envoy
x-envoy-upstream-service-time: 16
```

> `http localhost:8080/burstmetrics/services`
> Assuming we don't have any HPA associated with any deployments

```http
HTTP/1.1 204 No Content
date: Thu, 01 Mar 2022 13:10:12 GMT
server: istio-envoy
x-envoy-upstream-service-time: 16
```

### RBAC requirements

In-cluster configuration (as well as default kube-config) for this service requires additional permission to call the `autoscaling` API.

For a complete RBAC example, see [burst-dev.yaml](./deploy/burst-dev.yaml).

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
