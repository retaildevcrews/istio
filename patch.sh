#!/bin/sh

IP=$(kubectl get po -l istio=ingressgateway -n istio-system -o jsonpath='{.items[0].status.hostIP}')
PORT=$(kubectl -n istio-system get service istio-ingressgateway -o jsonpath='{.spec.ports[?(@.name=="http2")].nodePort}')

# update IP and port
sed -i -r "s/address: .+/address: $IP/g" deploy/filter.yml
sed -i -r "s/port_value: .+/port_value: $PORT/g" deploy/filter.yml
sed -i -r "s/\"service_authority\": .+/\"service_authority\": \"$IP\",/g" deploy/filter.yml

# remove existing exports
sed -i -r "/export INGRESS_HOST=.+/d" ~/.bashrc
sed -i -r "/export INGRESS_PORT=.+/d" ~/.bashrc
sed -i -r "/export SECURE_INGRESS_PORT=.+/d" ~/.bashrc
sed -i -r "/export GATEWAY_URL=.+/d" ~/.bashrc

# add exports to .bashrc
echo "export INGRESS_HOST=$IP" >> ~/.bashrc
echo "export INGRESS_PORT=$PORT" >> ~/.bashrc
echo "export SECURE_INGRESS_PORT=$(kubectl -n istio-system get service istio-ingressgateway -o jsonpath='{.spec.ports[?(@.name=="https")].nodePort}')" >> ~/.bashrc
echo 'export GATEWAY_URL=$INGRESS_HOST:$INGRESS_PORT' >> ~/.bashrc

kubectl delete --ignore-not-found -n default cm wasm-poc-filter
kubectl create cm -n default wasm-poc-filter --from-file=wasm_header_poc.wasm
kubectl patch deployment -n default ngsa -p '{"spec":{"template":{"metadata":{"annotations":{"sidecar.istio.io/userVolume":"[{\"name\":\"wasmfilters-dir\",\"configMap\": {\"name\": \"wasm-poc-filter\"}}]","sidecar.istio.io/userVolumeMount":"[{\"mountPath\":\"/var/local/lib/wasm-filters\",\"name\":\"wasmfilters-dir\"}]"}}}}}'

# force ngsa redeploy
kubectl scale deployments/ngsa --replicas=0
kubectl scale deployments/ngsa --replicas=1

kubectl apply -f deploy/filter.yml

kubectl wait pod --for condition=ready --all --timeout=60s

echo "to load env vars"
echo "    source ~/.bashrc"
echo "run - make test-all"
