.PHONY: deploy check-direct check-ingress clean build

help :
	@echo "Usage:"
	@echo "  make build               - build the plug-in"
	@echo "  make deploy              - apply wasm filter to istio ingress"
	@echo "  make check               - check the burst with http"
	@echo "  make clean               - remove the istio plugin from ngsa"

build:
	# build the WebAssembly
	-@rm -f ./burst_header.wasm
	@cargo build --manifest-path=./wasm-filter-mod-src/Cargo.toml --release --target=wasm32-wasi
	@cp ./wasm-filter-mod-src/target/wasm32-wasi/release/burst_header.wasm .

deploy : install-rust-target clean build

	# add config map
	@kubectl create cm burst-wasm-filter-spiked --from-file=burst_header.wasm

	# Apply ngsa yaml (embedded sidecar configuration)
	@kubectl apply -f ./yamls/ngsa-mem-sidecar.yaml

	# turn the wasm filter on
	@kubectl apply -f ./yamls/filter.yaml

install-rust-target :
	# Check and install wasm32-wasi target
	@rustup target list | grep '(installed)' | grep -i wasi && echo " " || rustup target add wasm32-wasi

check :
	# curl the healthz endpoint
	@curl -i http://localhost:30080/healthz

	# check the healthz endpoint with http
	# this will show the burst header if enabled
	@http http://localhost:30080/healthz

clean :
	@kubectl delete --ignore-not-found -f ./yamls/ngsa-mem-sidecar.yaml
	@kubectl delete --ignore-not-found -f ./yamls/filter.yaml
	@kubectl delete --ignore-not-found cm burst-wasm-filter-spiked

check-istio :
	@istioctl proxy-status
	@echo ""
	@kubectl logs -l=app=ngsa -c istio-proxy

clean-old :
	# Remove existing ngsa deployment
	@cd ../../ && make clean
	@kubectl delete --ignore-not-found cm burst-wasm-filter
	@kubectl delete --ignore-not-found -f ../../deploy/ngsa-memory/ngsa-memory.yaml
	@kubectl delete --ignore-not-found -f ../../deploy/ngsa-memory/filter.yaml
