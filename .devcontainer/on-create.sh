#!/bin/sh

# install wasm-pack
curl https://rustwasm.github.io/wasm-pack/installer/init.sh -sSf | sh

# install node
curl -sL https://deb.nodesource.com/setup_14.x | sudo -E bash -
sudo apt-get update && sudo apt install -y nodejs

