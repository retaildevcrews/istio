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
    public class HPAExtensionTest
    {
        /// <summary>
        /// Testing ToHPAMetrics function.
        /// </summary>
        [Fact]
        [Trait("Category", "UnitTest")]
        public void Test_ToHPAMetrics()
        {
            // Arrange 1
            var fakeHPAList = HPAEssentials.GenerateHPAEssentials();
            foreach (var hpa in fakeHPAList)
            {
                // Arrange 2
                var mockHpa = hpa.CreateMockHPA();
                var expectedMetrics = hpa.CreateExpectedMetrics();

                // Act
                var metrics = mockHpa.ToHPAMetrics(hpa.TargetPercent);

                // Assert K8sHPAMetrics ToString and implicit string
                Assert.True(expectedMetrics.ToString() == metrics.ToString());
                Assert.True((string)expectedMetrics == (string)metrics);

                Assert.True(
                       expectedMetrics.CurrentLoad == metrics.CurrentLoad
                    && expectedMetrics.TargetLoad == metrics.TargetLoad
                    && expectedMetrics.MaxLoad == metrics.MaxLoad
                    && expectedMetrics.Service == metrics.Service);
            }

            V2HorizontalPodAutoscaler nullHpa = null;
            Assert.Null(nullHpa.ToHPAMetrics(0.0));
        }

        /// <summary>
        /// Testing GetCurrentLoad function.
        /// </summary>
        [Fact]
        [Trait("Category", "UnitTest")]
        public void Test_GetCurrentLoad()
        {
            // Arrange 1
            var fakeHPAList = HPAEssentials.GenerateHPAEssentials();
            foreach (var hpa in fakeHPAList)
            {
                // Arrange 2
                var mockHpa = hpa.CreateMockHPA();

                // Act
                var currentLoad = mockHpa.GetCurrentLoad();

                // Assert
                Assert.Equal(hpa.CurrentReplicas, currentLoad);
            }

            V2HorizontalPodAutoscaler nullHpa = null;
            Assert.Throws<Exception>(() => nullHpa.GetCurrentLoad());

            var mockHpa2 = fakeHPAList[0].CreateMockHPA();
            mockHpa2.Status = null;
            Assert.Throws<Exception>(() => nullHpa.GetCurrentLoad());
        }

        /// <summary>
        /// Testing GeMaxLoad function.
        /// </summary>
        [Fact]
        [Trait("Category", "UnitTest")]
        public void Test_GetMaxLoad()
        {
            // Arrange
            var fakeHPAList = HPAEssentials.GenerateHPAEssentials();
            foreach (var hpa in fakeHPAList)
            {
                // Arrange
                var mockHpa = hpa.CreateMockHPA();

                // Act
                var maxLoad = mockHpa.GetMaxLoad();

                // Assert
                Assert.Equal(hpa.MaxReplicas, maxLoad);
            }

            V2HorizontalPodAutoscaler nullHpa = null;
            Assert.Throws<Exception>(() => nullHpa.GetMaxLoad());
            var anotherMock = fakeHPAList[0].CreateMockHPA();
            anotherMock.Spec = null;
            Assert.Throws<Exception>(() => nullHpa.GetMaxLoad());
        }
    }
}
