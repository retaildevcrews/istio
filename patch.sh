#!/bin/sh

IP=$(kubectl get po -l istio=ingressgateway -n istio-system -o jsonpath='{.items[0].status.hostIP}')
PORT=$(kubectl -n istio-system get service istio-ingressgateway -o jsonpath='{.spec.ports[?(@.name=="http2")].nodePort}')

# update IP and port
sed -i -r "s/address: .+/address: $IP/g" deploy/ngsa-memory/filter.yaml
sed -i -r "s/port_value: .+/port_value: $PORT/g" deploy/ngsa-memory/filter.yaml
sed -i -r "s/\"service_authority\": .+/\"service_authority\": \"$IP\",/g" deploy/ngsa-memory/filter.yaml

sed -i -r "s/address: .+/address: $IP/g" deploy/mem1/filter.yaml
sed -i -r "s/port_value: .+/port_value: $PORT/g" deploy/mem1/filter.yaml
sed -i -r "s/\"service_authority\": .+/\"service_authority\": \"$IP\",/g" deploy/mem1/filter.yaml

sed -i -r "s/address: .+/address: $IP/g" deploy/mem2/filter.yaml
sed -i -r "s/port_value: .+/port_value: $PORT/g" deploy/mem2/filter.yaml
sed -i -r "s/\"service_authority\": .+/\"service_authority\": \"$IP\",/g" deploy/mem2/filter.yaml

sed -i -r "s/address: .+/address: $IP/g" deploy/mem3/filter.yaml
sed -i -r "s/port_value: .+/port_value: $PORT/g" deploy/mem3/filter.yaml
sed -i -r "s/\"service_authority\": .+/\"service_authority\": \"$IP\",/g" deploy/mem3/filter.yaml


# remove existing exports
sed -i -r "/export INGRESS_HOST=.+/d" ~/.bashrc
sed -i -r "/export INGRESS_PORT=.+/d" ~/.bashrc
sed -i -r "/export SECURE_INGRESS_PORT=.+/d" ~/.bashrc
sed -i -r "/export K8s=.+/d" ~/.bashrc

# add exports to .bashrc
#echo "export INGRESS_HOST=$IP" >> ~/.bashrc
#echo "export INGRESS_PORT=$PORT" >> ~/.bashrc
#echo "export SECURE_INGRESS_PORT=$(kubectl -n istio-system get service istio-ingressgateway -o jsonpath='{.spec.ports[?(@.name=="https")].nodePort}')" >> ~/.bashrc
#echo 'export K8s=$INGRESS_HOST:$INGRESS_PORT' >> ~/.bashrc

export K8s=$IP:$PORT

echo 'if kubectl get po > /dev/null 2>&1; then export K8s=$(kubectl get po -l istio=ingressgateway -n istio-system -o jsonpath='"'{.items[0].status.hostIP}'"'):$(kubectl get service istio-ingressgateway -n istio-system -o jsonpath='"'{.spec.ports[?(@.name==\"http2\")].nodePort}'"'); fi' >> ~/.bashrc
