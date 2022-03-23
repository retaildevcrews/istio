// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Burst.Tests.Helper;
using k8s;
using k8s.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Moq;
using Ngsa.BurstService.Controllers;
using Ngsa.BurstService.K8sApi;
using Xunit;

namespace Burst.Tests.UnitTests
{
    /// <summary>
    /// Represents BurstMetricsController Test class.
    /// </summary>
    public class BurstMetricsControllerTest
    {
        [Fact]
        [Trait("Category", "UnitTest")]
        public void Ttest_GetSingleTarget()
        {
            // Generic Arrange
            var hpaessential = HPAEssentials.GenerateHPAEssentials();
            var metrics = hpaessential[0].CreateExpectedMetrics();

            // Arrange for deployment, service and hpa, should be the same as deployment if the k8s object names are same
            Mock<IK8sHPAMetricsService> hpaSvc = new();
            hpaSvc.Setup(x => x.GetK8SHPAMetrics(It.IsAny<K8sScaleTargetType>(), hpaessential[0].Namespace, hpaessential[0].Deployment)).Returns(metrics);

            hpaSvc.Setup(x => x.GetK8SHPAMetrics(It.IsAny<K8sScaleTargetType>(), "no-ns", It.IsAny<string>())).Returns<K8sHPAMetrics>(null);
            BurstMetricsController controller = new(null, hpaSvc.Object);

            foreach (var type in new List<K8sScaleTargetType>() { K8sScaleTargetType.Deployment, K8sScaleTargetType.HPA, K8sScaleTargetType.Service })
            {
                // Act
                var result = controller.Get(type, hpaessential[0].Namespace, hpaessential[0].Deployment);

                // Assert
                Assert.IsType<OkObjectResult>(result);
                Assert.Equal(metrics.ToString(), (result as OkObjectResult).Value as string);
                Assert.Equal(200, (result as OkObjectResult).StatusCode);
            }

            // Act: Check for Null or NoContent
            var nullResult = controller.Get(K8sScaleTargetType.Deployment, "no-ns", hpaessential[0].Deployment);

            // Assert
            Assert.IsType<NoContentResult>(nullResult);
            Assert.Equal(204, (nullResult as NoContentResult).StatusCode);
        }

        [Fact]
        [Trait("Category", "UnitTest")]
        public void Test_GetBulkTarget()
        {
            // Generic Arrange
            var hpaessential = HPAEssentials.GenerateHPAEssentials();

            Dictionary<string, string> bulkDict = new();
            foreach (var hpae in hpaessential)
            {
                var metrics = hpae.CreateExpectedMetrics();
                bulkDict[metrics.Service] = metrics.ToString();
            }

            // Arrange for deployment, service and hpa, should be the same as deployment if the k8s object names are same
            Mock<IK8sHPAMetricsService> hpaSvc = new();
            hpaSvc.Setup(x => x.GetBulkK8SHPAMetrics(It.IsAny<K8sScaleTargetType>())).Returns(bulkDict);

            BurstMetricsController controller = new(null, hpaSvc.Object);

            foreach (var type in new List<K8sScaleTargetType>() { K8sScaleTargetType.Deployment, K8sScaleTargetType.HPA, K8sScaleTargetType.Service })
            {
                // Act
                var result = controller.BulkGet(type);

                // Assert
                Assert.IsType<OkObjectResult>(result);
                Assert.Equal(bulkDict, (result as OkObjectResult).Value as Dictionary<string, string>);
                Assert.Equal(200, (result as OkObjectResult).StatusCode);
            }

            // Arrange for Null
            hpaSvc.Reset();
            hpaSvc.Setup(x => x.GetBulkK8SHPAMetrics(It.IsAny<K8sScaleTargetType>())).Returns<Dictionary<string, string>>(null);
            controller = new(null, hpaSvc.Object);

            // Act: Check for Null or NoContent
            var nullResult = controller.BulkGet(K8sScaleTargetType.Deployment);

            // Assert
            Assert.IsType<NoContentResult>(nullResult);
            Assert.Equal(204, (nullResult as NoContentResult).StatusCode);
        }
    }
}
