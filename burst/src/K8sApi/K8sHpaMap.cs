// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using HPADictionary = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, k8s.Models.V2beta2HorizontalPodAutoscaler>>;
using K8sHPAObj = k8s.Models.V2beta2HorizontalPodAutoscaler;

namespace Ngsa.BurstService.K8sApi
{
    public sealed class K8sHPAMap
    {
        private HPADictionary hpaMap;
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
        /// <param name="nsDeploy">Tuple: First Item namespace, second deployment name</param>
        /// <returns>V2beta2HorizontalPodAutoscaler </returns>
        public K8sHPAObj this[ValueTuple<string, string> nsDeploy] => GetHPA(nsDeploy.Item1, nsDeploy.Item2);

        /// <summary>
        /// Gets if Returns if HPA Map is empty.
        /// </summary>
        /// <returns>If no HPA exists</returns>
        public bool IsEmpty()
        {
            return hpaMap == null || hpaMap.Count == 0;
        }

        /// <summary>
        /// Checks if HPA exists in a namespace.
        /// </summary>
        /// <param name="ns">Namespace</param>
        /// <param name="name">Name of deployment</param>
        /// <returns>Whether the HPA exists</returns>
        public bool Exists(string ns, string name)
        {
            return GetHPA(ns, name) != null;
        }

        /// <summary>
        /// Gets HPA in a namespace.
        /// An indexer can also be used instead of this function.
        /// </summary>
        /// <param name="ns">Namespace</param>
        /// <param name="name">Name of deployment</param>
        /// <returns>The HPA object</returns>
        public K8sHPAObj GetHPA(string ns, string name)
        {
            if (hpaMap?.TryGetValue(ns, out Dictionary<string, K8sHPAObj> hpaDict) is true
                && hpaDict?.TryGetValue(name, out K8sHPAObj targetHpa) is true)
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
            // Iterate through the list and map HPA
            foreach (K8sHPAObj hpa in v2HpaList.Items)
            {
                newHpaMap[hpa.Namespace()] = new Dictionary<string, K8sHPAObj>() { { hpa.Name(), hpa } };
            }

            // Store the hpaMap and hpaList
            if (hpaList == null)
            {
                hpaMap = newHpaMap;
                hpaList = v2HpaList;
            }
            else
            {
                // Exchange the new dictionary with the old one
                // Do interlocking exchange in case it is being used by different thread
                // TODO: Might be unncessary to use InterLocking
                Interlocked.Exchange(ref hpaMap, newHpaMap);
                Interlocked.Exchange(ref hpaList, v2HpaList);
            }
        }
    }
}
