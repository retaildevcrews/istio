// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using System;
using System.Collections.Generic;
using System.Threading;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;

using HPADictionary = System.Collections.Generic.Dictionary<string, Ngsa.BurstService.K8sApi.K8sHPAMetrics>;
using K8sHPAObj = k8s.Models.V2beta2HorizontalPodAutoscaler;

namespace Ngsa.BurstService.K8sApi
{
    /// <summary>
    /// Handle all /burstmetrics requests.
    /// </summary>
    public sealed class K8sHPAMap
    {
        private const double TargetPercent = 0.8;
        private const string KeyFormat = "{0}/{1}";
        private readonly ILogger logger;
        private HPADictionary nsHPAMap;
        private HPADictionary nsDeploymentMap;
        private HPADictionary nsServiceMap;
        private V2beta2HorizontalPodAutoscalerList hpaList;

        /// <summary>
        /// Initializes a new instance of the <see cref="K8sHPAMap"/> class.
        /// </summary>
        /// <param name="logger">Logging object</param>
        public K8sHPAMap(ILogger logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Gets HPA as List.
        /// It doesn't convert hpa dictionary to list.
        /// Rather it returns original HPA list returned by the K8s sdk.
        /// </summary>
        /// <returns>List of HPAs if any</returns>
        public IList<K8sHPAObj> AsList => hpaList?.Items;

        /// <summary>
        /// Indexer returning K8sHPAMetrics Object
        /// </summary>
        /// <param name="nsDeploy">Tuple: First Item target type, 2nd namespace, 3rd deployment name</param>
        /// <returns>K8sHPAMetrics</returns>
        public K8sHPAMetrics this[ValueTuple<K8sScaleTargetType, string, string> nsDeploy] => GetHPA(nsDeploy.Item1, nsDeploy.Item2, nsDeploy.Item3);

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
        /// <returns>The HPA Metrics object</returns>
        public K8sHPAMetrics GetHPA(K8sScaleTargetType target, string ns, string targetName)
        {
            var targetDict = target switch
            {
                K8sScaleTargetType.Deployment => nsDeploymentMap,
                K8sScaleTargetType.HPA => nsHPAMap,
                K8sScaleTargetType.Service => nsServiceMap,
                _ => null,

            };
            if (targetDict != null && targetDict.TryGetValue(CreateDictKey(ns, targetName), out var targetHpa))
            {
                return targetHpa;
            }

            return null;
        }

        /// <summary>
        /// Gets HPA dictionary for a specified HPA target type.
        /// </summary>
        /// <param name="target">HPA Target type</param>
        /// <returns>The HPADictionary object</returns>
        public HPADictionary GetHPADictionary(K8sScaleTargetType target)
        {
            var targetDict = target switch
            {
                K8sScaleTargetType.Deployment => nsDeploymentMap,
                K8sScaleTargetType.HPA => nsHPAMap,
                K8sScaleTargetType.Service => nsServiceMap,
                _ => null,

            };

            return targetDict;
        }

        /// <summary>
        /// Parses the K8s HPA List and maps them.
        /// Might throw exception, hence its upto the caller to handle exceptions.
        /// </summary>
        /// <param name="k8sClient">List of HPA from K8s Client SDK</param>
        /// <param name="target">HPA's target scale object</param>
        /// <param name="selectorLabels">Labels to match the pods and service</param>
        public void CreateHPAMap(IKubernetes k8sClient, K8sScaleTargetType target, IReadOnlyList<string> selectorLabels)
        {
            var v2HpaList = k8sClient.ListHorizontalPodAutoscalerForAllNamespaces3(timeoutSeconds: 1);
            HPADictionary newHpaMap = new();
            HPADictionary newDeploymentMap = new();
            HPADictionary newSvcMap = new();

            // Iterate through the list and map HPA
            foreach (K8sHPAObj hpa in v2HpaList.Items)
            {
                // Since we can have multiple HPAs in one namespace
                // But one HPA is associated with one scaleobject
                newHpaMap[CreateDictKey(hpa.Namespace(), hpa.Name())] = hpa.ToHPAMetrics(TargetPercent);

                // Check if the Scale target is the same as target (set in appsettings)
                // TODO: Explore different scaleTargetRef or make target = deployment as constant
                if (Enum.TryParse(hpa.Spec.ScaleTargetRef.Kind, true, out K8sScaleTargetType parsed) && parsed == target)
                {
                    // We have 1-to-1 relationship with HPA and ScaleObject
                    newDeploymentMap[CreateDictKey(hpa.Namespace(), hpa.Spec.ScaleTargetRef.Name)] = hpa.ToHPAMetrics(TargetPercent);

                    // Now map the service to an deployment/hpa
                    try
                    {
                        // Very unlikely that the HPA will have a non existing deployment target
                        // But if it does, this try-catch will catch ReadNamespacedDeployment
                        var deploy = k8sClient.ReadNamespacedDeployment(hpa.Spec.ScaleTargetRef.Name, hpa.Namespace());
                        var podLabels = deploy?.Spec.Template.Metadata.Labels;
                        if (podLabels != null && selectorLabels != null)
                        {
                            string labelSelector = string.Empty;
                            foreach (var item in selectorLabels)
                            {
                                if (podLabels.TryGetValue(item, out string labelValue))
                                {
                                    labelSelector += string.Format("{0}={1},", item, labelValue);
                                }
                            }

                            if (labelSelector != string.Empty)
                            {
                                // Remove the last comma
                                labelSelector = labelSelector.Remove(labelSelector.Length - 1);
                            }

                            var svcList = k8sClient.ListNamespacedService(hpa.Namespace(), labelSelector: labelSelector);
                            foreach (var svc in svcList?.Items)
                            {
                                newSvcMap[CreateDictKey(svc.Namespace(), svc.Name())] = hpa.ToHPAMetrics(TargetPercent);
                            }
                        }
                        else
                        {
                            logger?.LogWarning("Deployment pod label or target label is empty");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning("Exception while mapping deployment and service. Exception: {}", ex.Message);
                    }
                }
            }

            // Store the nsHPAMap and hpaList
            if (hpaList == null)
            {
                nsHPAMap = newHpaMap;
                nsDeploymentMap = newDeploymentMap;
                nsServiceMap = newSvcMap;
                hpaList = v2HpaList;
            }
            else
            {
                // Exchange the new dictionary with the old one
                // Do interlocking exchange in case it is being used by different thread
                // TODO: Might be unncessary to use InterLocking
                Interlocked.Exchange(ref nsHPAMap, newHpaMap);
                Interlocked.Exchange(ref nsDeploymentMap, newDeploymentMap);
                Interlocked.Exchange(ref nsServiceMap, newSvcMap);
                Interlocked.Exchange(ref hpaList, v2HpaList);
            }
        }

        private static string CreateDictKey(string ns, string deployment)
        {
            return string.Format(KeyFormat, ns, deployment);
        }
    }
}
