rm -f wasm_header_poc.wasm
cp target/wasm32-unknown-unknown/release/wasm_header_poc.wasm .
kubectl delete --ignore-not-found -n default cm wasm-poc-filter
kubectl create cm -n default wasm-poc-filter --from-file=wasm_header_poc.wasm
kubectl patch deployment -n default ngsa -p '{"spec":{"template":{"metadata":{"annotations":{"sidecar.istio.io/userVolume":"[{\"name\":\"wasmfilters-dir\",\"configMap\": {\"name\": \"wasm-poc-filter\"}}]","sidecar.istio.io/userVolumeMount":"[{\"mountPath\":\"/var/local/lib/wasm-filters\",\"name\":\"wasmfilters-dir\"}]"}}}}}'
kubectl scale deployments/ngsa --replicas=0
kubectl scale deployments/ngsa --replicas=1