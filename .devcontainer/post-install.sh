#!/bin/sh

# copy grafana.db to /grafana
sudo mkdir -p /grafana
sudo  cp deploy/grafanadata/grafana.db /grafana
sudo  chown -R 472:472 /grafana

# start ngsa-app on port 4120
docker run -d -p 4120:4120 --name ngsa --restart always ghcr.io/retaildevcrews/ngsa-app:beta --in-memory --prometheus --port 4120 --burst-header --burst-service ngsa
