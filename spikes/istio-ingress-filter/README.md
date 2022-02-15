# Wasm Filter for Istio Ingress Controller

> Istio IngressGateway controller filter with Rust and Web Assembly

## Deploy

```bash
# Step 0. If your cluster is not modified, then this step can be skipped
## From your REPO root dir
cd clusteradm

## This will delete and recreate your cluster
make all

# Step 1. Clean any exising wasm deployment
## cd into the repo root
cd ..

## Delete any previous wasm filter deployment applied to ngsa sidecar
make clean

# Step 2. Deploy the wasm filter for istio ingresscontroller
## Goto this spike folder
cd ./spikes/istio-ingress-wasm-filter

## Deploy wasm filter in istio ingressgateway
make deploy

# Step 3. Check if the filter deployment is correct
## Afterwards check the ngsa service directly
## It will not show the burst header
make check-direct

## Then check ngsa service through istio ingress
## It should show the burst header
make check-ingress

```

## EnvoyFilter yaml and ingresscontroller patch

The envoy filter (located at [ingress-filter.yaml](./istio-ingress/ingress-filter.yaml)) is applied to ingress controller using proper label selector (`app: istio-ingressgateway` and `istio: ingressgateway`).

Ingress controller patch object is located at [ingress-patch.yaml](./istio-ingress/ingress-patch.yaml).

When the patch is applied (in `Makefile` target: `deploy`), the ingresscontroller deployment will be 
deleted and a new one will be created.
