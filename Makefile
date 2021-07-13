.PHONY: build build-metrics create delete check clean deploy test load-test test-all

help :
	@echo "Usage:"
	@echo "   make build        - build the plug-in"
	@echo "   make create       - create a kind cluster"
	@echo "   make delete       - delete the kind cluster"
	@echo "   make check        - check the endpoints with curl"
	@echo "   make deploy       - deploy the apps to the cluster (not working)"
	@echo "   make clean        - delete the apps from the cluster (not working)"
	@echo "   make test         - run a LodeRunner test"
	@echo "   make load-test    - run a 60 second load test"
	@echo "   make test-all     - check, test and load-test"

create : delete build
	kind create cluster --config deploy/kind/kind.yaml

	kubectl apply -f deploy/kind/config.yaml

	istioctl install --set profile=demo -y
	kubectl label namespace default istio-injection=enabled

	kubectl wait node --for condition=ready --all --timeout=60s

	# Install prometheus
	#@kubectl apply -f ${ISTIO_HOME}/samples/addons/prometheus.yaml

	# Install kiali
	#@kubectl apply -f deploy/kiali
	
	#sleep 5
	#@kubectl apply -f ${ISTIO_HOME}/samples/addons/kiali.yaml

	kubectl apply -f deploy/burst/burst.yaml
	kubectl apply -f deploy/burst/gw-burst.yaml
	kubectl apply -f deploy/ngsa-memory/ngsa-memory.yaml
	kubectl apply -f deploy/ngsa-memory/ngsa-gw.yaml

	kubectl wait pod --for condition=ready --all --timeout=60s

	# Patching Istio ...
	@./patch.sh

	@# delete filter and config map
	@kubectl delete --ignore-not-found -f deploy/filter.yaml
	@kubectl delete --ignore-not-found cm wasm-poc-filter

	@# add config map
	@kubectl create cm wasm-poc-filter --from-file=wasm_header_poc.wasm

	@# patch any deployments
	@kubectl patch deployment ngsa -p '{"spec":{"template":{"metadata":{"annotations":{"sidecar.istio.io/userVolume":"[{\"name\":\"wasmfilters-dir\",\"configMap\": {\"name\": \"wasm-poc-filter\"}}]","sidecar.istio.io/userVolumeMount":"[{\"mountPath\":\"/var/local/lib/wasm-filters\",\"name\":\"wasmfilters-dir\"}]"}}}}}'

	@# turn the wasm filter on for each deployment
	@# kubectl apply -f deploy/filter.yaml

	@kubectl wait pod --for condition=ready --all --timeout=60s

	@echo "to load env vars"
	@echo "    source ~/.bashrc"
	@echo "run - make test-all"

build :
	rm -f wasm_header_poc.wasm
	cargo build --release --target=wasm32-unknown-unknown
	cp target/wasm32-unknown-unknown/release/wasm_header_poc.wasm .

delete:
	# delete the cluster (if exists)
	@# this will fail harmlessly if the cluster does not exist
	-kind delete cluster

deploy :
	# TODO deploy the app
	@kubectl apply -f deploy/loderunner/loderunner.yaml

check :
	# check the endpoints
	@http http://${GATEWAY_URL}/memory/healthz

clean :
	# delete the deployment
	# TODO - implement
	@# continue on error
	@kubectl delete --ignore-not-found -f  deploy/burst/burst.yaml
	@kubectl delete --ignore-not-found -f  deploy/burst/gw-burst.yaml
	@kubectl delete --ignore-not-found -f  deploy/ngsa-memory/ngsa-memory.yaml
	@kubectl delete --ignore-not-found -f  deploy/ngsa-memory/ngsa-gw.yaml

	# show running pods
	@kubectl get po -A

test :
	# run a single test
	cd deploy/loderunner && webv -s http://${GATEWAY_URL} -f baseline.json

load-test :
	# run a 10 second load test
	cd deploy/loderunner && webv -s http://${GATEWAY_URL} -f benchmark.json -r -l 1 --duration 10

test-all : check test load-test
	# ran all tests

burstserver-build :
	docker build burst -t localhost:5000/burst:local
	docker push localhost:5000/burst:local

# Metrics Testing Additions

create-metrics-server :
	# deploy metrics server with --kubelet-insecure-tls flag
	kubectl apply -f deploy/metrics/components.yaml

get-metrics :
	# retrieve current values from metrics server
	kubectl get --raw https://localhost:5443/apis/metrics.k8s.io/v1beta1/pods | jq

create-hpa-ngsa :
	# create HPA for ngsa deployment for testing
	kubectl autoscale deployment ngsa --cpu-percent=50 --min=1 --max=5

delete-hpa-ngsa :
	kubectl delete hpa ngsa
