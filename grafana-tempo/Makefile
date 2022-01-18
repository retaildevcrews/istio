SHELL=/bin/bash
.PHONY: build-apps create istio-tempo build-ngsa build-mock-rest basic delete

all: create
build-apps : build-ngsa build-mock-rest

delete:
	@k3d cluster delete tempo

create:
	@k3d cluster create tempo --registry-use k3d-registry.localhost:5000 --config ./k3d.yaml --port "3000:80@loadbalancer" --k3s-arg "--no-deploy=traefik@server:0" --k3s-arg "--no-deploy=servicelb@server:0"

	@helm repo add grafana https://grafana.github.io/helm-charts
	@helm repo update

	@kubectl wait node --for condition=ready --all --timeout=60s
	@sleep 5
	@kubectl wait pod -A --all --for condition=ready --timeout=60s


istio-tempo: 
	@istioctl install -y --set profile=demo -f istio/istio-operator.yaml
	# kubectl apply -f istio-operator.yaml
	@kubectl label namespace default istio-injection=enabled --overwrite
	@kubectl apply -f istio/ngsa-memory

	@kubectl create ns tracing

	@helm upgrade --install tempo grafana/tempo -n tracing --set tempo.receivers.zipkin.endpoint=0.0.0.0:9411
	@helm upgrade -f istio/grafana-single-values.yaml --install grafana grafana/grafana -n tracing

build-apps: build-ngsa build-mock-rest

build-ngsa:
	# Build and push modified ngsa-app
	@docker build -t localhost:5000/ngsa-app:local -f ngsa-app/Dockerfile ./ngsa-app
	@docker push localhost:5000/ngsa-app:local

build-mock-rest:
	# Build and push mock-rest
	@docker build -t localhost:5000/mock-rest:local -f mock-rest/Dockerfile ./mock-rest
	@docker push localhost:5000/mock-rest:local
