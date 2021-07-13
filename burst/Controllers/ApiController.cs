using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Burst.Controllers
{
    [ApiController]
    [Route("/burstmetrics")]
    public class ApiController : ControllerBase
    {
        private readonly ILogger<ApiController> _logger;

        public ApiController(ILogger<ApiController> logger)
        {
            _logger = logger;
        }

        [HttpGet("{ns}/{deployment}")]

        public IActionResult Get(string ns, string deployment)
        {
            Console.WriteLine($"{DateTime.Now:s}  {Request.Path.ToString()}");
            return Ok($"service: {ns}/{deployment}, current-load: 27, target-load: 60, max-load: 85");
        }
    }
}
