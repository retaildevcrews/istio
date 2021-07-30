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
    public interface IK8sHPAMetricsService
    {
        K8sHPAMetrics GetK8SHPAMetrics(string ns, string deployment);
    }
    public class K8sHPAMetricsService : IHostedService, IDisposable, IK8sHPAMetricsService
    {
        private readonly ILogger<K8sHPAMetricsService> _logger;
        private Timer _timer;
        private readonly IKubernetes _client;
        private V2beta2HorizontalPodAutoscalerList _hpaList;

        public K8sHPAMetricsService(ILogger<K8sHPAMetricsService> logger)
        {
            _logger = logger;

            KubernetesClientConfiguration config;
            // Create a new config and a client
            if (KubernetesClientConfiguration.IsInCluster())
            {
                // Get In-Cluster Config
                config = KubernetesClientConfiguration.InClusterConfig();
            }
            else
            {
                // Otherwise get the default config from K8s
                config = KubernetesClientConfiguration.BuildDefaultConfig();
            }

            // Create a k8s client from config
            _client = new Kubernetes(config);
        }

        private int? getCurrentCpuLoad(V2beta2HorizontalPodAutoscaler hpa)
        {
            // Check if we created HPA but but don't have a metrics server
            if (hpa?.Status?.CurrentMetrics != null)
            {
                foreach (var m in hpa.Status.CurrentMetrics)
                {
                    // We're interested in CPU metrics
                    if (m.Resource.Name == "cpu")
                    {
                        return m.Resource.Current.AverageUtilization;
                    }
                }
            }
            _logger.LogWarning("Cannot get HPA metrics (probable cause: no metrics server)");

            return null;
        }

        private int? getTargetCpuLoad(V2beta2HorizontalPodAutoscaler hpa)
        {
            // Check if we created HPA but didn't set any CPU Target
            if( hpa?.Spec?.Metrics != null)
            {
                foreach (var m in hpa.Spec.Metrics)
                {
                    // We're interested in CPU metrics
                    if (m.Resource.Name == "cpu")
                    {
                        return m.Resource.Target.AverageUtilization;
                    }
                }
            }

            _logger.LogWarning("HPA Spec is not set");
            return null;
        }
        public K8sHPAMetrics GetK8SHPAMetrics(string ns, string deployment)
        {
            if (_hpaList == null)
            {
                _logger.LogWarning("HPA List is not populated");
                return null;
            }

            K8sHPAMetrics hpaMetrics = new K8sHPAMetrics();
            // If _hpaList is not null, we don't have any HPA
            if (_hpaList.Items.Count == 0)
            {
                _logger.LogWarning("No HPA object found in any namespace");
            }
            else
            {
                foreach (var hpa in _hpaList.Items)
                {
                    if (hpa.Namespace().Equals(ns) && hpa.Name().Equals(deployment))
                    {
                        // Get the Target CPU load
                        hpaMetrics.TargetCPULoad = getTargetCpuLoad(hpa);
                        // Get the current CPU load
                        hpaMetrics.CurrentCPULoad = getCurrentCpuLoad(hpa);
                        return hpaMetrics;
                    }
                }
            }

            // At the very least return empty metrics
            return hpaMetrics;
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            _timer = new Timer(TimerWork, null, TimeSpan.Zero,
            // TODO: Get timer period from appsettings.json
                TimeSpan.FromSeconds(5));
            _logger.LogInformation("Running timed k8s HPA-API Service...");

            return Task.CompletedTask;
        }

        private void TimerWork(object state)
        {
            // Call the K8s API
            // TODO: Try out differt version of API
            try
            {
                var hpaList = _client.ListHorizontalPodAutoscalerForAllNamespaces2(timeoutSeconds: 1);
                if(_hpaList == null)
                {
                    _hpaList = hpaList;
                }
                else
                {
                    // TODO: Might be unncessary to use InterLocking 
                    Interlocked.Exchange<V2beta2HorizontalPodAutoscalerList>(ref _hpaList, hpaList);
                }
            }
            catch (Exception ex)
            {
                // Don't have any HPA API Enabled!!
                _logger.LogError(ex, "Failed to get HPA objects. Check ClusterRole or HPA objects");
            }
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Stopping k8s HPA-API Service...");
            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
