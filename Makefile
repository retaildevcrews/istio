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
	@echo "   make build-burstserver   - build the burst metrics server"
	@echo "   get-pod-metrics          - get the raw pod metrics"
create : delete build build-burstserver
	kind create cluster --config deploy/kind/kind.yaml

	kubectl apply -f deploy/kind/config.yaml

	istioctl install --set profile=demo -y
	kubectl label namespace default istio-injection=enabled

	kubectl wait node --for condition=ready --all --timeout=60s

	@kubectl apply -f deploy/burst/burst.yaml
	@kubectl apply -f deploy/burst/gw-burst.yaml
	@kubectl apply -f deploy/ngsa-memory/ngsa-memory.yaml
	@kubectl apply -f deploy/ngsa-memory/ngsa-gw.yaml

	# deploy metrics server
	@kubectl apply -f deploy/metrics/components.yaml

	# create HPA for ngsa deployment for testing
	kubectl autoscale deployment ngsa --cpu-percent=50 --min=1 --max=2

	kubectl wait pod --for condition=ready --all --timeout=60s

	# Patching Istio ...
	@./patch.sh

	@# add config map
	@kubectl create cm burst-wasm-filter --from-file=burst_header.wasm

	@# patch any deployments
	@# this will create a new deployment and terminate the old one
	@# TODO - integrate into ngsa-memory.yaml?
	@kubectl patch deployment ngsa -p '{"spec":{"template":{"metadata":{"annotations":{"sidecar.istio.io/userVolume":"[{\"name\":\"wasmfilters-dir\",\"configMap\": {\"name\": \"burst-wasm-filter\"}}]","sidecar.istio.io/userVolumeMount":"[{\"mountPath\":\"/var/local/lib/wasm-filters\",\"name\":\"wasmfilters-dir\"}]"}}}}}'

	@# turn the wasm filter on for each deployment
	@kubectl apply -f deploy/ngsa-memory/filter.yaml

	@kubectl wait pod --for condition=ready --all --timeout=60s

	@echo "to load env vars"
	@echo "    source ~/.bashrc"
	@echo "run - make check"

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

build-burstserver :
	# build burst metrics server
	@docker build burst -t localhost:5000/burst:local
	@docker push localhost:5000/burst:local

get-pod-metrics :
	# retrieve current values from metrics server
	kubectl get --raw https://localhost:5443/apis/metrics.k8s.io/v1beta1/pods


### not working yet

mem1 :
	@kubectl apply -f deploy/mem1/app.yaml
	@kubectl apply -f deploy/mem1/gw.yaml
	@kubectl patch deployment mem1 -p '{"spec":{"template":{"metadata":{"annotations":{"sidecar.istio.io/userVolume":"[{\"name\":\"wasmfilters-dir\",\"configMap\": {\"name\": \"burst-wasm-filter\"}}]","sidecar.istio.io/userVolumeMount":"[{\"mountPath\":\"/var/local/lib/wasm-filters\",\"name\":\"wasmfilters-dir\"}]"}}}}}'
	@kubectl apply -f deploy/mem1/filter.yaml

mem1-check :
	@http http://${K8s}/mem1/healthz

mem2 :
	@kubectl apply -f deploy/mem2/app.yaml
	@kubectl apply -f deploy/mem2/gw.yaml
	@kubectl patch deployment mem2 -p '{"spec":{"template":{"metadata":{"annotations":{"sidecar.istio.io/userVolume":"[{\"name\":\"wasmfilters-dir\",\"configMap\": {\"name\": \"burst-wasm-filter\"}}]","sidecar.istio.io/userVolumeMount":"[{\"mountPath\":\"/var/local/lib/wasm-filters\",\"name\":\"wasmfilters-dir\"}]"}}}}}'
	@kubectl apply -f deploy/mem2/filter.yaml

mem2-check :
	@http http://${K8s}/mem2/healthz

mem3 :
	@kubectl apply -f deploy/mem3/app.yaml
	@kubectl apply -f deploy/mem3/gw.yaml
	@kubectl patch deployment mem3 -p '{"spec":{"template":{"metadata":{"annotations":{"sidecar.istio.io/userVolume":"[{\"name\":\"wasmfilters-dir\",\"configMap\": {\"name\": \"burst-wasm-filter\"}}]","sidecar.istio.io/userVolumeMount":"[{\"mountPath\":\"/var/local/lib/wasm-filters\",\"name\":\"wasmfilters-dir\"}]"}}}}}'
	@kubectl apply -f deploy/mem3/filter.yaml

mem3-check :
	@http http://${K8s}/mem3/healthz

istio-check :
	@istioctl proxy-status
	@echo ""
	@kubectl logs -l=app=ngsa -c istio-proxy
