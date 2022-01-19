# Observability

## Logs and Metrics

Both the in-memory and the CosmosDB connected versions of the NGSA application and the LodeRunner application achieve observability by emitting logs and exposing a Prometheus endpoint. In both cases the intent and expectation is that all logs will be stored in the same log store and all metrics will be stored in the same metrics store for all clusters and all applications.  This provides a convenient all-up and comparative view of the data being sent.  Additionally, for log entries, it provides a means to inspect that values for a single request flow using the Correlation Vector (CVector).

### Logs

Logs are emitted via `stdout` with a correlation vector that provides a means to look a single request flow when inspecting log data.  The logs are in a app specific JSON format shown in the following example:

```json
{
   "Date": "2021-03-31T04:13:10.8111822Z",
   "LogName": "Ngsa.RequestLog",
   "StatusCode": 200,
   "TTFB": 0.3,
   "Duration": 0.3,
   "Verb": "GET",
   "Path": "/api/movies/tt0473188",
   "Host": "ngsa-in-memory.ms-ngsa.svc.cluster.local:8080",
   "ClientIP": "11.16.171.149",
   "UserAgent": "l8r/0.3.0",
   "CVector": "iJxHfTgSfkSDeGFKnWA9tQ.0.0",
   "CVectorBase": "iJxHfTgSfkSDeGFKnWA9tQ",
   "Category": "Movies",
   "Subcategory": "Movies",
   "Mode": "Direct",
   "Zone": "azure",
   "Region": "eus2",
   "CosmosName": "in-memory"
}
```

Since logs use `stdout` they may be picked up by any collector.  In most cases, we have used Fluent Bit to retrieve logs and forward them to log storage systems such as an Azure Log Analytics Workspace and Splunk.

### Metrics

Metrics are provided via a Prometheus endpoint and may be exposed by both the application and LodeRunner.  The endpoint reports a number of key metrics that are used to graph the performance of the application (both modes: in-memory and cosmos) and LodeRunner as they are continually tested. Querying the Prometheus endpoint of the apps will result in a response similar to the following samples:

```log
...
NgsaAppSummary summaryNgsaAppSummary_sum{code="OK",cosmos="False",mode="Query",region="dev",zone="dev"} 235.98999999999995
NgsaAppSummary_count{code="OK",cosmos="False",mode="Query",region="dev",zone="dev"} 51
NgsaAppSummary{code="OK",cosmos="False",mode="Query",region="dev",zone="dev",quantile="0.9"} 7.19
...
# HELP dotnet_total_memory_bytes Total known allocated memory
#TYPE dotnet_total_memory_bytes gaugedotnet_total_memory_bytes 24461168
# HELP process_start_time_seconds Start time of the process since unix epoch in seconds.
# TYPE process_start_time_seconds gaugeprocess_start_time_seconds 1617167164.05
# HELP NgsaAppDuration Histogram of NGSA App request duration
# TYPE NgsaAppDuration histogram
NgsaAppDuration_sum{code="OK",cosmos="False",mode="Query",region="dev",zone="dev"} 235.98999999999995
NgsaAppDuration_count{code="OK",cosmos="False",mode="Query",region="dev",zone="dev"} 
...
```

The result is a consistent set of metrics that can be used to get an idea of base performance on the WCNP platform for accessing simple services.  These metrics may be used in a number of ways to derive useful information such as:

- Average duration of request by mode (direct or query)
- NGSA App (server) perspective of performance
- LodeRunner (client) perspective of performance
- Data access latency between between clusters by comparing duration of ngsa-cosmos for each deployment cluster
- Service + network latency by cluster by comparing the in-memory performance numbers across deployment clusters
- Number of errors encountered

The prometheus metrics may be used by various consumers.  In particular, we have used Grafana to visualize the metrics.

#### Enabling the Metrics Endpoint

The Ngsa app an the LodeRunner app both have Prometheus Metrics endpoints that may be enabled at runtime. The following is an example yaml for configuring the Ngsa app to run in memory mode:

