.PHONY: deploy check-direct check-ingress clean build

help :
	@echo "Usage:"
	@echo "  make build               - build the plug-in"
	@echo "  make deploy              - apply wasm filter to istio ingress"
	@echo "  make check               - check the endpoints with curl"
	@echo "  make clean               - remove the istio plugin from ngsa"

build:
	# build the WebAssembly
	@rm -f ./burst_header.wasm
	@cargo build --manifest-path=../../Cargo.toml --release --target=wasm32-unknown-unknown
	@cp ../../target/wasm32-unknown-unknown/release/burst_header.wasm .

deploy: clean build
	# add wasm binary as config map 
	@kubectl create cm burst-wasm-filter -n istio-system --from-file=burst_header.wasm

	# Patch istio-ingressgateway, will create a new deployment terminating the old one
	@kubectl patch deployment istio-ingressgateway -n istio-system --patch-file "./istio-ingress/ingress-patch.yaml"

	# apply wasm filter to istio-ingress
	@kubectl apply -f ./istio-ingress/ingress-filter.yaml

clean :
	# Remove patches from istio-ingressgateway
	-@kubectl get deploy -n istio-system istio-ingressgateway -o json | jq '.spec.template.spec.containers[0].volumeMounts | map(.name == "wasmfilters-dir") | index(true)' | xargs -I % kubectl patch deployment istio-ingressgateway -n istio-system --type=json -p "[{'op': 'remove', 'path': '/spec/template/spec/containers/0/volumeMounts/%'}]"

	-@kubectl get deploy -n istio-system istio-ingressgateway -o json | jq '.spec.template.spec.volumes | map(.name == "wasmfilters-dir") | index(true)' | xargs -I % kubectl patch deployment istio-ingressgateway -n istio-system --type=json -p "[{'op': 'remove', 'path': '/spec/template/spec/volumes/%'}]"

	# delete filter and config map
	@kubectl delete --ignore-not-found -f ./istio-ingress/ingress-filter.yaml
	@kubectl delete --ignore-not-found cm -n istio-system burst-wasm-filter

check-direct :
	# calling healthz service endpoint directly won't show the burst header
	# Since we're not going through the istio ingress
	@http http://localhost:30080/healthz

check-ingress :
	# But calling istio ingress endpoint will show the burst header
	http $(shell kubectl get po -l istio=ingressgateway -n istio-system -o jsonpath='{.items[0].status.hostIP}'):$(shell kubectl -n istio-system get service istio-ingressgateway -o jsonpath='{.spec.ports[?(@.name=="http2")].nodePort}')/memory/healthz
