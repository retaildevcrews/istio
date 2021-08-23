.PHONY: delete recreate

help :
	@echo "Usage:"
	@echo "   make recreate      - delete and recreate a new kind cluster"
	@echo "   make delete        - delete the kind cluster"

delete :
	# delete the cluster (if exists)
	@# this will fail harmlessly if the cluster does not exist
	-kind delete cluster

recreate : delete
	# create kind cluster
	@kind create cluster --config ../deploy/kind/kind.yaml
	@kubectl apply -f ../deploy/kind/config.yaml

	# build the WebAssembly
	rm -f ../burst_header.wasm
	cd .. && cargo build --release --target=wasm32-unknown-unknown
	cd .. && cp target/wasm32-unknown-unknown/release/burst_header.wasm .

	# build burst service
	docker build ../burst -t localhost:5000/burst:local
	docker push localhost:5000/burst:local

	# wait for kind node to be ready
	@kubectl wait node --for condition=ready --all --timeout=60s

	# install istio
	@/usr/local/istio/bin/istioctl install --set profile=demo -y
	@kubectl label namespace default istio-injection=enabled --overwrite

	# deploy metrics server
	@kubectl apply -f ../deploy/metrics

	# add config map
	@kubectl create cm burst-wasm-filter --from-file=../burst_header.wasm

	# deploy apps
	@kubectl apply -f ../burst/deploy
	@kubectl apply -f ../deploy/ngsa-memory/ngsa-memory.yaml
	@kubectl apply -f ../deploy/ngsa-memory/ngsa-gw.yaml

	# create HPA for ngsa deployment for testing
	@kubectl autoscale deployment ngsa --cpu-percent=40 --min=1 --max=2

	# Patching Istio ...
	@./patch.sh

	# exit this shell and start a new bash shell
