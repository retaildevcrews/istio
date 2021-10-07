.PHONY: delete recreate help from-scratch cleanall network-delete network-recreate

help :
	@echo "Usage:"
	@echo "   make recreate      - delete and recreate a new k3d cluster"
	@echo "   make delete        - delete the k3d cluster"

delete :
	# delete the cluster (if exists)
	@# this will fail harmlessly if the cluster does not exist
	-k3d cluster delete

recreate : delete
	# build k3d cluster
	@k3d cluster create --registry-use k3d-registry.localhost:5000 --config ../deploy/k3d/k3d.yaml --k3s-server-arg "--no-deploy=traefik" --k3s-server-arg "--no-deploy=servicelb"

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

network-recreate: network-delete
	# Check if k3d network exists and create if doesn't exist
	@docker network inspect k3d > /dev/null 2>&1 || docker network create k3d

	# Create k3d docker registry if doesn't exist
	@k3d reg list | grep k3d-registry.localhost > /dev/null 2>&1 || k3d registry create registry.localhost --port 5000

	# Connect k3d registry to docker
	@docker network connect k3d k3d-registry.localhost

network-delete:
	# Disconnect from k3d registry network
	-@docker network disconnect k3d k3d-registry.localhost

	# Delete k3d registry
	-@k3d reg delete k3d-registry.localhost

deleteall: delete network-delete

from-scratch: deleteall network-recreate recreate
