#!/bin/sh

echo "on-create started" >> $HOME/status

# Create Docker Network for k3d
docker network create k3d

# Create local container registry
k3d registry create registry.localhost --port 5000

# Connect to local registry
docker network connect k3d k3d-registry.localhost

cd clusteradm
make recreate

cd ..
make deploy

echo "on-create completed" > $HOME/status
