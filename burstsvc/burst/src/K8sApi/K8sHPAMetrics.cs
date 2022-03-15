// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Ngsa.BurstService.K8sApi
{
    public class K8sHPAMetrics
    {
        public string Service { get; internal set; } = null;
        public int? CurrentLoad { get; internal set; } = null;
        public int? TargetLoad { get; internal set; } = null;
        public int? MaxLoad { get; internal set; } = null;
        public static implicit operator string(K8sHPAMetrics km) => km.ToString();

        public override string ToString()
        {
            return $"service={this.Service}, current-load={this.CurrentLoad}, target-load={this.TargetLoad}, max-load={this.MaxLoad}";
        }
    }
}
