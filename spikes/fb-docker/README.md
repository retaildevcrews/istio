# Observability with Docker

Here we will run NGSA application and Fluentbit to showcase minimal observability without K8s.

## Setting up

### Baremetal app and fluentbit

Assuming current working directory is `<REPO_ROOT>/spikes/fb-docker`.

```bash
# Emulate local ngsa applicaion log parsing with docker run and redirection
# Redirect to a file named output.log
docker run -it -p 8080:8080 --rm --name ngsa ghcr.io/retaildevcrews/ngsa-app:beta --in-memory >| output.log

# Change Path parameter on Line 18 under [INPUT] in the cconfig/cfg_baremetal.conf
# To point to output.log file above
# In another terminal run fluentbit
/opt/fluent-bit/bin/fluent-bit -c ./config/cfg_baremetal.conf

## Run some load with webv in another terminal... (3 TERMINALS!!!)
webv -s localhost:8080 -f /workspaces/istio/deploy/loderunner/benchmark.json -l 2000 -r -v
```

### App in Docker and fluentbit in baremetal

The steps are the same as above, except we will change the Path parameter under `[INPUT]` to docker log path.

See [cfg_baremetal.conf](./config/cfg_baremetal.conf) for example.

### App and Fluentbit running in docker

```bash

# Run app in docker (notice the detached state)
docker run -itd -p 8080:8080 --rm --name ngsa ghcr.io/retaildevcrews/ngsa-app:beta --in-memory

# Run fluentbit in docker mounting the log directory
docker run -it --rm -v $(pwd)/config/cfg_mount.conf:/fluent-bit/etc/fluent-bit.conf:ro \
        -v /var/lib/docker/containers:/var/lib/docker/containers:ro \
        --name fb fluent/fluent-bit:1.8-debug

## In another terminal Run WebV 
webv -s localhost:8080 -f /workspaces/istio/deploy/loderunner/benchmark.json -l 2000 -r -v
```

### App and Fluentbit running in docker (without mount)

```bash

# For this we need to first run FluentBit
# Use detached more or use different terminal
docker run -itd -v $(pwd)/config/cfg_driver.conf:/fluent-bit/etc/fluent-bit.conf:ro --rm -p 24224:24244 -p 24224:24224/udp fluent/fluent-bit:1.8-debug

# Now run NGSA application
# Notice the change in docker log driver `fluentd`
docker run -it -p 8080:8080 --log-driver=fluentd --log-opt fluentd-address=tcp://localhost:24224 \
        --rm --name ngsa ghcr.io/retaildevcrews/ngsa-app:beta --in-memory

## In another terminal Run WebV
webv -s localhost:8080 -f /workspaces/istio/deploy/loderunner/benchmark.json -l 2000 -r -v
```

## Log Analytics Output

Optionally to outout to Log Analytics, enable Log Analytics output block of
each of the corresponding fluentbit config file.

Two ENV variables is required to use Log Analytics output block.

- `SHARED_KEY`
- `WORKSPACE_ID`

Since FB depends on these two vars to populate secrets, make sure to export them if using baremetal.

If using FB in Docker, pass them as ENV var.

Example:

> docker run -it -v $(pwd)/config/cfg_driver.conf:/fluent-bit/etc/fluent-bit.conf:ro **-e WORKSPACE_ID="$WORKSPACE_ID" -e SHARED_KEY="$SHARED_KEY"** fluent/fluent-bit:1.8-debu`g
