# Burst Metrics Service

Burst metrics service is responsible for reading HPA metrics from a deployment/HPA and return burst metrics header for the deployment. It is implemented as a REST service.

Currently it will capture current pod count and max pod count from an HPA.

TODO: put some impl details about the k8s API timer

## API Endpoints

### `/burstmetrics/{namespace}/{deployment or HPA}`

This is the only endpoint exposed by this service.

This endpoint will check the k8s {namespace} for the{deployment/HPA} and return the burst metrics header content.

#### Params

None

#### Return

TODO: If it can find specified HPA in the specified namespace it gets data from

```http

HTTP/1.1 200 OK
content-type: text/plain; charset=utf-8
date: Mon, 23 Aug 2021 16:29:51 GMT
server: istio-envoy
transfer-encoding: chunked
x-envoy-upstream-service-time: 1

service=default/ngsa, current-load=1, target-load=1, max-load=2

```

HTTP/1.1 204 No Content
date: Mon, 23 Aug 2021 16:30:26 GMT
server: istio-envoy
x-envoy-upstream-service-time: 16

## Deployment

### RBAC

InCluster

TODO: link files
