// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using System;
using System.Collections.Generic;
using System.Threading;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;

using HPADictionary = System.Collections.Generic.Dictionary<string, Ngsa.BurstService.K8sApi.K8sHPAMetrics>;
using K8sHPAObj = k8s.Models.V2beta2HorizontalPodAutoscaler;

namespace Ngsa.BurstService.K8sApi
{
    /// <summary>
    /// Handle all /burstmetrics requests.
    /// </summary>
    public sealed class K8sClientFacade
    {
        public enum K8sConfigType
        {
            /// <summary>
            /// Represents a k8s config from default k8s context.
            /// </summary>
            DefaultContext,

            /// <summary>
            /// Represents a k8s config inside a pod in a cluster.
            /// </summary>
            InCluster,
        }

        public IKubernetes K8sObject { get; private set; }

        private readonly ILogger logger;
        public K8sClientFacade(ILogger logger)
        {
            this.logger = logger;
        }

        public bool BuildK8sConfiguration(K8sConfigType? configType = null)
        {
            // Not handling any k8s exceptions here
            // This will pass all k8s exceptions
            this.K8sObject = new Kubernetes(
                configType switch
                {
                    null => KubernetesClientConfiguration.IsInCluster() ? KubernetesClientConfiguration.InClusterConfig() : KubernetesClientConfiguration.BuildDefaultConfig(),
                    K8sConfigType.InCluster => KubernetesClientConfiguration.InClusterConfig(),
                    K8sConfigType.DefaultContext => KubernetesClientConfiguration.BuildDefaultConfig(),
                    _ => null,

                });

            return this.K8sObject != null;
        }

        public V2beta2HorizontalPodAutoscalerList ListHPAForAllNamespaces(string fieldSelector = null, string labelSelector = null, int? timeoutSeconds = null)
        {
            return this.K8sObject?.ListHorizontalPodAutoscalerForAllNamespaces3(fieldSelector: fieldSelector, labelSelector: labelSelector, timeoutSeconds: timeoutSeconds);
        }

        public V1Deployment ReadNamespacedDeployment(string name, string namespaceParameter)
        {
            return this.K8sObject?.ReadNamespacedDeployment(name, namespaceParameter);
        }

        public V1ServiceList ListNamespacedService(string namespaceParameter, string fieldSelector = null, string labelSelector = null, int? timeoutSeconds = null)
        {
            return this.K8sObject?.ListNamespacedService(namespaceParameter, fieldSelector: fieldSelector, labelSelector: labelSelector, timeoutSeconds: timeoutSeconds);
        }
    }
}
