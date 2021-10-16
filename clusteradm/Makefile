.PHONY: delete recreate help

help :
	@echo "Usage:"
	@echo "   make help          - prints this msg"
	@echo "   make all           - delete and recreate a new k3d cluster"
	@echo "   make delete        - delete k3d cluster"

delete :
	# delete the cluster (if exists)
	@# this will fail harmlessly if the cluster does not exist
	-k3d cluster delete ngsa-k3d-cluster

all : delete
	# build k3d cluster
	@k3d cluster create ngsa-k3d-cluster --registry-use k3d-registry.localhost:5000 --config ../deploy/k3d/k3d.yaml --k3s-arg "--no-deploy=traefik@server:0" --k3s-arg "--no-deploy=servicelb@server:0"
	 																							                                             

	# build the WebAssembly
	rm -f ../burst_header.wasm
	cd .. && cargo build --release --target=wasm32-unknown-unknown
	cd .. && cp target/wasm32-unknown-unknown/release/burst_header.wasm .

	# build burst service
	docker build ../burst -t localhost:5000/burst:local
	docker push localhost:5000/burst:local

	# wait for cluster to be ready
	@kubectl wait node --for condition=ready --all --timeout=60s
	@sleep 5
	@kubectl wait pod -A --all --for condition=ready --timeout=60s

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

