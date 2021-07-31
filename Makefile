.PHONY: build build-metrics create delete check clean deploy test build-burstserver

help :
	@echo "Usage:"
	@echo "   make build               - build the plug-in"
	@echo "   make create              - create a kind cluster"
	@echo "   make delete              - delete the kind cluster"
	@echo "   make check               - check the endpoints with curl"
	@echo "   make deploy              - deploy the apps to the cluster (not working)"
	@echo "   make clean               - delete the apps from the cluster (not working)"
	@echo "   make test                - run a LodeRunner test"
	@echo "   make build-burstserver   - build the burst metrics server"
create : delete build
	kind create cluster --config deploy/kind/kind.yaml

	kubectl apply -f deploy/kind/config.yaml

	istioctl install --set profile=demo -y
	kubectl label namespace default istio-injection=enabled

	kubectl wait node --for condition=ready --all --timeout=60s

	@# Install prometheus
	@#@kubectl apply -f ${ISTIO_HOME}/samples/addons/prometheus.yaml

	@# Install kiali
	@#@kubectl apply -f deploy/kiali
	
	@#sleep 5
	@#@kubectl apply -f ${ISTIO_HOME}/samples/addons/kiali.yaml

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
	@kubectl create cm wasm-poc-filter --from-file=wasm_header_poc.wasm

	@# patch any deployments
	@# this will create a new deployment and terminate the old one
	@# TODO - integrate into ngsa-memory.yaml?
	@kubectl patch deployment ngsa -p '{"spec":{"template":{"metadata":{"annotations":{"sidecar.istio.io/userVolume":"[{\"name\":\"wasmfilters-dir\",\"configMap\": {\"name\": \"wasm-poc-filter\"}}]","sidecar.istio.io/userVolumeMount":"[{\"mountPath\":\"/var/local/lib/wasm-filters\",\"name\":\"wasmfilters-dir\"}]"}}}}}'

	@# turn the wasm filter on for each deployment
	@# this is commented out for testing
	@# kubectl apply -f deploy/filter.yaml

	@kubectl wait pod --for condition=ready --all --timeout=60s

	@echo "to load env vars"
	@echo "    source ~/.bashrc"
	@echo "run - make check"

build : build-burstserver
	# build the WebAssembly
	@rm -f wasm_header_poc.wasm
	@cargo build --release --target=wasm32-unknown-unknown
	@cp target/wasm32-unknown-unknown/release/wasm_header_poc.wasm .

delete:
	# delete the cluster (if exists)
	@# this will fail harmlessly if the cluster does not exist
	-kind delete cluster

deploy :
	# TODO deploy the app

	@kubectl apply -f deploy/ngsa-memory/ngsa-memory.yaml
	@kubectl apply -f deploy/ngsa-memory/ngsa-gw.yaml

	@kubectl wait pod --for condition=ready --all --timeout=60s

	@kubectl apply -f deploy/loderunner/loderunner.yaml

check :
	# check the endpoints
	@http http://${GATEWAY_URL}/memory/healthz

clean :
	@# TODO - implement

	# delete filter and config map
	kubectl delete --ignore-not-found -f deploy/filter.yaml
	kubectl delete --ignore-not-found cm wasm-poc-filter

	# delete ngsa
	kubectl delete --ignore-not-found -f  deploy/ngsa-memory/ngsa-memory.yaml
	kubectl delete --ignore-not-found -f  deploy/ngsa-memory/ngsa-gw.yaml

	# show running pods
	@kubectl get po -A

test :
	# run a 90 second test
	@cd deploy/loderunner && webv -s http://${GATEWAY_URL} -f benchmark.json -r -l 20 --duration 90

build-burstserver :
	# build burst metrics server
	@docker build burst -t localhost:5000/burst:local
	@docker push localhost:5000/burst:local

get-pod-metrics :
	# retrieve current values from metrics server
	kubectl get --raw https://localhost:5443/apis/metrics.k8s.io/v1beta1/pods

get-burst-metrics :
    # We're assuming the hpa is in default namespace and tied to ngsa deployment
    # HPA takes 15~30 seconds to receive metrics from metrics-server
    # If you see cpu-load and target-load as -1, then try again
    # If connectionError then check hpa and metrics server 
	@http http://${GATEWAY_URL}/burstmetrics/default/ngsa


### not working yet

mem1 :
	@kubectl apply -f deploy/mem1/app.yaml
	@kubectl apply -f deploy/mem1/gw.yaml
	@kubectl patch deployment mem1 -p '{"spec":{"template":{"metadata":{"annotations":{"sidecar.istio.io/userVolume":"[{\"name\":\"wasmfilters-dir\",\"configMap\": {\"name\": \"wasm-poc-filter\"}}]","sidecar.istio.io/userVolumeMount":"[{\"mountPath\":\"/var/local/lib/wasm-filters\",\"name\":\"wasmfilters-dir\"}]"}}}}}'
	@kubectl apply -f deploy/mem1/filter.yaml

mem1-check :
	@http http://${GATEWAY_URL}/mem1/healthz

mem2 :
	@kubectl apply -f deploy/mem2/app.yaml
	@kubectl apply -f deploy/mem2/gw.yaml
	@kubectl patch deployment mem2 -p '{"spec":{"template":{"metadata":{"annotations":{"sidecar.istio.io/userVolume":"[{\"name\":\"wasmfilters-dir\",\"configMap\": {\"name\": \"wasm-poc-filter\"}}]","sidecar.istio.io/userVolumeMount":"[{\"mountPath\":\"/var/local/lib/wasm-filters\",\"name\":\"wasmfilters-dir\"}]"}}}}}'
	@kubectl apply -f deploy/mem2/filter.yaml

mem2-check :
	@http http://${GATEWAY_URL}/mem2/healthz

mem3 :
	@kubectl apply -f deploy/mem3/app.yaml
	@kubectl apply -f deploy/mem3/gw.yaml
	@kubectl patch deployment mem3 -p '{"spec":{"template":{"metadata":{"annotations":{"sidecar.istio.io/userVolume":"[{\"name\":\"wasmfilters-dir\",\"configMap\": {\"name\": \"wasm-poc-filter\"}}]","sidecar.istio.io/userVolumeMount":"[{\"mountPath\":\"/var/local/lib/wasm-filters\",\"name\":\"wasmfilters-dir\"}]"}}}}}'
	@kubectl apply -f deploy/mem3/filter.yaml

mem3-check :
	@http http://${GATEWAY_URL}/mem3/healthz
