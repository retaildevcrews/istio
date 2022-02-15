// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Ngsa.Application.DataAccessLayer;
using Ngsa.Application.Model;
using Ngsa.Middleware;

namespace Ngsa.Application
{
    /// <summary>
    /// Cosmos Health Check
    /// </summary>
    public partial class CosmosHealthCheck : IHealthCheck
    {
        public static readonly string ServiceId = "ngsa";
        public static readonly string Description = "NGSA Health Check";

        private static JsonSerializerOptions jsonOptions;

        private readonly ILogger logger;
        private readonly IDAL dal;

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosHealthCheck"/> class.
        /// </summary>
        /// <param name="logger">ILogger</param>
        /// <param name="dal">IDAL</param>
        public CosmosHealthCheck(ILogger<CosmosHealthCheck> logger, IDAL dal)
        {
            // save to member vars
            this.logger = logger;
            this.dal = dal;

            // setup serialization options
            if (jsonOptions == null)
            {
                // ignore nulls in json
                jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    IgnoreNullValues = true,
                    DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                };

                // serialize enums as strings
                jsonOptions.Converters.Add(new JsonStringEnumConverter());

                // serialize TimeSpan as 00:00:00.1234567
                jsonOptions.Converters.Add(new TimeSpanConverter());
            }
        }

        /// <summary>
        /// Run the health check (IHealthCheck)
        /// </summary>
        /// <param name="context">HealthCheckContext</param>
        /// <param name="cancellationToken">CancellationToken</param>
        /// <returns>HealthCheckResult</returns>
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            // dictionary
            Dictionary<string, object> data = new ();

            try
            {
                HealthStatus status = HealthStatus.Healthy;

                // add instance and version
                data.Add("Instance", System.Environment.GetEnvironmentVariable("WEBSITE_ROLE_INSTANCE_ID") ?? "unknown");
                data.Add("Version", Middleware.VersionExtension.Version);

                // Run each health check
                await GetGenresAsync(data).ConfigureAwait(false);
                await GetActorByIdAsync("nm0000173", data).ConfigureAwait(false);
                await GetMovieByIdAsync("tt0133093", data).ConfigureAwait(false);
                await SearchMoviesAsync("ring", data).ConfigureAwait(false);
                await SearchActorsAsync("nicole", data).ConfigureAwait(false);

                // overall health is the worst status
                foreach (object d in data.Values)
                {
                    if (d is HealthzCheck h && h.Status != HealthStatus.Healthy)
                    {
                        status = h.Status;
                    }

                    if (status == HealthStatus.Unhealthy)
                    {
                        break;
                    }
                }

                // return the result
                return new HealthCheckResult(status, Description, data: data);
            }
            catch (CosmosException ce)
            {
                // log and return Unhealthy
                logger.LogError($"{ce}\nCosmosException:Healthz:{ce.StatusCode}:{ce.ActivityId}:{ce.Message}");

                data.Add("CosmosException", ce.Message);

                return new HealthCheckResult(HealthStatus.Unhealthy, Description, ce, data);
            }
            catch (Exception ex)
            {
                // log and return unhealthy
                logger.LogError($"{ex}\nException:Healthz:{ex.Message}");

                data.Add("Exception", ex.Message);

                return new HealthCheckResult(HealthStatus.Unhealthy, Description, ex, data);
            }
        }
    }
}
