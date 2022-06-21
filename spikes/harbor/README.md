
# Deploy Harbor

## Azure ASB Cluster

> The steps below are guidelines for the dev cluster, but is replicable for the pre-prod as well

- Create a dns entry for harbor (*harbor-core-westus2-dev.cse.ms*) `rg-ngsa-asb-dev/cse.ms` private dns and put the Load-Balancer IP address for Westus2.
- Create a public dns entry `harbor-core-westus2-dev.cse.ms` in `dns-rg/cse.ms` and put the public ip for WestUS2 dev cluster
- Add below settings for harbor (similar to ngsa apps) in Application Gateway:
  - Backend pool
  - Backend settings (with no custom probes, that we don't have to configure probes)
  - Listeners (http and https)
  - Rules (http and https)

At this point Harbor should be ready to deploy.
Now we need to make sure our cluste can pull the container images.
To do that we have two options:

- We can push Harbor images into our cluster acr repo (`rg-ngsa-asb-dev/acraks3i2qzkkxofr7c`)
    > This is easier and preferable for pre-prod and dev clusters
- Or we can use another private repo and make sure our cluster can access the repo
    > This requires several extra steps and should be done for SPIKEs only

For deploying using another private repo couple of things to keep in mind:

- Add the ACR's address in `rg-ngsa-asb-dev-hub/fw-policies-eastus` allow rule collection.
- Add managed identity for the Westus2 cluster (e.g `aks-3i2qzkkxofr7c-westus2-agentpool`) to the ACR and give it `AcrPull` permissions
- Deploy helm in `azure-arc` repo, since its in the policy exception list.

Now that all of the setup is done, to deploy:

```bash
# Add harbor helm repo and update
helm repo add harbor https://helm.goharbor.io
helm repo update

# From this spike/harbor directory
## For the spike we're deploying in azure-arc directory
## but if you have access to push images to the private repo, you can deploy to any namespace
helm istall -f helm-values.yaml harbor harbor/harbor -n azure-arc --create-namespace

# Be sure to change the namespace in harbor-virtual-svc.yaml file
kuebctl apply -f harbor-virtual-svc.yaml

```

> Default user for harbor portal is admin and password is `Harbor12345`.
>
> This can be changed in `helm-values.yaml` file.

## Locally or in a VM with Docker

For deploying harbor locally we need to have these tools available:

- Bash
- Docker
- Docker Compose
- [Optional] Login to Dockerhub (to pull images)

### Steps

Follow the steps below (based on https://goharbor.io/docs/1.10/install-config/):

1. Download the **online** installer `wget https://github.com/goharbor/harbor/releases/download/{VERSION}/harbor-online-installer-{VERSION}.tar.gz` file from [harbor github release page](https://github.com/goharbor/harbor/releases)
    > Notes: Download the online installer: e.g wget https://github.com/goharbor/harbor/releases/download/v2.5.1/harbor-online-installer-v2.5.1.tgz

1. Extract the archive (assuming the extracted path is `$HARBOR_PATH`)

1. Copy `harbor.yml` to `$HARBOR_PATH` (so that `harbor.yml` will be in the same directory as `install.sh`)

1. Generate certificate for HTTPS access.

    ```bash
    # Here replace $HARBOR_PATH with the extracted dir path
    ./gen-multi-domain-certs.bash --cert-path ${HARBOR_PATH} --cert-prefix harbor-ssl -san 127.0.0.1,localhost,harboar.core.local,harbor.notary.local,harboar.local
    ```

1. Change `certificate` and `private_key` entry in `harbor.yml` file and point to `$HARBOR_PATH/harbor-ssl.crt` and `$HARBOR_PATH/harbor-ssl.key`.

    > *Note:* Use full path for `certificate` and `private_key`

1. Run the installer

    ```bash
    # Here replace $HARBOR_PATH with the extracted dir path
    cd $HARBOR_PATH
    sudo ./install.sh
    ```

1. Try `docker ps` and try the ports for `harbor-nginx` and `harbor-portal`.
