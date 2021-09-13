.PHONY: build deploy check check-metrics check-istio test clean

help :
	@echo "Usage:"
	@echo "   make build               - build the plug-in"
	@echo "   make deploy              - deploy the istio plugin to ngsa"
	@echo "   make check               - check the endpoints with curl"
	@echo "   make check-istio         - check istio status and logs"
	@echo "   make check-metrics       - check the raw pod metrics"
	@echo "   make test                - run a LodeRunner test"
	@echo "   make clean               - remove the istio plugin from ngsa"

build :
	# build the WebAssembly
	@rm -f burst_header.wasm
	@cargo build --release --target=wasm32-unknown-unknown
	@cp target/wasm32-unknown-unknown/release/burst_header.wasm .

deploy : clean build
    # For POC.
	# add config map with rust-wasm binary as its content 
	@kubectl create cm burst-wasm-filter -n istio-system --from-file=burst_header.wasm

	# Patch istio-ingressgateway, will create a new deployment terminating the old one
	@kubectl patch deployment istio-ingressgateway -n istio-system -p "$(cat deploy/istio-ingress/ingress-patch.yaml)"

	# apply wasm filter to istio-ingress
	@kubectl apply -f deploy/istio-ingress/ingress-filter.yaml

check :
	# curl the healthz endpoint
	@curl -i http://localhost:30080/healthz

	# check the healthz endpoint with http
	# this will show the burst header if enabled
	@http http://localhost:30080/healthz

clean :
	# Remove patches from istio-ingressgateway
	-@kubectl get deploy -n istio-system istio-ingressgateway -o json | jq '.spec.template.spec.containers[0].volumeMounts | map(.name == "wasmfilters-dir") | index(true)' | xargs -I % kubectl patch deployment istio-ingressgateway -n istio-system --type=json -p "[{'op': 'remove', 'path': '/spec/template/spec/containers/0/volumeMounts/%'}]"

	-@kubectl get deploy -n istio-system istio-ingressgateway -o json | jq '.spec.template.spec.volumes | map(.name == "wasmfilters-dir") | index(true)' | xargs -I % kubectl patch deployment istio-ingressgateway -n istio-system --type=json -p "[{'op': 'remove', 'path': '/spec/template/spec/volumes/%'}]"

	# delete filter and config map
	@kubectl delete --ignore-not-found -f deploy/istio-ingress/ingress-filter.yaml
	@kubectl delete --ignore-not-found cm -n istio-system burst-wasm-filter

test :
	# run a 60 second test
	@cd deploy/loderunner && webv -s http://localhost:30080/ -f benchmark.json -r -l 5 --duration 60

check-metrics :
	# retrieve current values from metrics server
	kubectl get --raw https://localhost:5443/apis/metrics.k8s.io/v1beta1/pods

check-istio :
	@istioctl proxy-status
	@echo ""
	@kubectl logs -l=app=ngsa -c istio-proxy
