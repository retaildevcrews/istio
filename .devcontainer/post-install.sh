#!/bin/sh

# install rust
#curl https://sh.rustup.rs > rust.sh
#chmod +x rust.sh
#./rust.sh -y
#rm rust.sh
$HOME/.cargo/bin/cargo install cargo-debug
echo 'export PATH="/usr/local/cargo/bin:$PATH"' >> $HOME/.bashrc

# copy grafana.db to /grafana
sudo mkdir -p /grafana
sudo  cp deploy/grafanadata/grafana.db /grafana
sudo  chown -R 472:472 /grafana

# install webv
dotnet tool install -g webvalidate --version 2.0.0-beta2
