#!/bin/sh

IP=$(kubectl get po -l istio=ingressgateway -n istio-system -o jsonpath='{.items[0].status.hostIP}')
PORT=$(kubectl -n istio-system get service istio-ingressgateway -o jsonpath='{.spec.ports[?(@.name=="http2")].nodePort}')

# update IP and port
sed -i -r "s/address: .+/address: $IP/g" deploy/ngsa-memory/filter.yaml
sed -i -r "s/port_value: .+/port_value: $PORT/g" deploy/ngsa-memory/filter.yaml
sed -i -r "s/\"service_authority\": .+/\"service_authority\": \"$IP\",/g" deploy/ngsa-memory/filter.yaml

# remove existing exports
sed -i -r "/export K8s=.+/d" ~/.bashrc

# add exports to .bashrc
echo "export K8s=$IP:$PORT" >> ~/.bashrc

export K8s=$IP:$PORT
