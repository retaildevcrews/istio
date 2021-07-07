# These are samples and process I  used for early examination of a rust WASM filter deployed in Istio.

You will likely need to tweak things for your own env. These samples should be seen as a guide and not a fully developed pipeline.
##
Environment

chmar@cmonecsmvm:~/istio/spikes/cmexamples$ rustup show
Default host: x86_64-unknown-linux-gnu
rustup home:  /home/chmar/.rustup

installed toolchains
--------------------

stable-x86_64-unknown-linux-gnu
nightly-x86_64-unknown-linux-gnu (default)

installed targets for active toolchain
--------------------------------------

wasm32-unknown-unknown
x86_64-unknown-linux-gnu

active toolchain
----------------

nightly-x86_64-unknown-linux-gnu (default)
rustc 1.55.0-nightly (a435b49e8 2021-06-28)

chmar@cmonecsmvm:~/istio/spikes/cmexamples$ istioctl version
client version: 1.10.2
control plane version: 1.10.2
data plane version: 1.10.2 (9 proxies)

## Process
install kind and create a cluster with the local repo enabled!
install istio into the new cluster
switch kubectl to the istio context
install the bookinfo application and follow their environment set up. 

I had a .sh I sourced with this in it as shortcut:

export INGRESS_PORT=$(kubectl -n istio-system get service istio-ingressgateway -o jsonpath='{.spec.ports[?(@.name=="http2")].nodePort}')
export SECURE_INGRESS_PORT=$(kubectl -n istio-system get service istio-ingressgateway -o jsonpath='{.spec.ports[?(@.name=="https")].nodePort}')
export INGRESS_HOST=$(kubectl get po -l istio=ingressgateway -n istio-system -o jsonpath='{.items[0].status.hostIP}')
export GATEWAY_URL=$INGRESS_HOST:$INGRESS_PORT



use docker to buld the python image under pymetric folder then tag it for and push it to your local kind repo
*OR* use your own service and then modify the rust code to use it.

If you use the pymetric app then apply the yaml in the pymetricyaml folder making eny edits desired

switch to the rust directory and build OR edit and build your own riff
 cargo build --release --target=wasm32-unknown-unknown

### delete any existing config map
kubectl delete -n default cm wasm-poc-filter

### create the config map with embedded wasm
kubectl create cm wasm_header_poc --from-file=../target/wasm32-unknown-unknown/release/wasm_header_poc.wasm

### patch the pod to maek sure it has access to the wasm
kubectl patch deployment -n default productpage-v1 -p '{"spec":{"template":{"metadata":{"annotations":{"sidecar.istio.io/userVolume":"[{\"name\":\"wasmfilters-dir\",\"configMap\": {\"name\": \"wasm-poc-filter\"}}]","sidecar.istio.io/userVolumeMount":"[{\"mountPath\":\"/var/local/lib/wasm-filters\",\"name\":\"wasmfilters-dir\"}]"}}}}}'

### kick the service in effort to reload configmap bytes
kubectl scale deployments/productpage-v1 --replicas=0
kubectl scale deployments/productpage-v1 --replicas=1

### apply the wip yaml file in the 
delete -f wip.yml
apply -f wip.yml

### run proxy status to make sure you see sync sync on product page

istioctl proxy-status

>NAME                                                   CDS        LDS        EDS        RDS          ISTIOD                      VERSION
details-v1-79f774bdb9-m4nwd.default                    SYNCED     SYNCED     SYNCED     SYNCED       istiod-664799f4bc-4ntpn     1.10.2
istio-egressgateway-78cb6c4799-dclwv.istio-system      SYNCED     SYNCED     SYNCED     NOT SENT     istiod-664799f4bc-4ntpn     1.10.2
istio-ingressgateway-59644976b5-ktrqs.istio-system     SYNCED     SYNCED     SYNCED     SYNCED       istiod-664799f4bc-4ntpn     1.10.2
productpage-v1-c87dffdd9-gtfzq.default                 SYNCED     SYNCED     SYNCED     SYNCED       istiod-664799f4bc-4ntpn     1.10.2
pymetric-8bd684dcb-kllxz.default                       SYNCED     SYNCED     SYNCED     SYNCED       istiod-664799f4bc-4ntpn     1.10.2
ratings-v1-b6994bb9-m2nhw.default                      SYNCED     SYNCED     SYNCED     SYNCED       istiod-664799f4bc-4ntpn     1.10.2
reviews-v1-545db77b95-gx2j9.default                    SYNCED     SYNCED     SYNCED     SYNCED       istiod-664799f4bc-4ntpn     1.10.2
reviews-v2-7bf8c9648f-z8dj2.default                    SYNCED     SYNCED     SYNCED     SYNCED       istiod-664799f4bc-4ntpn     1.10.2
reviews-v3-84779c7bbc-5jxpj.default                    SYNCED     SYNCED     SYNCED     SYNCED       istiod-664799f4bc-4ntpn     1.10.2


curl to see results. rinse and repeat as needed.



