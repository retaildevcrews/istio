#!/bin/sh

# copy grafana.db to /grafana
sudo mkdir -p /grafana
sudo  cp deploy/grafanadata/grafana.db /grafana
sudo  chown -R 472:472 /grafana

docker network create kind

# create local registry
docker run -d --net kind --restart=always -p "127.0.0.1:5000:5000" --name kind-registry registry:2

# build pymetric
docker build pymetric -t localhost:5000/pymetric:local
docker push localhost:5000/pymetric:local
