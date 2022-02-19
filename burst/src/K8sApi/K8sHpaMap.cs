// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using System;
using System.Collections.Generic;
using System.Threading;
using k8s.Models;

using HPADictionary = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, k8s.Models.V2beta2HorizontalPodAutoscaler>>;
using K8sHPAObj = k8s.Models.V2beta2HorizontalPodAutoscaler;

namespace Ngsa.BurstService.K8sApi
{
    /// <summary>
    /// Handle all /burstmetrics requests.
    /// </summary>
    public sealed class K8sHPAMap
    {
        private HPADictionary nsHPAMap;
        private HPADictionary nsDeploymentMap;
        private V2beta2HorizontalPodAutoscalerList hpaList;

        /// <summary>
        /// Gets HPA as List.
        /// It doesn't convert hpa dictionary to list.
        /// Rather it returns original HPA list returned by the K8s sdk.
        /// </summary>
        /// <returns>List of HPAs if any</returns>
        public IList<K8sHPAObj> AsList => hpaList?.Items;

        /// <summary>
        /// Indexer returning K8s HPA Object
        /// </summary>
        /// <param name="nsDeploy">Tuple: First Item target type, 2nd namespace, 3rd deployment name</param>
        /// <returns>V2beta2HorizontalPodAutoscaler</returns>
        public K8sHPAObj this[ValueTuple<K8sScaleTargetType, string, string> nsDeploy] => GetHPA(nsDeploy.Item1, nsDeploy.Item2, nsDeploy.Item3);

        /// <summary>
        /// Gets if Returns if HPA Map is empty.
        /// </summary>
        /// <returns>If no HPA exists</returns>
        public bool IsEmpty()
        {
            return nsHPAMap == null || nsHPAMap.Count == 0;
        }

        /// <summary>
        /// Checks if HPA exists in a namespace.
        /// </summary>
        /// <param name="target">HPA Target type</param>
        /// <param name="ns">Namespace</param>
        /// <param name="name">Name of deployment</param>
        /// <returns>Whether the HPA exists</returns>
        public bool Exists(K8sScaleTargetType target, string ns, string name)
        {
            return GetHPA(target, ns, name) != null;
        }

        /// <summary>
        /// Gets HPA in a namespace.
        /// An indexer can also be used instead of this function.
        /// </summary>
        /// <param name="target">HPA Target type</param>
        /// <param name="ns">Namespace</param>
        /// <param name="targetName">Name of deployment</param>
        /// <returns>The HPA object</returns>
        public K8sHPAObj GetHPA(K8sScaleTargetType target, string ns, string targetName)
        {
            var targetDict = target switch
            {
                K8sScaleTargetType.Deployment => nsDeploymentMap,
                K8sScaleTargetType.HPA => nsHPAMap,
                _ => null,

            };
            if (targetDict?.TryGetValue(ns, out Dictionary<string, K8sHPAObj> hpaDict) is true
                && hpaDict?.TryGetValue(targetName, out K8sHPAObj targetHpa) is true)
            {
                return targetHpa;
            }

            return null;
        }

        /// <summary>
        /// Parses the K8s HPA List and maps them.
        /// Might throw exception, hence its upto the caller to handle exceptions.
        /// </summary>
        /// <param name="v2HpaList">List of HPA from K8s Client SDK</param>
        public void ParseHPAList(V2beta2HorizontalPodAutoscalerList v2HpaList)
        {
            var newHpaMap = new HPADictionary();
            var newDeploymentMap = new HPADictionary();

            // Iterate through the list and map HPA
            foreach (K8sHPAObj hpa in v2HpaList.Items)
            {
                newHpaMap[hpa.Namespace()] = new () { { hpa.Name(), hpa } };
                if (hpa.Spec.ScaleTargetRef.Kind == "Deployment")
                {
                    // TODO: move the const "Deployment" to appsettings.json
                    // If the target is a Deployment then add it to deploymentMap
                    newDeploymentMap[hpa.Namespace()] = new () { { hpa.Spec.ScaleTargetRef.Name, hpa } };
                }
            }

            // Store the nsHPAMap and hpaList
            if (hpaList == null)
            {
                nsHPAMap = newHpaMap;
                nsDeploymentMap = newDeploymentMap;
                hpaList = v2HpaList;
            }
            else
            {
                // Exchange the new dictionary with the old one
                // Do interlocking exchange in case it is being used by different thread
                // TODO: Might be unncessary to use InterLocking
                Interlocked.Exchange(ref nsHPAMap, newHpaMap);
                Interlocked.Exchange(ref nsDeploymentMap, newDeploymentMap);
                Interlocked.Exchange(ref hpaList, v2HpaList);
            }
        }
    }
}
