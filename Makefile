.PHONY: build deploy check check-metrics check-istio test clean

help :
	@echo "Usage:"
	@echo "   make build               - build the plug-in"
	@echo "   make deploy              - deploy the istio plugin to ngsa"
	@echo "   make check               - check the endpoints with curl"
	@echo "   make check-istio         - check istio status and logs"
	@echo "   make check-metrics       - check the raw pod metrics"
	@echo "   make test                - run a LodeRunner test"
	@echo "   make clean               - remove the istio plugin from ngsa"

build :
	# build the WebAssembly
	@rm -f burst_header.wasm
	@cargo build --release --target=wasm32-unknown-unknown
	@cp target/wasm32-unknown-unknown/release/burst_header.wasm .

deploy-ingress-filter : clean-ingress-filter build
    # For POC.
	# add config map with rust-wasm binary as its content 
	@kubectl create cm burst-wasm-filter -n istio-system --from-file=burst_header.wasm

	# Patch istio-ingressgateway, will create a new deployment terminating the old one
	@kubectl patch deployment istio-ingressgateway -n istio-system --patch-file deploy/istio-ingress/ingressgateway-patch.yaml

	# Patch istio-ingressgateway service, will expose the service to nodeport 300083
	@kubectl patch service istio-ingressgateway -n istio-system --patch-file deploy/istio-ingress/ingressservice-patch.yaml

	# apply wasm filter to istio-ingress
	@kubectl apply -f deploy/istio-ingress/ingress-filter.yaml

clean-ingress-filter :
    # For POC
	# Remove patches from istio-ingressgateway
	-@kubectl get deploy -n istio-system istio-ingressgateway -o json | jq '.spec.template.spec.containers[0].volumeMounts | map(.name == "wasmfilters-dir") | index(true)' | xargs -I % kubectl patch deployment istio-ingressgateway -n istio-system --type=json -p "[{'op': 'remove', 'path': '/spec/template/spec/containers/0/volumeMounts/%'}]"

	-@kubectl get deploy -n istio-system istio-ingressgateway -o json | jq '.spec.template.spec.volumes | map(.name == "wasmfilters-dir") | index(true)' | xargs -I % kubectl patch deployment istio-ingressgateway -n istio-system --type=json -p "[{'op': 'remove', 'path': '/spec/template/spec/volumes/%'}]"

	# delete filter and config map
	@kubectl delete --ignore-not-found -f deploy/istio-ingress/ingress-filter.yaml
	@kubectl delete --ignore-not-found cm -n istio-system burst-wasm-filter

deploy : clean build

	# add config map
	@kubectl create cm burst-wasm-filter --from-file=burst_header.wasm

	# patch ngsa-memory
	@# this will create a new deployment and terminate the old one
	@kubectl patch deployment ngsa -p '{"spec":{"template":{"metadata":{"annotations":{"sidecar.istio.io/userVolume":"[{\"name\":\"wasmfilters-dir\",\"configMap\": {\"name\": \"burst-wasm-filter\"}}]","sidecar.istio.io/userVolumeMount":"[{\"mountPath\":\"/var/local/lib/wasm-filters\",\"name\":\"wasmfilters-dir\"}]"}}}}}'

	# turn the wasm filter on
	@kubectl apply -f deploy/ngsa-memory/filter.yaml

check-ingress:
	# curl ngsa healthz endpoint directly via nodeport
	# Should not return any burst header
	@http http://localhost:30080/healthz

	# Now do the same via ingressgateway
	# this will show the burst header if enabled
	@http http://localhost:30083/memory/healthz
check :
	# curl the healthz endpoint
	@curl -i http://localhost:30080/healthz

	# check the healthz endpoint with http
	# this will show the burst header if enabled
	@http http://localhost:30080/healthz

clean :
	# delete filter and config map
	@kubectl patch deployment ngsa -p '{"spec":{"template":{"metadata":{"annotations":{"sidecar.istio.io/userVolume":"[]","sidecar.istio.io/userVolumeMount":"[]"}}}}}'
	@kubectl delete --ignore-not-found -f deploy/ngsa-memory/filter.yaml
	@kubectl delete --ignore-not-found cm burst-wasm-filter

test :
	# run a timed test ('$(seconds)' seconds)
	@test "$(seconds)" \
		&& cd deploy/loderunner \
		&& webv -s http://localhost:30080/ -f benchmark.json -v -s -r -l 5 --duration $(seconds) \
		|| echo "usage: make $@ seconds=number\ne.g.   make $@ seconds=60"

prom-adapter-hpa :
	# Add and update prometheus adapter helm
	@helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
	@helm repo update
	# Deploy prometheus
	@kubectl create ns monitoring
	@kubectl apply -f deploy/prometheus/prometheus.yaml
	# Deploy prometheus adapter, it'll take a min or two
	@helm upgrade --install -n monitoring prom-metrics-adapter -f deploy/prometheus/prom-adapter-helm-values.yaml prometheus-community/prometheus-adapter --wait
	# Recreate HPA with custom metrics
	@kubectl apply -f deploy/prometheus/hpa-custom-metrics.yaml
	# Deploy sample constant load
	@kubectl apply -f deploy/loderunner/loderunner.yaml

check-metrics :
	# retrieve current values from metrics server
	kubectl get --raw https://localhost:5443/apis/metrics.k8s.io/v1beta1/pods

check-istio :
	@istioctl proxy-status
	@echo ""
	@kubectl logs -l=app=ngsa -c istio-proxy
