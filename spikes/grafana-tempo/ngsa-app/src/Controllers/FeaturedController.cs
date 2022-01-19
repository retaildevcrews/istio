// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Imdb.Model;
using Microsoft.AspNetCore.Mvc;
using Ngsa.Application.DataAccessLayer;
using Ngsa.Middleware;

namespace Ngsa.Application.Controllers
{
    /// <summary>
    /// Handle /api/featured/movie requests
    /// </summary>
    [Route("api/[controller]")]
    public class FeaturedController : Controller
    {
        private static readonly NgsaLog Logger = new ()
        {
            Name = typeof(FeaturedController).FullName,
            ErrorMessage = "FeaturedControllerException",
            NotFoundError = "Movie Not Found",
        };

        private readonly IDAL dal;

        /// <summary>
        /// Initializes a new instance of the <see cref="FeaturedController"/> class.
        /// </summary>
        /// <param name="dal">data access layer instance</param>
        public FeaturedController()
        {
            dal = App.Config.CosmosDal;
        }

        /// <summary>
        /// Returns a random movie from the featured movie list as a JSON Movie
        /// </summary>
        /// <response code="200">OK</response>
        /// <returns>IActionResult</returns>
        [HttpGet("movie")]
        public async Task<IActionResult> GetFeaturedMovieAsync()
        {
            IActionResult res;

            if (App.Config.AppType == AppType.WebAPI)
            {
                res = await DataService.Read<Movie>(Request).ConfigureAwait(false);
            }
            else
            {
                List<string> featuredMovies = await App.Config.CacheDal.GetFeaturedMovieListAsync().ConfigureAwait(false);

                if (featuredMovies != null && featuredMovies.Count > 0)
                {
                    // get random featured movie by movieId
                    string movieId = featuredMovies[DateTime.UtcNow.Millisecond % featuredMovies.Count];

                    // get movie by movieId
                    res = await ResultHandler.Handle(dal.GetMovieAsync(movieId), Logger).ConfigureAwait(false);
                }
                else
                {
                    return NotFound();
                }
            }

            return res;
        }
    }
}
