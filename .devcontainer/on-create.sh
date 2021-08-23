#!/bin/sh

echo "on-create started" >> $HOME/status

docker network create kind

# create local registry
docker run -d --net kind --restart=always -p "127.0.0.1:5000:5000" --name kind-registry registry:2

cd clusteradm
make recreate

cd ..
make deploy

echo "on-create completed" > $HOME/status
