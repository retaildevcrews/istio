#!/bin/sh

sudo apt-get install -y python

# install cargo debug
cargo install cargo-debug

rustup update
rustup target add wasm32-unknown-unknown

curl -sL https://run.solo.io/wasme/install | sh
echo 'export PATH=$HOME/.wasme/bin:$PATH' >> $HOME/.bashrc

# copy grafana.db to /grafana
sudo mkdir -p /grafana
sudo  cp deploy/grafanadata/grafana.db /grafana
sudo  chown -R 472:472 /grafana

# install webv
dotnet tool install -g webvalidate
