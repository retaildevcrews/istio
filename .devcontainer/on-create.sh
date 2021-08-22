#!/bin/sh

echo "on-create started" > $HOME/status

# copy grafana.db to /grafana
sudo mkdir -p /grafana
sudo  cp deploy/grafanadata/grafana.db /grafana
sudo  chown -R 472:0 /grafana

docker network create kind

# create local registry
docker run -d --net kind --restart=always -p "127.0.0.1:5000:5000" --name kind-registry registry:2

# create kind cluster
kind create cluster --config deploy/kind/kind.yaml
kubectl apply -f deploy/kind/config.yaml

# build burst service
docker build burst -t localhost:5000/burst:local
docker push localhost:5000/burst:local

# build the WebAssembly
rm -f burst_header.wasm
cargo build --release --target=wasm32-unknown-unknown
cp target/wasm32-unknown-unknown/release/burst_header.wasm .

# wait for kind node to be ready
kubectl wait node --for condition=ready --all --timeout=60s

# install istio
istioctl install --set profile=demo -y
kubectl label namespace default istio-injection=enabled

# deploy apps
kubectl apply -f deploy/burst
kubectl apply -f deploy/ngsa-memory/ngsa-memory.yaml
kubectl apply -f deploy/ngsa-memory/ngsa-gw.yaml

# deploy metrics server
kubectl apply -f deploy/metrics

# create HPA for ngsa deployment for testing
kubectl autoscale deployment ngsa --cpu-percent=50 --min=1 --max=2

kubectl wait pod --for condition=ready --all --timeout=60s

# Patching Istio ...
./patch.sh

# add config map
kubectl create cm burst-wasm-filter --from-file=burst_header.wasm

echo "on-create completed" > $HOME/status
