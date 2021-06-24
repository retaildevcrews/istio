.PHONY: build run create delete check clean deploy test load-test

help :
	@echo "Usage:"
	@echo "   make build            - build Istio plug-in"
	@echo "   make run              - run Istio with plug-in via Docker"
	@echo "   make create           - create a kind cluster"
	@echo "   make delete           - delete the kind cluster"
	@echo "   make deploy           - deploy the apps to the cluster"
	@echo "   make check            - check the endpoints with curl"
	@echo "   make clean            - delete the apps from the cluster"
	@echo "   make test             - run a LodeRunner test (generates warnings)"
	@echo "   make load-test        - run a 60 second load test"

build:
	cargo build --target wasm32-unknown-unknown --release
	cp target/wasm32-unknown-unknown/release/hello_world.wasm ./
	wasme build precompiled hello_world.wasm --tag hello_world:v0.1
	rm hello_world.wasm

run: build
	wasme deploy envoy hello_world:v0.1 --envoy-image=istio/proxyv2:1.5.1 --bootstrap=envoy-bootstrap.yml

delete:
	# delete the cluster (if exists)
	@# this will fail harmlessly if the cluster does not exist
	@kind delete cluster

create:
	# create the cluster and wait for ready
	@# this will fail harmlessly if the cluster exists
	@# default cluster name is kind

	@kind create cluster --config deploy/kind/kind.yaml

	# wait for cluster to be ready
	@kubectl wait node --for condition=ready --all --timeout=60s

deploy:
	# deploy the app
	@# continue on most errors
	-kubectl apply -f deploy/ngsa-memory

	# deploy LodeRunner after the app starts
	@kubectl wait pod ngsa-memory --for condition=ready --timeout=30s
	-kubectl apply -f deploy/loderunner

	# wait for the pods to start
	@kubectl wait pod loderunner --for condition=ready --timeout=30s

	# display pod status
	@kubectl get po -A

check :
	# curl all of the endpoints
	@curl localhost:30080/version
	@echo "\n"
	@curl localhost:30088/version
	@echo "\n"

clean :
	# delete the deployment
	@# continue on error
	-kubectl delete -f deploy/loderunner --ignore-not-found=true
	-kubectl delete -f deploy/ngsa-memory --ignore-not-found=true

	# show running pods
	@kubectl get po -A

test:
	# run a single test
	cd deploy/loderunner && webv -s http://localhost:30080 -f baseline.json

load-test:
	# run a 10 second load test
	cd deploy/loderunner && webv -s http://localhost:30080 -f benchmark.json -r -l 1 --duration 10
