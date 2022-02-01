.PHONY: build deploy check check-metrics check-istio test clean

help :
	@echo "Usage:"
	@echo "   make build-wasm          - build the wasm plug-in"
	@echo "   make build-docker        - build the wasm Dockerfile"
	@echo "   make deploy              - deploy the istio plugin to ngsa"
	@echo "   make check               - check the endpoints with curl"
	@echo "   make check-istio         - check istio status and logs"
	@echo "   make check-metrics       - check the raw pod metrics"

check : 
	$(MAKE) check -C ../../

check-istio : 
	$(MAKE) check-istio -C ../../

check-metrics : 
	$(MAKE) check-metrics -C ../../

build-wasm :
	# Goto REPO root and build the WebAssembly
	$(MAKE) build -C ../../
	# Copy burst_header.wasm here so that docker context is less expensive
	@cp ../../burst_header.wasm .

docker-build : build-wasm
	# Build Container Image
	@docker build -f Dockerfile -t localhost:5000/burst-wasm:local .
	@docker push localhost:5000/burst-wasm:local

deploy:
	# Deploy the filter if not already deployed
	@kubectl apply -f ../../deploy/ngsa-memory/filter.yaml
	# Then deploy NGSA app with PV
	@kubectl apply -f ./ngsa-memory-init-wasm.yaml
