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

deploy : clean build

	# Patching Istio ...
	@./patch.sh

	# add config map
	@kubectl create cm burst-wasm-filter --from-file=burst_header.wasm

	# patch ngsa-memory
	@# this will create a new deployment and terminate the old one
	@kubectl patch deployment ngsa -p '{"spec":{"template":{"metadata":{"annotations":{"sidecar.istio.io/userVolume":"[{\"name\":\"wasmfilters-dir\",\"configMap\": {\"name\": \"burst-wasm-filter\"}}]","sidecar.istio.io/userVolumeMount":"[{\"mountPath\":\"/var/local/lib/wasm-filters\",\"name\":\"wasmfilters-dir\"}]"}}}}}'

	# turn the wasm filter on
	@kubectl apply -f deploy/ngsa-memory/filter.yaml

check :
	# curl the healthz endpoint
	@curl -i http://${K8s}/memory/healthz

	# check the healthz endpoint with http
	# this will show the burst header if enabled
	@http http://${K8s}/memory/healthz

clean :
	# delete filter and config map
	@kubectl patch deployment ngsa -p '{"spec":{"template":{"metadata":{"annotations":{"sidecar.istio.io/userVolume":"[]","sidecar.istio.io/userVolumeMount":"[]"}}}}}'
	@kubectl delete --ignore-not-found -f deploy/ngsa-memory/filter.yaml
	@kubectl delete --ignore-not-found cm burst-wasm-filter

test :
	# run a 60 second test
	@cd deploy/loderunner && webv -s http://${K8s} -f benchmark.json -r -l 5 --duration 60

check-metrics :
	# retrieve current values from metrics server
	kubectl get --raw https://localhost:5443/apis/metrics.k8s.io/v1beta1/pods

check-istio :
	@istioctl proxy-status
	@echo ""
	@kubectl logs -l=app=ngsa -c istio-proxy
