.PHONY: build build-metrics create delete check clean deploy test build-burstserver get-pod-metrics istio-check

help :
	@echo "Usage:"
	@echo "   make build               - build the plug-in"
	@echo "   make istio-check         - check istio status and logs"
	@echo "   make check               - check the endpoints with curl"
	@echo "   make test                - run a LodeRunner test"
	@echo "   make clean               - delete the istio plugin from ngsa"
	@echo "   make deploy              - deploy the istio plugin to ngsa"
	@echo "   make create              - create a kind cluster"
	@echo "   make delete              - delete the kind cluster"
	@echo "   get-pod-metrics          - get the raw pod metrics"

build :
	# build the WebAssembly
	@rm -f burst_header.wasm
	@cargo build --release --target=wasm32-unknown-unknown
	@cp target/wasm32-unknown-unknown/release/burst_header.wasm .

delete:
	# delete the cluster (if exists)
	@# this will fail harmlessly if the cluster does not exist
	-kind delete cluster

deploy : clean build

	# Patching Istio ...
	@./patch.sh

	# add config map
	@kubectl create cm burst-wasm-filter --from-file=burst_header.wasm

	# patch any deployments
	@# this will create a new deployment and terminate the old one
	@kubectl patch deployment ngsa -p '{"spec":{"template":{"metadata":{"annotations":{"sidecar.istio.io/userVolume":"[{\"name\":\"wasmfilters-dir\",\"configMap\": {\"name\": \"burst-wasm-filter\"}}]","sidecar.istio.io/userVolumeMount":"[{\"mountPath\":\"/var/local/lib/wasm-filters\",\"name\":\"wasmfilters-dir\"}]"}}}}}'

	# turn the wasm filter on for each deployment
	@kubectl apply -f deploy/ngsa-memory/filter.yaml

	@kubectl wait pod --for condition=ready --all --timeout=60s

check :
	# get the metrics
	@curl -q http://${K8s}/burstmetrics/default/ngsa
	@echo ""

	# curl the healthz endpoint
	@curl -i http://${K8s}/memory/healthz

	# check the healthz endpoint
	@http http://${K8s}/memory/healthz

clean :
	# delete filter and config map
	@kubectl patch deployment ngsa -p '{"spec":{"template":{"metadata":{"annotations":{"sidecar.istio.io/userVolume":"[]","sidecar.istio.io/userVolumeMount":"[]"}}}}}'
	@kubectl delete --ignore-not-found -f deploy/ngsa-memory/filter.yaml
	@kubectl delete --ignore-not-found cm burst-wasm-filter

test :
	# run a 90 second test
	@cd deploy/loderunner && webv -s http://${K8s} -f benchmark.json -r -l 20 --duration 90

get-pod-metrics :
	# retrieve current values from metrics server
	kubectl get --raw https://localhost:5443/apis/metrics.k8s.io/v1beta1/pods

istio-check :
	@istioctl proxy-status
	@echo ""
	@kubectl logs -l=app=ngsa -c istio-proxy
