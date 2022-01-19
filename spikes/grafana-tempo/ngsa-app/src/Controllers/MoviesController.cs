// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Imdb.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Ngsa.Application.DataAccessLayer;
using Ngsa.Middleware;

namespace Ngsa.Application.Controllers
{
    /// <summary>
    /// Handle all of the /api/movies requests
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class MoviesController : Controller
    {
        private static readonly NgsaLog Logger = new ()
        {
            Name = typeof(MoviesController).FullName,
            ErrorMessage = "MovieControllerException",
            NotFoundError = "Movie Not Found",
        };

        private readonly IDAL dal;

        /// <summary>
        /// Initializes a new instance of the <see cref="MoviesController"/> class.
        /// </summary>
        public MoviesController()
        {
            dal = App.Config.CosmosDal;
        }

        /// <summary>
        /// Returns a JSON array of Movie objects
        /// </summary>
        /// <param name="movieQueryParameters">query parameters</param>
        /// <returns>IActionResult</returns>
        [HttpGet]
        public async Task<IActionResult> GetMoviesAsync([FromQuery] MovieQueryParameters movieQueryParameters)
        {
            if (movieQueryParameters == null)
            {
                throw new ArgumentNullException(nameof(movieQueryParameters));
            }

            List<Middleware.Validation.ValidationError> list = movieQueryParameters.Validate();

            if (list.Count > 0)
            {
                Logger.LogWarning(nameof(GetMoviesAsync), NgsaLog.MessageInvalidQueryString, NgsaLog.LogEvent400, HttpContext);

                return ResultHandler.CreateResult(list, RequestLogger.GetPathAndQuerystring(Request));
            }

            IActionResult res;

            if (App.Config.AppType == AppType.WebAPI)
            {
                res = await DataService.Read<List<Movie>>(Request).ConfigureAwait(false);
            }
            else
            {
                // get the result
                res = await ResultHandler.Handle(dal.GetMoviesAsync(movieQueryParameters), Logger).ConfigureAwait(false);
            }

            return res;
        }

        /// <summary>
        /// Returns a single JSON Movie by movieIdParameter
        /// </summary>
        /// <param name="movieId">Movie ID</param>
        /// <returns>IActionResult</returns>
        [HttpGet("{movieId}")]
        public async Task<IActionResult> GetMovieByIdAsync([FromRoute] string movieId)
        {
            if (string.IsNullOrWhiteSpace(movieId))
            {
                throw new ArgumentNullException(nameof(movieId));
            }

            List<Middleware.Validation.ValidationError> list = MovieQueryParameters.ValidateMovieId(movieId);

            if (list.Count > 0)
            {
                Logger.LogWarning(nameof(GetMoviesAsync), "Invalid Movie Id", NgsaLog.LogEvent400, HttpContext);

                return ResultHandler.CreateResult(list, RequestLogger.GetPathAndQuerystring(Request));
            }

            IActionResult res;

            if (App.Config.AppType == AppType.WebAPI)
            {
                res = await DataService.Read<Movie>(Request).ConfigureAwait(false);
            }
            else
            {
                res = await ResultHandler.Handle(dal.GetMovieAsync(movieId), Logger).ConfigureAwait(false);
            }

            return res;
        }

        [HttpPut("{movieId}")]
        public async Task<IActionResult> UpsertMovieAsync([FromRoute] string movieId)
        {
            try
            {
                List<Middleware.Validation.ValidationError> list = MovieQueryParameters.ValidateMovieId(movieId);

                if (list.Count > 0 || !movieId.StartsWith("zz"))
                {
                    Logger.LogWarning(nameof(UpsertMovieAsync), "Invalid Movie Id", NgsaLog.LogEvent400, HttpContext);

                    return ResultHandler.CreateResult(list, RequestLogger.GetPathAndQuerystring(Request));
                }

                // duplicate the movie for upsert
                Movie mOrig = App.Config.CacheDal.GetMovie(movieId.Replace("zz", "tt"));
                Movie m = mOrig.DuplicateForUpsert();

                IActionResult res;

                if (App.Config.AppType == AppType.WebAPI)
                {
                    res = await DataService.Post(Request, m).ConfigureAwait(false);
                }
                else
                {
                    await App.Config.CacheDal.UpsertMovieAsync(m);

                    // upsert into Cosmos
                    if (!App.Config.InMemory)
                    {
                        try
                        {
                            await App.Config.CosmosDal.UpsertMovieAsync(m).ConfigureAwait(false);
                        }
                        catch (CosmosException ce)
                        {
                            Logger.LogError("UpsertMovieAsync", ce.ActivityId, new LogEventId((int)ce.StatusCode, "CosmosException"), ex: ce);

                            return ResultHandler.CreateResult(Logger.ErrorMessage, ce.StatusCode);
                        }
                        catch (Exception ex)
                        {
                            // log and return 500
                            Logger.LogError("UpsertMovieAsync", "Exception", NgsaLog.LogEvent500, ex: ex);
                            return ResultHandler.CreateResult("Internal Server Error", HttpStatusCode.InternalServerError);
                        }
                    }

                    res = Ok(m);
                }

                return res;
            }
            catch
            {
                return NotFound($"Movie ID Not Found: {movieId}");
            }
        }

        /// <summary>
        /// Delete a movie by movieId
        /// </summary>
        /// <param name="movieId">ID to delete</param>
        /// <returns>IActionResult</returns>
        [HttpDelete("{movieId}")]
        public async Task<IActionResult> DeleteMovieAsync([FromRoute] string movieId)
        {
            List<Middleware.Validation.ValidationError> list = MovieQueryParameters.ValidateMovieId(movieId);

            if (list.Count > 0 || !movieId.StartsWith("zz"))
            {
                Logger.LogWarning(nameof(UpsertMovieAsync), "Invalid Movie Id", NgsaLog.LogEvent400, HttpContext);

                return ResultHandler.CreateResult(list, RequestLogger.GetPathAndQuerystring(Request));
            }

            IActionResult res;

            if (App.Config.AppType == AppType.WebAPI)
            {
                res = await DataService.Delete(Request).ConfigureAwait(false);
            }
            else
            {
                await App.Config.CacheDal.DeleteMovieAsync(movieId);
                res = NoContent();

                if (!App.Config.InMemory)
                {
                    try
                    {
                        // Delete from Cosmos
                        await App.Config.CosmosDal.DeleteMovieAsync(movieId).ConfigureAwait(false);
                    }
                    catch (CosmosException ce)
                    {
                        // log and return Cosmos status code
                        if (ce.StatusCode != HttpStatusCode.NotFound)
                        {
                            Logger.LogError("DeleteMovieAsync", ce.ActivityId, new LogEventId((int)ce.StatusCode, "CosmosException"), ex: ce);
                            return ResultHandler.CreateResult(Logger.ErrorMessage, ce.StatusCode);
                        }
                    }
                    catch (Exception ex)
                    {
                        // log and return 500
                        Logger.LogError("DeleteMovieAsync", "Exception", NgsaLog.LogEvent500, ex: ex);
                        return ResultHandler.CreateResult("Internal Server Error", HttpStatusCode.InternalServerError);
                    }
                }
            }

            return res;
        }
    }
}
