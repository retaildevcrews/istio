.PHONY: build run

help :
	@echo "Usage:"
	@echo "   make build            - build Istio plug-in"
	@echo "   make run              - run Istio with plug-in via Docker"

build:
	cargo build --target wasm32-unknown-unknown --release
	cp target/wasm32-unknown-unknown/release/hello_world.wasm ./
	wasme build precompiled hello_world.wasm --tag hello_world:v0.1
	rm hello_world.wasm

run: build
	wasme deploy envoy hello_world:v0.1 --envoy-image=istio/proxyv2:1.5.1 --bootstrap=envoy-bootstrap.yml
