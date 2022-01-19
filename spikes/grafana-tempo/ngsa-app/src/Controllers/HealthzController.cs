// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Ngsa.Application.DataAccessLayer;
using Ngsa.Application.Model;

namespace Ngsa.Application.Controllers
{
    /// <summary>
    /// Handle the /healthz* requests
    ///
    /// Cache results to prevent monitoring from overloading service
    /// </summary>
    [Route("[controller]")]
    [ResponseCache(Duration = 60)]
    public class HealthzController : Controller
    {
        private readonly ILogger logger;
        private readonly ILogger<CosmosHealthCheck> hcLogger;
        private readonly IDAL dal;
        private readonly IHttpClientFactory httpClientFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthzController"/> class.
        /// </summary>
        /// <param name="logger">logger</param>
        /// <param name="dal">data access layer</param>
        /// <param name="hcLogger">HealthCheck logger</param>
        /// <param name="hcf">HttpClientFactory</param>
        public HealthzController(ILogger<HealthzController> logger, ILogger<CosmosHealthCheck> hcLogger, IHttpClientFactory hcf)
        {
            this.logger = logger;
            this.hcLogger = hcLogger;
            dal = App.Config.CosmosDal;
            this.httpClientFactory = hcf;
        }

        /// <summary>
        /// Returns a plain text health status (Healthy, Degraded or Unhealthy)
        /// </summary>
        /// <returns>IActionResult</returns>
        [HttpGet]
        [Produces("text/plain")]
        [ProducesResponseType(typeof(string), 200)]
        public async Task<IActionResult> RunHealthzAsync()
        {
            // get list of genres as list of string
            logger.LogInformation(nameof(RunHealthzAsync));

            HealthCheckResult res = await RunCosmosHealthCheck().ConfigureAwait(false);

            HttpContext.Items.Add(typeof(HealthCheckResult).ToString(), res);

            ContentResult result = new ()
            {
                Content = IetfCheck.ToIetfStatus(res.Status),
                StatusCode = res.Status == HealthStatus.Unhealthy ? (int)System.Net.HttpStatusCode.ServiceUnavailable : (int)System.Net.HttpStatusCode.OK,
            };

            BurstMetricsService.InjectBurstMetricsHeader(Response.HttpContext);

            if (App.Config.UseIstioTraceID && App.Config.PropagateApis != null)
            {
                // We must have all the APIs
                for (int i = 0; i < App.Config.PropagateApis.Count; i++)
                {
                    var client = httpClientFactory.CreateClient($"mock-client-{i}");
                    // We don't care about the response body
                    // Just testing Trace Header propagation
                    await client.GetAsync("/healthz");
                }
            }

            return result;
        }

        /// <summary>
        /// Returns an IETF (draft) health+json representation of the full Health Check
        /// </summary>
        /// <returns>IActionResult</returns>
        [HttpGet("ietf")]
        [Produces("application/health+json")]
        [ProducesResponseType(typeof(CosmosHealthCheck), 200)]
        public async Task RunIetfAsync()
        {
            logger.LogInformation(nameof(RunHealthzAsync));

            DateTime dt = DateTime.UtcNow;

            HealthCheckResult res = await RunCosmosHealthCheck().ConfigureAwait(false);

            HttpContext.Items.Add(typeof(HealthCheckResult).ToString(), res);

            await CosmosHealthCheck.IetfResponseWriter(HttpContext, res, DateTime.UtcNow.Subtract(dt)).ConfigureAwait(false);
        }

        /// <summary>
        /// Run the health check
        /// </summary>
        /// <returns>HealthCheckResult</returns>
        private async Task<HealthCheckResult> RunCosmosHealthCheck()
        {
            CosmosHealthCheck chk = new (hcLogger, dal);

            return await chk.CheckHealthAsync(new HealthCheckContext()).ConfigureAwait(false);
        }
    }
}
