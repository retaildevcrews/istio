﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ngsa.BurstService.K8sApi
{
    public class K8sHPAMetricsService : IHostedService, IDisposable, IK8sHPAMetricsService
    {
        private const double TargetPercent = 0.8;
        private readonly ILogger<K8sHPAMetricsService> logger;
        private readonly IKubernetes client;
        private System.Timers.Timer timer;
        private V2beta1HorizontalPodAutoscalerList hpaList;

        public K8sHPAMetricsService(ILogger<K8sHPAMetricsService> logger)
        {
            this.logger = logger;

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
            client = new Kubernetes(config);
        }

        public K8sHPAMetrics GetK8SHPAMetrics(string ns, string deployment)
        {
            // If hpaList is null or the count is zero, we don't have any HPA
            if (hpaList == null || hpaList.Items.Count == 0)
            {
                logger.LogError("No HPA found in any namespace. Check RBAC if an HPA already exist");
            }
            else
            {
                K8sHPAMetrics hpaMetrics = new ();
                foreach (V2beta2HorizontalPodAutoscaler hpa in hpaList.Items)
                {
                    if (hpa.Namespace().Equals(ns) && hpa.Name().Equals(deployment))
                    {
                        try
                        {
                            // Get the Target CPU load
                            hpaMetrics.MaxLoad = GetMaxLoad(hpa);

                            // Get the current CPU load
                            hpaMetrics.CurrentLoad = GetCurrentLoad(hpa);
                            hpaMetrics.TargetLoad = (int?)Math.Floor(hpaMetrics.MaxLoad.GetValueOrDefault() * TargetPercent);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex.Message);

                            // Should have all values available
                            // Otherwise return null
                            return null;
                        }

                        // If calculated target load is zero (it can be since we are flooring MaxLoad)
                        if (hpaMetrics.TargetLoad == 0)
                        {
                            hpaMetrics.TargetLoad = hpaMetrics.MaxLoad;
                        }

                        return hpaMetrics;
                    }
                }

                // At this point didn't find any HPA in our hpaList object
                logger.LogWarning("No HPA found with matching name ({}) in namspace '{}'", deployment, ns);
            }

            return null;
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            timer = new System.Timers.Timer();
            timer.Elapsed += TimerWork;
            timer.Interval = App.Config.Frequency * 1000;

            // Run once before the timer
            Task.Run(() => TimerWork(this, null), stoppingToken);

            // Start the timer, it will be called after Interval
            timer.Start();

            logger.LogInformation("Running timed k8s HPA-API Service...");

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Stopping k8s HPA-API Service...");
            timer?.Stop();

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (timer != null)
                {
                    timer.Dispose();

                    // 'timer' is nullable-accessed at StopAsync
                    timer = null;
                }
            }
        }

        private static int GetCurrentLoad(V2beta2HorizontalPodAutoscaler hpa)
        {
            // Check if we created HPA but but don't have a metrics server
            int currReplicas;
            if (hpa?.Status != null)
            {
                currReplicas = hpa.Status.CurrentReplicas;
            }
            else
            {
                throw new Exception("Cannot get HPA metrics because hpa is null");
            }

            return currReplicas;
        }

        private static int GetMaxLoad(V2beta2HorizontalPodAutoscaler hpa)
        {
            int maxReplicas;

            // Check if we created HPA but didn't set any CPU Target
            if (hpa?.Spec != null)
            {
                maxReplicas = hpa.Spec.MaxReplicas;
            }
            else
            {
                throw new Exception("Cannot get HPA Spec because hpa is null");
            }

            return maxReplicas;
        }

        private void TimerWork(object state, ElapsedEventArgs e)
        {
            // Call the K8s API
            // TODO: Try out different version of K8s API
            try
            {
                V2beta1HorizontalPodAutoscalerList hpaList = client.ListHorizontalPodAutoscalerForAllNamespaces2(timeoutSeconds: 1);

                if (this.hpaList == null)
                {
                    this.hpaList = hpaList;
                }
                else
                {
                    // TODO: Might be unncessary to use InterLocking
                    Interlocked.Exchange<V2beta1HorizontalPodAutoscalerList>(ref this.hpaList, hpaList);
                }
            }
            catch (Exception ex)
            {
                // Don't have any HPA API Enabled!!
                logger.LogError(ex, "Failed to get HPA objects. Check ClusterRole or HPA objects");
            }
        }
    }
}
