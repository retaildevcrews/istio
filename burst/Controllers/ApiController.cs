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
        private readonly IK8sHPAStatusService _service;
        public ApiController(ILogger<ApiController> logger, IK8sHPAStatusService timedService)
        {
            _logger = logger;
            if (timedService != null)
            {
                _logger.LogInformation("Got it right");
                _service = timedService;
            }
            else
            {
                _logger.LogInformation("Somrhing is wrong");
            }
        }

        [HttpGet("{ns}/{deployment}")]

        public IActionResult Get(string ns, string deployment)
        {
            var hpa = _service.GetK8sHPAStatus(ns, deployment);
            string cpuTarget = "-1";
            string cpuCurrent = "0";
            if (hpa != null)
            {
                // Get the CPU Target
                foreach (var m in hpa.Spec.Metrics)
                {
                    // We're interested in CPU metrics
                    if (m.Resource.Name == "cpu")
                    {
                        cpuTarget = m.Resource.Target.AverageUtilization.ToString();
                        break;
                    }
                }
                foreach (var m in hpa.Status.CurrentMetrics)
                {
                    // We're interested in CPU metrics
                    if (m.Resource.Name == "cpu")
                    {
                        cpuCurrent = m.Resource.Current.AverageUtilization.ToString();
                        break;
                    }
                }
                _logger.LogInformation("Target: {}, Cur CPU: {}", cpuTarget, cpuCurrent);
            }
            Console.WriteLine($"{DateTime.Now:s}  {Request.Path.ToString()}");
            return Ok($"service: {ns}/{deployment}, current-load: {cpuCurrent}, target-load: {cpuTarget}, max-load: 85");
        }
    }
}
