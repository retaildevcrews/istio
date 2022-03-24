// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using HPADictionary = System.Collections.Generic.Dictionary<string, string>;

namespace Ngsa.BurstService.K8sApi
{
    public class K8sHPAMetricsService : IHostedService, IDisposable, IK8sHPAMetricsService
    {
        private readonly ILogger<K8sHPAMetricsService> logger;
        private readonly K8sClientFacade k8SClientFacade;
        private readonly K8sScaleTargetType scaleTargetType;
        private readonly IReadOnlyList<string> svcSelectors;
        private readonly K8sHPAMap hpaMap;
        private System.Timers.Timer timer;

        public K8sHPAMetricsService(ILogger<K8sHPAMetricsService> logger, IConfiguration configuration)
        {
            this.logger = logger;
            this.hpaMap = new(logger);

            // Get the Target Scale Object (usually deployment)
            this.scaleTargetType = Enum.TryParse(configuration["Service:TargetScaleObj"], true, out K8sScaleTargetType parsed) ? parsed : K8sScaleTargetType.Deployment;

            this.svcSelectors = configuration.GetSection("Service:SelectorLabel").Get<List<string>>();

            k8SClientFacade = new K8sClientFacade(logger);
            if (!k8SClientFacade.BuildK8sConfiguration())
            {
                logger?.LogCritical("K8s Client cannot be created");
            }
        }

        public HPADictionary GetBulkK8SHPAMetrics(K8sScaleTargetType target)
        {
            // If cluster has no hpa, then RBAC issue or didn't deploy
            if (hpaMap.IsEmpty())
            {
                logger.LogError("No HPA found in any namespace. Check RBAC if an HPA already exist");
                return null;
            }

            // ToDictionary is O(N), will convert the dictionary of K8sHPAMetrics values to their string
            // represntation as opposed to changing it directly within the K8sHpaMap class
            return hpaMap.GetHPADictionary(target).ToDictionary(x => x.Key, x => x.Value.ToString());
        }

        public K8sHPAMetrics GetK8SHPAMetrics(K8sScaleTargetType target, string ns, string deployment)
        {
            // If cluster has no hpa, then RBAC issue or didn't deploy
            if (hpaMap.IsEmpty())
            {
                logger.LogError("No HPA found in any namespace. Check RBAC if an HPA already exist");
            }
            else
            {
                K8sHPAMetrics hpaMetrics = hpaMap[(target, ns, deployment)];
                if (hpaMetrics == null)
                {
                    // Didn't find any HPA in our hpaList object
                    logger.LogWarning("No HPA found with matching name ({}) in namspace '{}'", deployment, ns);

                    return null;
                }

                // If calculated target load is zero (it can be since we are flooring MaxLoad)
                if (hpaMetrics.TargetLoad == 0)
                {
                    hpaMetrics.TargetLoad = hpaMetrics.MaxLoad;
                }

                return hpaMetrics;
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

        private void TimerWork(object state, ElapsedEventArgs e)
        {
            // Call the K8s API
            try
            {
                hpaMap.CreateHPAMap(this.k8SClientFacade, this.scaleTargetType, this.svcSelectors);
            }
            catch (Exception ex)
            {
                // Don't have any HPA API Enabled!!
                logger.LogError(ex, "Failed to get HPA objects. Check ClusterRole or HPA objects");
            }
        }
    }
}
