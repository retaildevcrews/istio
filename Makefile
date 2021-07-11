.PHONY: build build-metrics create delete check clean deploy test load-test finish

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

delete:
	# delete the cluster (if exists)
	@# this will fail harmlessly if the cluster does not exist
	-kind delete cluster

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
	# check the endpoints
	@http http://${GATEWAY_URL}/healthz

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

create : delete
	./kindlocalreg.sh kind

	kubectl config use-context kind-kind

	istioctl install --set profile=demo -y
	kubectl label namespace default istio-injection=enabled

	kubectl wait node --for condition=ready --all --timeout=60s

	# Install prometheus
	#@kubectl apply -f ${ISTIO_HOME}/samples/addons/prometheus.yaml

	# Install kiali
	#@kubectl apply -f deploy/kiali
	
	#sleep 5
	#@kubectl apply -f ${ISTIO_HOME}/samples/addons/kiali.yaml

	kubectl apply -f cmdemoyml/pymetric.yaml
	kubectl apply -f cmdemoyml/pymetric-gw.yaml
	kubectl apply -f cmdemoyml/ngsa.yaml
	kubectl apply -f cmdemoyml/ngsa-gw.yaml
	kubectl wait pod --for condition=ready --all --timeout=60s

	#source tcall.sh

finish :
	#Patching Istio ...
	@./patch.sh

	@kubectl apply -f cmdemoyml/filter.yml

	@kubectl get pod
