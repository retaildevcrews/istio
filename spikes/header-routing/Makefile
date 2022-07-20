.PHONY: delete recreate help

help :
	@echo "Usage:"
	@echo "   make help          - prints this msg"
	@echo "   make all           - delete and recreate a new k3d cluster"
	@echo "   make delete        - delete k3d cluster"
	@echo "   make test-all      - test all apps"
	@echo "   make test-standard - test standard app"
	@echo "   make test-plus     - test plus app"
	@echo "   make test-base     - test base app"

delete :
	# delete the cluster (if exists)
	@# this will fail harmlessly if the cluster does not exist
	-k3d cluster delete ngsa-k3d-cluster

all : delete
	# build k3d cluster
	@k3d cluster create ngsa-k3d-cluster --registry-use k3d-registry.localhost:5000 --config ./k3d.yaml --k3s-arg "--no-deploy=traefik@server:0"

	# wait for cluster to be ready
	@kubectl wait node --for condition=ready --all --timeout=60s
	@sleep 5
	@kubectl wait pod -A --all --for condition=ready --timeout=60s

	# install istio
	@/usr/local/istio/bin/istioctl install --set profile=demo -y -f ./deploy/istio-mesh-config.yaml

	# initialize app namespaces
	kubectl create namespace ngsa
	kubectl create namespace ngsaplus
	kubectl create namespace ngsastd

	@kubectl label namespace ngsa istio-injection=enabled --overwrite
	@kubectl label namespace ngsaplus istio-injection=enabled --overwrite
	@kubectl label namespace ngsastd istio-injection=enabled --overwrite

	# deploy apps
	@kubectl apply -f ./deploy/ngsa-memory
	@kubectl apply -f ./deploy/ngsa-memory-plus
	@kubectl apply -f ./deploy/ngsa-memory-standard

	# deploy routing config
	@kubectl apply -f ./deploy/istio-routing.yaml

	# deploy kiali dashboard
	@kubectl apply -f ./deploy/kiali.yaml

	# deploy jaeger tracing
	@kubectl apply -f ./deploy/jaeger.yaml

	# deploy prometheus for istio
	@kubectl apply -f ./deploy/prometheus.yaml

test-all : test-standard test-plus test-base

test-standard :
	# curl the standard healthz endpoint
	@http http://localhost:30000/healthz Sublevel:standard

test-plus :
	# curl the plus healthz endpoint
	@http http://localhost:30000/healthz Sublevel:plus

test-base :
	# curl the base healthz endpoint
	@http http://localhost:30000/healthz Sublevel:
