// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ngsa.Application.DataAccessLayer
{
    /// <summary>
    /// Data Access Layer for CosmosDB
    /// </summary>
    public partial class CosmosDal
    {
        /// <summary>
        /// Read the genres from cache
        /// </summary>
        /// <returns>List of strings</returns>
        public async Task<IEnumerable<string>> GetGenresAsync()
        {
            return await App.Config.CacheDal.GetGenresAsync().ConfigureAwait(false);
        }
    }
}
