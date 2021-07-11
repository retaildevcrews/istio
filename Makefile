.PHONY: build build-metrics create delete check clean deploy test load-test

help :
	@echo "Usage:"
	@echo "   make build        - build the plug-in"
	@echo "   make create       - create a kind cluster"
	@echo "   make delete       - delete the kind cluster"
	@echo "   make deploy       - deploy the apps to the cluster"
	@echo "   make check        - check the endpoints with curl"
	@echo "   make clean        - delete the apps from the cluster"
	@echo "   make test         - run a LodeRunner test (generates warnings)"
	@echo "   make load-test    - run a 60 second load test"

build :
	rm -f wasm_header_poc.wasm
	cargo build --release --target=wasm32-unknown-unknown
	cp target/wasm32-unknown-unknown/release/wasm_header_poc.wasm .

build-metrics :
	docker build pymetric -t pymetric:local
	kind load docker-image pymetric:local

delete:
	# delete the cluster (if exists)
	@# this will fail harmlessly if the cluster does not exist
	@kind delete cluster

create :
	# create the cluster and wait for ready
	@# this will fail harmlessly if the cluster exists
	@# default cluster name is kind

	@kind create cluster --config deploy/kind/kind.yaml

	# wait for cluster to be ready
	@kubectl wait node --for condition=ready --all --timeout=60s

	# Install istio
	@istioctl install --set profile=demo -y
	@kubectl label namespace default istio-injection=enabled

	# connect the registry to the cluster network
	-docker network create kind
	-docker network connect "kind" "kind-registry"

	# Install prometheus
	#@kubectl apply -f ${ISTIO_HOME}/samples/addons/prometheus.yaml

	# Install kiali
	#@kubectl apply -f deploy/kiali
	
	#sleep 5
	#@kubectl apply -f ${ISTIO_HOME}/samples/addons/kiali.yaml

	@export INGRESS_PORT=$(kubectl -n istio-system get service istio-ingressgateway -o jsonpath='{.spec.ports[?(@.name=="http2")].nodePort}')
	@export SECURE_INGRESS_PORT=$(kubectl -n istio-system get service istio-ingressgateway -o jsonpath='{.spec.ports[?(@.name=="https")].nodePort}')
	@export INGRESS_HOST=$(kubectl get po -l istio=ingressgateway -n istio-system -o jsonpath='{.items[0].status.hostIP}')
	@export GATEWAY_URL=$INGRESS_HOST:$INGRESS_PORT

deploy : build-metrics
	# deploy the app
	@# continue on most errors
	-kubectl apply -f deploy/ngsa-memory
	@kubectl wait pod ngsa-memory --for condition=ready --timeout=30s

	# deploy metrics server
	@kubectl apply -f deploy/pymetric

	# display pod status
	@kubectl get po

check :
	# curl all of the endpoints
	@curl localhost:30080/version

clean :
	# delete the deployment
	@# continue on error
	@kubectl delete --ignore-not-found -f  cmdemoyml/pymetric.yaml
	@kubectl delete --ignore-not-found -f  cmdemoyml/pymetric-gw.yaml
	@kubectl delete --ignore-not-found -f  cmdemoyml/ngsa.yaml
	@kubectl delete --ignore-not-found -f  cmdemoyml/ngsa-gw.yaml

	# show running pods
	@kubectl get po -A

test :
	# run a single test
	cd deploy/loderunner && webv -s http://localhost:30080 -f baseline.json

load-test :
	# run a 10 second load test
	cd deploy/loderunner && webv -s http://localhost:30080 -f benchmark.json -r -l 1 --duration 10
