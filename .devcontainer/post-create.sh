#!/bin/sh

echo "post-create started" > $HOME/status

# install istio
istioctl install --set profile=demo -y
kubectl label namespace default istio-injection=enabled --overwrite
k get ns > ~/istio.log

# deploy apps
#kubectl apply -f deploy/burst
#kubectl apply -f deploy/ngsa-memory/ngsa-memory.yaml
#kubectl apply -f deploy/ngsa-memory/ngsa-gw.yaml

# create HPA for ngsa deployment for testing
#kubectl autoscale deployment ngsa --cpu-percent=50 --min=1 --max=2

#kubectl wait pod --for condition=ready --all --timeout=60s

# Patching Istio ...
#./patch.sh >> ~/app.log

echo "post-create completed" > $HOME/status
