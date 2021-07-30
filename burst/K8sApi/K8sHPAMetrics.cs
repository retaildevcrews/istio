using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;

namespace Burst.K8sApi
{
    public class K8sHPAMetrics
    {
        public int? CurrentCPULoad {get; internal set;} = null; 
        public int? TargetCPULoad {get; internal set;} = null;
    }
}
