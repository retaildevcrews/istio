#!/bin/sh

echo "on-create started" > $HOME/status

docker network create kind

# create local registry
docker run -d --net kind --restart=always -p "127.0.0.1:5000:5000" --name kind-registry registry:2

# create kind cluster
kind create cluster --config deploy/kind/kind.yaml
kubectl apply -f deploy/kind/config.yaml

# build the WebAssembly
rm -f burst_header.wasm
cargo build --release --target=wasm32-unknown-unknown
cp target/wasm32-unknown-unknown/release/burst_header.wasm .

# build burst service
docker build burst -t localhost:5000/burst:local >> ~/burst.log
docker push localhost:5000/burst:local >> ~/burst.log

# wait for kind node to be ready
#kubectl wait node --for condition=ready --all --timeout=60s >> ~/wait.log

# deploy metrics server
kubectl apply -f deploy/metrics >> ~/metrics.log

# add config map
kubectl create cm burst-wasm-filter --from-file=burst_header.wasm >> ~/app.log

# install istio
istioctl install --set profile=demo -y
kubectl label namespace default istio-injection=enabled --overwrite
k get ns > ~/istio.log

echo "on-create completed" > $HOME/status
