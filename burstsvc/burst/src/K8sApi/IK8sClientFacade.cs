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

    /// <summary>
    /// Handle all /burstmetrics requests.
    /// </summary>
    public interface IK8sClientFacade
    {
        public bool BuildK8sConfiguration(K8sConfigType? configType = null);

        public V2beta2HorizontalPodAutoscalerList ListHPAForAllNamespaces(string fieldSelector = null, string labelSelector = null, int? timeoutSeconds = null);

        public V1Deployment ReadNamespacedDeployment(string name, string namespaceParameter);

        public V1ServiceList ListNamespacedService(string namespaceParameter, string fieldSelector = null, string labelSelector = null, int? timeoutSeconds = null);
    }
}
