// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Ngsa.BurstService.K8sApi;

namespace Ngsa.BurstService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class BurstMetricsController : ControllerBase
    {
        //private TimedHostedService timedService
        private readonly ILogger<BurstMetricsController> logger;
        private readonly IK8sHPAMetricsService service;
        public BurstMetricsController(ILogger<BurstMetricsController> logger, IK8sHPAMetricsService timedService)
        {
            this.logger = logger;
            if (timedService != null)
            {
                // _logger.LogInformation("Got it right");
                service = timedService;
            }
            else
            {
                // _logger.LogInformation("Somrhing is wrong");
            }
        }

        [HttpGet("{target}/{ns}/{deployment}")]
        public IActionResult Get(K8sScaleTargetType target, string ns, string deployment)
        {
            K8sHPAMetrics hpaMetrics = service.GetK8SHPAMetrics(target, ns, deployment);

            // Nullable interpolation will return "" for null objects
            // string cpuTarget = $"{hpaMetrics?.TargetCPULoad}";
            // string cpuCurrent = $"{hpaMetrics?.CurrentCPULoad}";
            // But we can control what to output if we do null
            // TODO: Set the default value from appsettings.json

            if (hpaMetrics == null)
            {
                // Means we don't have all the values available
                return NoContent();
            }

            // hpaMetrics was Null checked, so it should have all the values
            // Checking nullable would be redundant
            string targetLoad = hpaMetrics.TargetLoad.ToString();
            string currentLoad = hpaMetrics.CurrentLoad.ToString();
            string maxLoad = hpaMetrics.MaxLoad.ToString();

            // Get the CPU Target
            logger.LogDebug("Max: {}, Target: {}, Cur: {},", maxLoad, targetLoad, currentLoad);

            // Console.WriteLine($"{DateTime.Now:s}  {Request.Path.ToString()}");
            return Ok($"service={ns}/{deployment}, current-load={currentLoad}, target-load={targetLoad}, max-load={maxLoad}");
        }
    }
}
