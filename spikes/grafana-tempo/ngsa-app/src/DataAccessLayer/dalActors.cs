// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.Caching;
using System.Threading.Tasks;
using Imdb.Model;
using Microsoft.Azure.Cosmos;
using Ngsa.Middleware;

namespace Ngsa.Application.DataAccessLayer
{
    /// <summary>
    /// Data Access Layer for CosmosDB
    /// </summary>
    public partial class CosmosDal
    {
        /// <summary>
        /// Retrieve a single Actor from CosmosDB by actorId
        ///
        /// Uses the CosmosDB single document read API which is 1 RU if less than 1K doc size
        ///
        /// Throws an exception if not found
        /// </summary>
        /// <param name="actorId">Actor ID</param>
        /// <returns>Actor object</returns>
        public async Task<Actor> GetActorAsync(string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                throw new ArgumentNullException(nameof(actorId));
            }

            // get the partition key for the actor ID
            // note: if the key cannot be determined from the ID, ReadDocumentAsync cannot be used.
            // ComputePartitionKey will throw an ArgumentException if the actorId isn't valid
            // get an actor by ID

            string key = $"/api/actors/{actorId.ToUpperInvariant().Trim()}";

            if (App.Config.Cache && cache.Contains(key) && cache.Get(key) is Actor ac)
            {
                return ac;
            }

            Actor res = await cosmosDetails.Container.ReadItemAsync<Actor>(actorId, new PartitionKey(Actor.ComputePartitionKey(actorId))).ConfigureAwait(false);

            if (App.Config.Cache)
            {
                cache.Add(new CacheItem(key, res), cachePolicy);
            }

            return res;
        }

        /// <summary>
        /// Get a list of Actors by search string
        ///
        /// The search is a "contains" search on actor name
        /// If q is empty, all actors are returned
        /// </summary>
        /// <param name="actorQueryParameters">search parameters</param>
        /// <returns>List of Actors or an empty list</returns>
        public async Task<IEnumerable<Actor>> GetActorsAsync(ActorQueryParameters actorQueryParameters)
        {
            if (actorQueryParameters == null)
            {
                throw new ArgumentNullException(nameof(actorQueryParameters));
            }

            string key = actorQueryParameters.GetKey();

            if (App.Config.Cache && cache.Contains(key) && cache.Get(key) is List<Actor> ac)
            {
                return ac;
            }

            string sql = App.Config.CacheDal.GetActorIds(actorQueryParameters);
            List<Actor> res;

            if (!string.IsNullOrWhiteSpace(sql))
            {
                res = (List<Actor>)await InternalCosmosDBSqlQuery<Actor>(sql).ConfigureAwait(false);
            }
            else
            {
                res = new List<Actor>();
            }

            if (App.Config.Cache)
            {
                // add to cache
                cache.Add(new CacheItem(key, res), cachePolicy);
            }

            return res;
        }
    }
}