```yaml
  containers:
  - name: ds
    imagePullPolicy: Always
    image: ghcr.io/retaildevcrews/ngsa-app:beta
    args:
      - --in-memory
      - --prometheus
      - --log-level
      - Warning
      - --request-log-level
      - Information
      - --zone
      - dev
      - --region
      - dev
    ports:
    - containerPort: 8080
```

In the example above, the `--prometheus` flag is passed which enables the metrics endpoint for scraping by Prometheus.  The same flag may be passed to LodeRunner as well to enable the same metrics endpoint for it.

#### Metrics Data Dictionary

Both the NGSA app and LodeRunner emit several metrics via a Prometheus endpoint.  The following table describes the metrics that are emitted and their intended use:

<!-- markdownlint-disable MD033 -->
|**Metric**|**Type**|**Range**|**Description**|
| :-- | :-- | :-- | :-- |
|NgsaAppSummary<br>LodeRunnerSummary|struct| \[code, cosmos, mode, region, zone, quartil\]|Used to calculate the average duration across request types and filter by constituent values listed in **Range**. Quartile values are used to graph values that meet the 0.95 and 0.99 confidence intervals.<br>**Example query**: avg(NgsaAppSummary{namespace="\$namespace",code="OK",service="\$service",region=~"\$region",zone="azure",quantile=~"0.95|0.99"}) by (mode, quantile)|
|NgsaAppSummary_countLodeRunner<br>Summary_count|struct|\[code, cosmos, mode, region, zone\]|Provides a running total of requests at a given time over a duration.<br>**Example query**: TBD|
|NgsaAppSummary_sum<br>LodeRunnerSummary_sum|struct|\[code, cosmos, mode, region, zone\]|Sum of the summary values at a given instance.<br>**Example query**: TBD|
|NgsaAppDuration_bucket<br>LodeRunnerSummary_bucket|struct|\[code, cosmos, mode, region, zone, le\]|Used to calculate the number of requests per intervale by a given mode (Query or Direct).<br>**Example query**: sum(rate(NgsaAppDuration_bucket{namespace="\$namespace",zone="azure",region=~"\$region",service="\$service"}\[1m\])) by (mode)|
|NgsaAppDuration_count<br>LodeRunnerDuration_count|struct|\[code, cosmos, mode, region, zone\]|Used to calculate the requests per second over a given interval. Predicates may be used to filter by any of the field values.<br>**Example query**: sum(rate(NgsaAppDuration_count{namespace='\$namespace",zone="azure",region=~"\$region",service="\$service",mode != "Metrics"}\[1m\]))|
|NgsaAppDuration_sum<br>LodeRunnerDuration_sum|struct|\[code, cosmos, mode, region, zone\]|Provides the sum of the duration values at a given instance.<br>**Example query**: TBD|

Each of the above metrics has several field values that are used as part of the measurement.  The following table describes those sub-metric fields and their intended use:

|**Field**|**Type**|**Range**|**Description**|
| :-- | :-- | :-- | :-- |
|mode|string|\[Query, Direct\]|Represent whether a request to CosmosDB is using a **direct** read by id or if it is using a **query** that must be parsed and its predicates used to filter.|
|cosmos|bool|\[True, False\]|Indicates whether the app is running in in-memory mode or querying CosmosDB.This is not currently used in the WCNP deployment as the **service** field is used to filter for **ngsa-in-memory** or **ngsa-cosmos**|
|region|string|Based on environment|This is set from the cluster environment and represents the geo region of the deployment. It is set in kitt.yml as:<br>`- --region={{$.kittExec.currentCluster.site}}`|
|zone|string|Based on environment|This is set from the cluster environment and represents the cloud used for the deployment. It is set in kitt.yml as:<br>`- --zone={{$.kittExec.currentCluster.provider}}`|
|code|string|\[Error, Retry, Warn, OK\]|This value represents the HTTP status disposition of the request:<br>"Error" → 500+ <br>"Retry" → 429<br>"Warn" → Any non-429 400 response<br>"OK" → Any value < 400|
|quantile|decimal|\[0, 100\]|Used to indicate confidence interval in which the measurement lands.|
|le|int||Value added by Prometheus library to indicate "buckets" for a histogram|
