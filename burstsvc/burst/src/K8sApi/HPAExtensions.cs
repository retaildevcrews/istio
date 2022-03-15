// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using k8s.Models;

namespace Ngsa.BurstService.K8sApi
{
    public static class HPAExtensions
    {
        public static K8sHPAMetrics ToHPAMetrics(this V2beta2HorizontalPodAutoscaler hpa, double targetPercentOfMaxLoad)
        {
            if (hpa != null)
            {
                K8sHPAMetrics hpaMetrics = new ();

                // Get the Target CPU load
                hpaMetrics.MaxLoad = hpa.GetMaxLoad();

                // Get the current CPU load
                hpaMetrics.CurrentLoad = hpa.GetCurrentLoad();
                hpaMetrics.TargetLoad = (int?)Math.Floor(hpaMetrics.MaxLoad.GetValueOrDefault() * targetPercentOfMaxLoad);

                // Setting the burst header service name to namespace/deployment
                hpaMetrics.Service = string.Format("{0}/{1}", hpa.Namespace(), hpa.Spec.ScaleTargetRef.Name);

                // If calculated target load is zero (it can be since we are flooring MaxLoad)
                if (hpaMetrics.TargetLoad == 0)
                {
                    hpaMetrics.TargetLoad = hpaMetrics.MaxLoad;
                }

                return hpaMetrics;
            }

            return null;
        }

        public static int GetCurrentLoad(this V2beta2HorizontalPodAutoscaler hpa)
        {
            // Check if we created HPA but but don't have a metrics server
            int currReplicas;
            if (hpa?.Status != null)
            {
                currReplicas = hpa.Status.CurrentReplicas;
            }
            else
            {
                throw new Exception("Cannot get HPA metrics because hpa is null");
            }

            return currReplicas;
        }

        public static int GetMaxLoad(this V2beta2HorizontalPodAutoscaler hpa)
        {
            int maxReplicas;

            // Check if we created HPA but didn't set any CPU Target
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
