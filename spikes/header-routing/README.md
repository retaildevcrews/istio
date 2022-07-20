# Header based routing

Implementing header-based routing with Istio Service Mesh. There are three versions of the NGSA app deployed: base, standard and plus. Requests will be routed to the services as follows:

- "sublevel: plus" routes to ngsa-plus (ngsaplus namespace)
- "sublevel: standard" routes to ngsa-standard (ngsastd namespace)
- "sublevel: " and any other requests route to ngsa (ngsa namespace)

## Deploy

```bash

# enter the header-routing spikes folder
cd ./spikes/header-routing

# Make the k3d cluster
make all

# let k3d cluster finish deploying

# send requests to all three services in a loop
watch make test-all

```

## Observability

Kiali is installed by default with the k3d cluster specific for this spike. The kiali dashboard can be accessed on **port 30085** of the codespace instance.

We also have Jaeger setup, wired with Jaeger and Kiali, and you can view the Jaeger dashboard on **port 30086**.

In Kiali, click on the "Graph" panel in the sidebar to view a topology of the services. To view live data, ensure that `watch make test-all` is running in the background. Kiali also has an option to view the traces by clicking on a node in the graph view, or by selecting a specific workload from the "Workloads" panel.

## Routing config

The istio routing config (located at [istio-routing.yaml](./deploy/istio-routing.yaml)) consists of a Istio Gateway and a VirtualService object to enable header-based routing. The core logic is embedded in the VirtualService, where matches on the `Sublevel` header are routing to the appropriate service.
