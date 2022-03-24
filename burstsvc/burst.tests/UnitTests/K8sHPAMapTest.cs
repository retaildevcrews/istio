// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using Burst.Tests.Helper;
using k8s.Models;
using Moq;
using Ngsa.BurstService.K8sApi;
using Xunit;

namespace Burst.Tests.UnitTests
{
    /// <summary>
    /// Represents HPAMap Test class.
    /// </summary>
    public class K8sHpaMapTest
    {
        /// <summary>
        /// Testing ToHPAMetrics function.
        /// </summary>
        [Fact]
        [Trait("Category", "UnitTest")]
        public void Test_CreateHPAMap()
        {
            // Arrange
            var hpaessential = HPAEssentials.GenerateHPAEssentials();
            Mock<IK8sClientFacade> facade = new();
            facade.Setup(x => x.BuildK8sConfiguration(null)).Verifiable();
            V2beta2HorizontalPodAutoscalerList v2hpaList = new();
            v2hpaList.Items = new List<V2beta2HorizontalPodAutoscaler>();
            foreach (var hpa in hpaessential)
            {
                var v1ObjMetadata = new V1ObjectMeta(
                    name: hpa.Deployment,
                    namespaceProperty: hpa.Namespace);
                var mockHPA = new V2beta2HorizontalPodAutoscaler(
                    metadata: v1ObjMetadata,
                    spec: new V2beta2HorizontalPodAutoscalerSpec(
                        maxReplicas: hpa.MaxReplicas,
                        minReplicas: hpa.MinReplicas,
                        scaleTargetRef: new V2beta2CrossVersionObjectReference(
                            kind: "Deployment",
                            name: hpa.Deployment,
                            apiVersion: hpa.ApiVersion)),
                    status: new V2beta2HorizontalPodAutoscalerStatus(
                        currentReplicas: hpa.CurrentReplicas,
                        desiredReplicas: hpa.MaxReplicas));

                v2hpaList.Items.Add(mockHPA);
                // TODO: Start from here
                // var deplK8s = new V1Deployment(metadata: v1ObjMetadata, spec: new V1DeploymentSpec())
                // facade.Setup(x => x.ReadNamespacedDeployment(hpa.Deployment, hpa.Deployment)).Returns()
            }

            facade.Setup(x => x.ListHPAForAllNamespaces(null, null, It.IsAny<int>())).Returns(v2hpaList);

            // Act
            K8sHPAMap k8sHpaMapMock = new(null);

            // Assert
            Assert.Null(k8sHpaMapMock.AsList);
        }
    }
}
