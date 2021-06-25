#!/bin/sh

rustup component add rust-analysis
rustup component add rust-src
rustup component add rls

# copy grafana.db to /grafana
sudo mkdir -p /grafana
sudo  cp deploy/grafanadata/grafana.db /grafana
sudo  chown -R 472:472 /grafana
