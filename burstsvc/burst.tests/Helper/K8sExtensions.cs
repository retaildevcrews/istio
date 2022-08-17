// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using System;
using System.Collections.Generic;
using k8s;
using k8s.Models;
using Ngsa.BurstService.K8sApi;
namespace Burst.Tests.Helper
{
    internal static class K8sExtensions
    {
        public static V2HorizontalPodAutoscalerList ListHorizontalPodAutoscalerForAllNamespaces3(this IKubernetes ik8s, int timeoutSeconds = 1)
        {
            V2HorizontalPodAutoscalerList hpaList = new();
            var hpaEssentials = HPAEssentials.GenerateHPAEssentials();
            foreach (var hpa in hpaEssentials)
            {
                hpaList.Items.Add(hpa.CreateMockHPA());
            }

            return hpaList;
        }
    }
}
