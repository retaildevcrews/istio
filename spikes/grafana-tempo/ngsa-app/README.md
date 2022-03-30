# NGSA App

NGSA App is inteneded for platform testing and monitoring in one or many Kubernetes clusters and/or cloud deployments.

## Prerequisites

- Bash shell (tested on Visual Studio Codespaces, Mac, Ubuntu, Windows with WSL2)
  - Will not work with WSL1 or Cloud Shell
- Azure CLI ([download](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli?view=azure-cli-latest))
- Docker CLI ([download](https://docs.docker.com/install/))
- .NET 5.0 ([download](https://docs.microsoft.com/en-us/dotnet/core/install/))
- Visual Studio Code (optional) ([download](https://code.visualstudio.com/download))

## Ngsa-app Usage

```text

Usage:
  Ngsa.Application [options]
  
Options:
  -a, --app-type <App|WebAPI>                                                      Application Type [default: App]
  -p, --prometheus                                                                 Send metrics to Prometheus [default: False]
  -m, --in-memory                                                                  Use in-memory database [default: False]
  -n, --no-cache                                                                   Don't cache results [default: False]
  --url-prefix <url-prefix>                                                        URL prefix for ingress mapping [default: ]
  --port <port>                                                                    Listen Port [default: 8080]
  -d, --cache-duration <cache-duration>                                            Cache for duration (seconds) [default: 300]
  --burst-header                                                                   Enable burst metrics header in health and version endpoints. [default: False]
  --burst-service-endpoint <burst-service-endpoint>                                Burst metrics service endpoint
  --burst-service-ns <burst-service-ns>                                            Namespace parameter for burst metrics service
  --burst-service-hpa <burst-service-hpa>                                          HPA name parameter for burst metrics service
  --retries <retries>                                                              Cosmos 429 retries [default: 10]
  --timeout <timeout>                                                              Request timeout [default: 10]
  -s, --data-service <data-service>                                                Data Service URL [default: ]
  -v, --secrets-volume <secrets-volume>                                            Secrets Volume Path [default: secrets]
  -z, --zone <zone>                                                                Zone for log [default: dev]
  -r, --region <region>                                                            Region for log [default: dev]
  -l, --log-level <Critical|Debug|Error|Information|None|Trace|Warning>            Log Level [default: Error]
  -q, --request-log-level <Critical|Debug|Error|Information|None|Trace|Warning>    Request Log Level [default: Information]
  --dry-run                                                                        Validates configuration
  --version                                                                        Show version information
  -?, -h, --help                                                                   Show help and usage information

```

## Run the Application

### Using Visual Studio Codespaces

> Visual Studio Codespaces is the easiest way to evaluate ngsa.

### Using bash shell

> This will work from a terminal in Visual Studio Codespaces as well

1. Clone the repo
    >`git clone https://github.com/retaildevcrews/ngsa-app.git`
2. Change to the app root directory
    >`cd ngsa-app`
3. Run the application in memory mode

  Running the application in memory mode allows us to run the application without setting up the rest of the supporting infrastructure.

```bash

# run the application
dotnet run -- -m

```

 You should see the following response:
 > Hosting environment: Production
Content root path: /mnt/c/Users/t-anbassey/Source/ngsa-app
Now listening on: http://[::]:8080
Application started. Press Ctrl+C to shut down.

### Testing the application

Open a new bash shell

> Visual Studio Codespaces allows you to open multiple shells by clicking on the `Split Terminal` icon

```bash

# test the application

# test using httpie (installed automatically in Codespaces)
http localhost:4120/version

# test using curl
curl localhost:4120/version

```

Stop ngsa by typing Ctrl-C or the stop button if run via F5

### Run the app with burst service

```bash

# Option 1: set burst service env variables (recommended)
export BURST_SERVICE_ENDPOINT="http://localhost:4120/burstmetrics/"
export BURST_SERVICE_NS="default"
export BURST_SERVICE_HPA="ngsa"

dotnet run -- -m --burst-header

# Option 2: pass in burst variables as args
dotnet run -- -m --burst-header --burst-service-endpoint="http://localhost:4120/burstmetrics/" --burst-service-ns default --burst-service-hpa ngsa

```

If the env variables are set AND you pass in the burst env variables as args, the CLI argument values take precedence.

## Contributing

This project welcomes contributions and suggestions. Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit [Microsoft Contributor License Agreement](https://cla.opensource.microsoft.com).

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
