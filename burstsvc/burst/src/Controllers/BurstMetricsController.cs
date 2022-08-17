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
            service = timedService;
        }

        [HttpGet("{target}s/")]
        public IActionResult BulkGet(K8sScaleTargetType target)
        {
            var hpaDictionary = service.GetBulkK8SHPAMetrics(target);

            if (hpaDictionary == null)
            {
                // Means we don't have all the values available
                return NoContent();
            }

            return Ok(hpaDictionary);
        }

        [HttpGet("{target}s/{ns}/{name}")]
        public IActionResult Get(K8sScaleTargetType target, string ns, string name)
        {
            K8sHPAMetrics hpaMetrics = service.GetK8SHPAMetrics(target, ns, name);

            // Nullable interpolation will return "" for null objects
            if (hpaMetrics == null)
            {
                // Means we don't have all the values available
                return NoContent();
            }

            return Ok(hpaMetrics.ToString());
        }
    }
}
