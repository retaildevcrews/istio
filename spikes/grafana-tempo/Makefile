SHELL=/bin/bash
.PHONY: build-apps create istio-tempo build-ngsa basic delete

all: create
build-apps : build-ngsa

delete:
	@k3d cluster delete tempo

create:
	@k3d cluster create tempo --registry-use k3d-registry.localhost:5000 --config ./k3d.yaml --port "3000:80@loadbalancer" --k3s-arg "--no-deploy=traefik@server:0" --k3s-arg "--no-deploy=servicelb@server:0"

	@helm repo add grafana https://grafana.github.io/helm-charts
	@helm repo add fluent https://fluent.github.io/helm-charts
	@helm repo update

	@kubectl wait node --for condition=ready --all --timeout=60s
	@sleep 5
	@kubectl wait pod -A --all --for condition=ready --timeout=60s

grafana-simple-demo:
	@helm upgrade --install tempo grafana/tempo
	@helm upgrade -f basic/single-binary-grafana-values.yaml --install grafana grafana/grafana
	@kubectl create -f basic/single-binary-extras.yaml

istio-tempo: 
	@istioctl install -y --set profile=demo -f istio/istio-operator.yaml
	# kubectl apply -f istio-operator.yaml

	@kubectl create ns tracing

    # Install Tempo
	@helm upgrade --install tempo grafana/tempo -n tracing --set tempo.receivers.zipkin.endpoint=0.0.0.0:9411

    # Install Loki
	@helm upgrade -f istio/loki-values.yaml --install loki grafana/loki-stack -n tracing
    # Wait for Loki to spawn up, sometimes fluentbit is deployed faster, and doesn't send logs to Loki
	-kubectl wait pod -n tracing --all --for condition=ready --timeout=60s
    # Install fluentbit
	@kubectl apply -f istio/fluentbit

	@kubectl label namespace default istio-injection=enabled --overwrite
    # Install a basic ngsa-app
	@kubectl apply -f istio/ngsa-memory/ngsa-mem-only.yaml

    # Install Grafna
	@helm upgrade -f istio/grafana-single-values.yaml --install grafana grafana/grafana -n tracing

install-mocks:
    # Install mock application chain for ngsa-app
    # Chain:    [ngsa-mock]-->[mock-a]-->[mock-b]--┐
    #                    └--->[mock-c]<---┘        |
    #                              └--->[mock-d]<--┘
	# Install root ngsa-app with mock and tracing
	@kubectl apply -f istio/ngsa-memory/ngsa-memory-with-mock.yaml

	# Install Other mock services
	@./istio/mock-rest/gen-mock.bash ngsa-a ngsa-mods ngsa-b | kubectl apply -f -
	@./istio/mock-rest/gen-mock.bash ngsa-b ngsa-mods ngsa-c ngsa-d | kubectl apply -f -
	@./istio/mock-rest/gen-mock.bash ngsa-c ngsa-mods ngsa-d | kubectl apply -f -
	@./istio/mock-rest/gen-mock.bash ngsa-d ngsa-mods | kubectl apply -f -

build-ngsa:
	# Build and push modified ngsa-app
	@docker build -t localhost:5000/ngsa-app:local -f ngsa-app/Dockerfile ./ngsa-app
	@docker push localhost:5000/ngsa-app:local
