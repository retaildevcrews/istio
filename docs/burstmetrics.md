# Burst Metrics API

This document decribes Burst Metrics Rest API documentation and data types.

## Burst Metrics Endpoint for specific namespace and deployment

Get the details of the currently deployed HPA in a specific namespace and deployment name.

- **URL** : `/burstmetrics/{ns}/{hpa}`
  > Here `{ns}` represents k8s namespace and `{hpa}` represents deployment/hpa
- **Method** : `GET`
- **Permissions/Auth** : None
- **Success Response**
  - **Code** : `200 OK`
    > When the `{hpa}` deployment/HPA is found in `{ns}` namespace

    **Content Example** : text/plain

    `service={ns}/{name}, current-load=<int>, target-load=<int>, max-load=<int>`
  - **Code** : `204 No Content`
    > When an `{hpa}` deployment/HPA is not found in `{ns}` namespace but the API call itself is successful
