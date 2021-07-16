using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace Burst.Controllers
{
    using Burst.K8sApi;
    using System.Threading;

    [ApiController]
    [Route("/burstmetrics")]
    public class ApiController : ControllerBase
    {
        //private TimedHostedService timedService
        private readonly ILogger<ApiController> _logger;
        private readonly IK8sHPAMetricsService _service;
        public ApiController(ILogger<ApiController> logger, IK8sHPAMetricsService timedService)
        {
            _logger = logger;
            if (timedService != null)
            {
                // _logger.LogInformation("Got it right");
                _service = timedService;
            }
            else
            {
                // _logger.LogInformation("Somrhing is wrong");
            }
        }

        [HttpGet("{ns}/{deployment}")]
        public IActionResult Get(string ns, string deployment)
        {
            var hpaMetrics = _service.GetK8SHPAMetrics(ns, deployment);
            // Nullable interpolation will return "" for null objects
            // string cpuTarget = $"{hpaMetrics?.TargetCPULoad}";
            // string cpuCurrent = $"{hpaMetrics?.CurrentCPULoad}";
            // But we can control what to output if we do null
            // TODO: Set the default value from appsettings.json
            string cpuTarget = hpaMetrics?.TargetCPULoad?.ToString() ?? "-1";
            string cpuCurrent = hpaMetrics?.CurrentCPULoad?.ToString() ?? "-1";
            // Get the CPU Target
            _logger.LogDebug("Target: {}, Cur CPU: {}", cpuTarget, cpuCurrent);
            // Console.WriteLine($"{DateTime.Now:s}  {Request.Path.ToString()}");
            return Ok($"service: {ns}/{deployment}, current-load: {cpuCurrent}, target-load: {cpuTarget}, max-load: 85");
        }
    }
}
