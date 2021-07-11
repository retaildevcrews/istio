#!/bin/sh

# copy grafana.db to /grafana
sudo mkdir -p /grafana
sudo  cp deploy/grafanadata/grafana.db /grafana
sudo  chown -R 472:472 /grafana

# create local registry
docker run -d --restart=always -p "5000:5000" --name kind-registry registry:2

# pull docker image
docker pull ghcr.io/retaildevcrews/ngsa-app:beta
docker tag ghcr.io/retaildevcrews/ngsa-app:beta localhost:5000/retaildevcrews/ngsa-app:beta
docker push localhost:5000/retaildevcrews/ngsa-app:beta

# build pymetric
docker build pymetric -t pymetric:local
docker tag  pymetric:local localhost:5000/pymetric:local
docker push localhost:5000/pymetric:local
