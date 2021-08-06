#!/bin/sh

cargo update
rustup self update
rustup update

# install wasm-pack
curl https://rustwasm.github.io/wasm-pack/installer/init.sh -sSf | sh
cargo install wasm-bindgen-cli 

# install node
curl -sL https://deb.nodesource.com/setup_14.x | sudo -E bash -
sudo apt-get update && sudo apt install -y nodejs

