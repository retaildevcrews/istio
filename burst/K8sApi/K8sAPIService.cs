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
    public interface IK8sHPAStatusService
    {
        V2beta2HorizontalPodAutoscaler GetK8sHPAStatus(string ns, string deployment);
    }
    public class K8sHPAStatusService : IHostedService, IDisposable, IK8sHPAStatusService
    {
        private readonly ILogger<K8sHPAStatusService> _logger;
        private Timer _timer;
        private readonly IKubernetes _client;
        private V2beta2HorizontalPodAutoscalerList _hpaList;

        public K8sHPAStatusService(ILogger<K8sHPAStatusService> logger)
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

        public V2beta2HorizontalPodAutoscaler GetK8sHPAStatus(string ns, string deployment)
        {
            if (_hpaList == null)
            {
                _logger.LogWarning("HPA List is not populated");
                return null;
            }

            foreach (var hpa in _hpaList.Items)
            {
                if (hpa.Namespace().Equals(ns) && hpa.Name().Equals(deployment))
                    return hpa;
            }

            _logger.LogWarning("No HPA named {} in namespace '{}' found", deployment, ns);

            return null;
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Timed Hosted Service running.");

            _timer = new Timer(DoWork, null, TimeSpan.Zero,
                TimeSpan.FromSeconds(5));

            return Task.CompletedTask;
        }

        private void DoWork(object state)
        {
            // Call the K8s API

            // TODO: Try out differt version of API
            var hpaList = _client.ListHorizontalPodAutoscalerForAllNamespaces2(timeoutSeconds: 1);

            if(_hpaList == null)
            {
                _hpaList = hpaList;
            }
            else
            {
                // Might be unncessary to use InterLocking 
                Interlocked.Exchange<V2beta2HorizontalPodAutoscalerList>(ref _hpaList, hpaList);
            }
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Timed Hosted Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
