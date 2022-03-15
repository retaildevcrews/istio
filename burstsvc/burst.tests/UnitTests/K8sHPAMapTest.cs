// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using System;
using System.Collections.Generic;
using k8s.Models;
using Ngsa.BurstService.K8sApi;
using Xunit;

namespace Burst.Tests.UnitTests
{
    /// <summary>
    /// Represents HPAMap Test class.
    /// </summary>
    public class K8sHPAMapTest
    {
        private readonly V2beta2HorizontalPodAutoscalerList listOfHpa;
        private readonly Dictionary<string, string> nsDeploy = new()
        {
            ["ns1"] = "deploy1",
            ["ns2"] = "deploy2",
            ["ns3"] = "deploy3",
            ["ns4"] = "deploy4",
            ["ns5"] = "deploy5",
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="K8sHPAMapTest"/> class.
        /// </summary>
        public K8sHPAMapTest()
        {
            List<V2beta2HorizontalPodAutoscaler> hps = new();
            foreach (var kv in this.nsDeploy)
            {
                hps.Add(new V2beta2HorizontalPodAutoscaler()
                {
                    Metadata = new V1ObjectMeta(name: kv.Value, namespaceProperty: kv.Key),
                });
            }

            this.listOfHpa = new V2beta2HorizontalPodAutoscalerList(hps);
        }

        /// <summary>
        /// Testing ParseHPAList function.
        /// </summary>
        [Fact]
        [Trait("Category", "UnitTest")]
        public void TestParseHPAList()
        {
            K8sHPAMap map = new();
            Assert.True(map.IsEmpty());
            Assert.Null(map.AsList);
            Assert.Null(map.GetHPA("ns", "deploy"));

            // Parse the HPA
            map.ParseHPAList(this.listOfHpa);
            Assert.False(map.IsEmpty());
            Assert.NotNull(map.AsList);
        }

        /// <summary>
        /// Testing GetHPA function.
        /// </summary>
        [Fact]
        [Trait("Category", "UnitTest")]
        public void TestGetHPA()
        {
            K8sHPAMap map = new();

            // Parse the HPA
            map.ParseHPAList(this.listOfHpa);
            Assert.False(map.IsEmpty());
            Assert.NotNull(map.AsList);

            foreach (var kv in this.nsDeploy)
            {
                Assert.Null(map.GetHPA("ns-non-existing", kv.Value));

                // Invalid Deployment name
                Assert.Null(map.GetHPA(kv.Key, "deploy-non-existing"));

                // Get a valid Deployment
                Assert.NotNull(map.GetHPA(kv.Key, kv.Value));
            }
        }

        /// <summary>
        /// Testing GetHPA function.
        /// </summary>
        [Fact]
        [Trait("Category", "UnitTest")]
        public void TestGetHPAWithIndexer()
        {
            K8sHPAMap map = new();

            // Parse the HPA
            map.ParseHPAList(this.listOfHpa);
            Assert.False(map.IsEmpty());
            Assert.NotNull(map.AsList);

            // Now test using the index accessor
            foreach (var kv in this.nsDeploy)
            {
                // Invalid Namespace name
                Assert.Null(map[("ns-non-existing", kv.Value)]);

                // Invalid Deployment name
                Assert.Null(map[(kv.Key, "deploy-non-existing")]);

                // Get a valid Deployment
                Assert.NotNull(map[(kv.Key, kv.Value)]);
            }
        }
    }
}
