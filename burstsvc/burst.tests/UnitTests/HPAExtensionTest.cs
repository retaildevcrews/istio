// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using k8s.Models;
using Ngsa.BurstService.K8sApi;
using Xunit;

namespace Burst.Tests.UnitTests
{
    /// <summary>
    /// Represents HPAMap Test class.
    /// </summary>
    public class HPAExtensionTest
    {
        private readonly V2beta2HorizontalPodAutoscalerList listOfHpa;
        private readonly List<HPAEssentials> nsDeploy = new()
        {
            new(
                deployment: "deploy1",
                @namespace: "ns1",
                maxReplicas: 10,
                minReplicas: 1,
                currentReplicas: 1,
                targetPercent: .8,
                apiVersion: "v2beta2"),
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
                apiVersion: "v2beta2"),
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="K8sHPAMapTest"/> class.
        /// </summary>
        public HPAExtensionTest()
        {
            List<V2beta2HorizontalPodAutoscaler> hpas = new();
            foreach (var hPAEssentials in this.nsDeploy)
            {
                hpas.Add(hPAEssentials.FakeHpa);
            }

            this.listOfHpa = new V2beta2HorizontalPodAutoscalerList(hpas);
        }

        /// <summary>
        /// Testing ToHPAMetrics function.
        /// </summary>
        [Fact]
        [Trait("Category", "UnitTest")]
        public void TestToHPAMetrics()
        {
            foreach (var hpa in this.nsDeploy)
            {
                var metrics = hpa.FakeHpa.ToHPAMetrics(hpa.TargetPercent);
                // System.Console.WriteLine("Actual {0}\nExpect {1}", metrics.ToString(), hpa.ExpectedMetrics.ToString());
                Assert.True(hpa.ExpectedMetrics.ToString() == metrics.ToString());
                Assert.True(
                   hpa.ExpectedMetrics.CurrentLoad == metrics.CurrentLoad
                    && hpa.ExpectedMetrics.TargetLoad == metrics.TargetLoad
                    && hpa.ExpectedMetrics.MaxLoad == metrics.MaxLoad
                    && hpa.ExpectedMetrics.Service == metrics.Service);
            }
        }

        /// <summary>
        /// Testing GetCurrentLoad function.
        /// </summary>
        [Fact]
        [Trait("Category", "UnitTest")]
        public void TestGetCurrentLoad()
        {
            foreach (var hpa in this.nsDeploy)
            {
                var currentLoad = hpa.FakeHpa.GetCurrentLoad();
                Assert.Equal(hpa.CurrentReplicas, currentLoad);
            }
        }

        /// <summary>
        /// Testing GeMaxLoad function.
        /// </summary>
        [Fact]
        [Trait("Category", "UnitTest")]
        public void TestGetMaxLoad()
        {
            foreach (var hpa in this.nsDeploy)
            {
                var maxLoad = hpa.FakeHpa.GetMaxLoad();
                Assert.Equal(hpa.MaxReplicas, maxLoad);
            }
        }

        internal struct HPAEssentials
        {
            public string Namespace;
            public string Deployment;
            public int MaxReplicas;
            public int MinReplicas;
            public int CurrentReplicas;
            public string ApiVersion;
            public double TargetPercent;
            public V2beta2HorizontalPodAutoscaler FakeHpa;
            public K8sHPAMetrics ExpectedMetrics;

            public HPAEssentials(string @namespace, string deployment, int maxReplicas, int minReplicas, int currentReplicas, double targetPercent, string apiVersion)
            {
                this.Namespace = @namespace;
                this.Deployment = deployment;
                this.MaxReplicas = maxReplicas;
                this.MinReplicas = minReplicas;
                this.CurrentReplicas = currentReplicas;
                this.TargetPercent = targetPercent;
                this.ApiVersion = apiVersion;

                this.FakeHpa = new V2beta2HorizontalPodAutoscaler()
                {
                    Metadata = new V1ObjectMeta(name: this.Deployment, namespaceProperty: this.Namespace),
                    Spec = new V2beta2HorizontalPodAutoscalerSpec(maxReplicas: this.MaxReplicas, minReplicas: this.MinReplicas, scaleTargetRef: new V2beta2CrossVersionObjectReference(kind: "Deployment", name: this.Deployment, apiVersion: this.ApiVersion)),
                    Status = new V2beta2HorizontalPodAutoscalerStatus(currentReplicas: currentReplicas, desiredReplicas: maxReplicas),
                };

                var calcReplica = Math.Floor(this.MaxReplicas * this.TargetPercent);

                this.ExpectedMetrics = new K8sHPAMetrics()
                {
                    Service = string.Format("{0}/{1}", this.Namespace, this.Deployment),
                    CurrentLoad = this.CurrentReplicas,
                    TargetLoad = (int?)(calcReplica < 1 ? this.MaxReplicas : calcReplica),
                    MaxLoad = this.MaxReplicas,
                };
            }
        }
    }
}
