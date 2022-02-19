// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.Serialization;
namespace Ngsa.BurstService.K8sApi
{
    /// <summary>
    /// Supported HPA target object types.
    /// </summary>
    public enum K8sScaleTargetType
    {
        /// <summary>
        /// Represents a target reference of Deployment.
        /// </summary>
        [EnumMember(Value = "deployment")]
        Deployment,

        /// <summary>
        /// Represents an target reference of HPA.
        /// </summary>
        [EnumMember(Value = "hpa")]
        HPA,

        /// <summary>
        /// Represents a target reference of Service.
        /// </summary>
        [EnumMember(Value = "hpa")]
        Service,
    }
}
