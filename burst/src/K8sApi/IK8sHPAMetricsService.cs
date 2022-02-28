// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using HPADictionary = System.Collections.Generic.Dictionary<string, Ngsa.BurstService.K8sApi.K8sHPAMetrics>;
namespace Ngsa.BurstService.K8sApi
{
    public interface IK8sHPAMetricsService
    {
        HPADictionary GetBulkK8SHPAMetrics(K8sScaleTargetType target);
        K8sHPAMetrics GetK8SHPAMetrics(K8sScaleTargetType target, string ns, string deployment);
    }
}
