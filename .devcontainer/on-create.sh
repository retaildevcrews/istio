#!/bin/sh

echo "on-create started" >> $HOME/status

# Install k3d > 5.0.1
k3d --version | grep -Eo '^k3d version v5...[1-9]$' > /dev/null 2>&1
if [ $? -ne 0 ]; then
    # Means we don't have proper k3d version
    # Install v5.0.1
    echo "Installing k3d v5.0.1"
    sudo wget https://github.com/rancher/k3d/releases/download/v5.0.1/k3d-linux-amd64 -O /usr/local/bin/k3d
    sudo chmod +x /usr/local/bin/k3d
fi

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
