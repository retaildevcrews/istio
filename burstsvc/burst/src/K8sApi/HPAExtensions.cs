// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using k8s.Models;

namespace Ngsa.BurstService.K8sApi
{
    public static class HPAExtensions
    {
        public static K8sHPAMetrics ToHPAMetrics(this V2HorizontalPodAutoscaler hpa, double targetPercentOfMaxLoad)
        {
            if (hpa != null)
            {
                K8sHPAMetrics hpaMetrics = new()
                {
                    MaxLoad = hpa.GetMaxLoad(),
                    CurrentLoad = hpa.GetCurrentLoad(),

                    // Setting the burst header service name to namespace/deployment
                    Service = string.Format("{0}/{1}", hpa.Namespace(), hpa.Spec.ScaleTargetRef.Name),
                };

                // If calculated target load is zero (it can be since we are flooring MaxLoad)
                hpaMetrics.TargetLoad = (int?)Math.Floor(hpaMetrics.MaxLoad.GetValueOrDefault() * targetPercentOfMaxLoad);
                if (hpaMetrics.TargetLoad == 0)
                {
                    hpaMetrics.TargetLoad = hpaMetrics.MaxLoad;
                }

                return hpaMetrics;
            }

            return null;
        }

        public static int GetCurrentLoad(this V2HorizontalPodAutoscaler hpa)
        {
            // Check if we created HPA but but don't have a metrics server
            int currReplicas;
            if (hpa?.Status != null)
            {
                currReplicas = hpa.Status.CurrentReplicas.GetValueOrDefault();
            }
            else
            {
                throw new Exception("Cannot get HPA metrics because hpa is null");
            }

            return currReplicas;
        }

        public static int GetMaxLoad(this V2HorizontalPodAutoscaler hpa)
        {
            int maxReplicas;

            // Check if we created HPA but didn't set any Target
            if (hpa?.Spec != null)
            {
                maxReplicas = hpa.Spec.MaxReplicas;
            }
            else
            {
                throw new Exception("Cannot get HPA Spec because hpa is null");
            }

            return maxReplicas;
        }
    }
}
