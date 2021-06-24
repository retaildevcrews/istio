#!/bin/sh
export ISTIO_VERSION=1.10.1
export ISTIO_HOME=/usr/local/istio

sudo apt-get install -y python cmake pkg-config libssl-dev

# install cargo debug
cargo install cargo-debug

rustup update
rustup target add wasm32-unknown-unknown

curl -sL https://run.solo.io/wasme/install | sh
echo "export ISTIO_VERSION=$ISTIO_VERSION" >> $HOME/.bashrc
echo "export ISTIO_HOME=$ISTIO_HOME" >> $HOME/.bashrc
echo 'export PATH=$HOME/.wasme/bin:$ISTIO_HOME/bin:$PATH' >> $HOME/.bashrc

# copy grafana.db to /grafana
sudo mkdir -p /grafana
sudo  cp deploy/grafanadata/grafana.db /grafana
sudo  chown -R 472:472 /grafana

# install webv
dotnet tool install -g webvalidate

#install istioctl
pushd $HOME
curl -L https://istio.io/downloadIstio | sh -
sudo mv istio-$ISTIO_VERSION $ISTIO_HOME
popd
