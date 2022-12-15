// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using System;
using System.Collections.Generic;
using k8s;
using k8s.Models;
using Ngsa.BurstService.K8sApi;
namespace Burst.Tests.Helper
{
    internal struct HPAEssentials
    {
        public string Namespace;
        public string Deployment;
        public int MaxReplicas;
        public int MinReplicas;
        public int CurrentReplicas;
        public string ApiVersion;
        public double TargetPercent;

        public HPAEssentials(
            string @namespace,
            string deployment,
            int maxReplicas,
            int minReplicas,
            int currentReplicas,
            double targetPercent,
            string apiVersion)
        {
            Namespace = @namespace;
            Deployment = deployment;
            MaxReplicas = maxReplicas;
            MinReplicas = minReplicas;
            CurrentReplicas = currentReplicas;
            TargetPercent = targetPercent;
            ApiVersion = apiVersion;
        }

        public static List<HPAEssentials> GenerateHPAEssentials()
        {
            return new List<HPAEssentials>()
            {
                new(
                deployment: "deploy1",
                @namespace: "ns1",
                maxReplicas: 10,
                minReplicas: 1,
                currentReplicas: 1,
                targetPercent: .8,
                apiVersion: "v2"),
                new(
                deployment: "deploy2",
                @namespace: "ns2",
                maxReplicas: 5,
                minReplicas: 2,
                currentReplicas: 3,
                targetPercent: .5,
                apiVersion: "v1"),
                new(
                deployment: "deploy3",
                @namespace: "ns1",
                maxReplicas: 100,
                minReplicas: 50,
                currentReplicas: 77,
                targetPercent: .9,
                apiVersion: "v3"),
                new(
                deployment: "deploy4",
                @namespace: "ns2",
                maxReplicas: 5,
                minReplicas: 1,
                currentReplicas: 2,
                targetPercent: 0,
                apiVersion: "v2"),
            };
        }

        public V2HorizontalPodAutoscaler CreateMockHPA()
        {
            return new V2HorizontalPodAutoscaler()
            {
                Metadata = new V1ObjectMeta(name: Deployment, namespaceProperty: Namespace),
                Spec = new V2HorizontalPodAutoscalerSpec(maxReplicas: MaxReplicas, minReplicas: MinReplicas, scaleTargetRef: new V2CrossVersionObjectReference(kind: "Deployment", name: Deployment, apiVersion: ApiVersion)),
                Status = new V2HorizontalPodAutoscalerStatus(currentReplicas: CurrentReplicas, desiredReplicas: MaxReplicas),
            };
        }

        public K8sHPAMetrics CreateExpectedMetrics()
        {
            var calcReplica = Math.Floor(MaxReplicas * TargetPercent);
            return new K8sHPAMetrics()
            {
                Service = string.Format("{0}/{1}", Namespace, Deployment),
                CurrentLoad = CurrentReplicas,
                TargetLoad = (int?)(calcReplica < 1 ? MaxReplicas : calcReplica),
                MaxLoad = MaxReplicas,
            };
        }
    }
}
