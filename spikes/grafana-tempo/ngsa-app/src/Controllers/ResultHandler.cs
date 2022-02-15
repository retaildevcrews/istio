// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Ngsa.Middleware;
using Ngsa.Middleware.Validation;

namespace Ngsa.Application.Controllers
{
    /// <summary>
    /// Handles query requests from the controllers
    /// </summary>
    public static class ResultHandler
    {
        /// <summary>
        /// Handle an IActionResult request from a controller
        /// </summary>
        /// <typeparam name="T">type of result</typeparam>
        /// <param name="task">async task (usually the Cosmos query)</param>
        /// <param name="logger">NgsaLog</param>
        /// <returns>IActionResult</returns>
        public static async Task<IActionResult> Handle<T>(Task<T> task, NgsaLog logger)
        {
            // log the request
            logger.LogInformation("Handle<T>", "DS request");

            // return exception if task is null
            if (task == null)
            {
                logger.LogError("Handle<T>", "Exception: task is null", NgsaLog.LogEvent500, ex: new ArgumentNullException(nameof(task)));

                return CreateResult(logger.ErrorMessage, HttpStatusCode.InternalServerError);
            }

            try
            {
                // return an OK object result
                return new OkObjectResult(await task.ConfigureAwait(false));
            }
            catch (CosmosException ce)
            {
                // log and return Cosmos status code
                if (ce.StatusCode == HttpStatusCode.NotFound)
                {
                    logger.LogWarning("Handle<T>", logger.NotFoundError, new LogEventId((int)ce.StatusCode, string.Empty));
                    return CreateResult(logger.NotFoundError, ce.StatusCode);
                }

                logger.LogError("Handle<T>", ce.ActivityId, new LogEventId((int)ce.StatusCode, "CosmosException"), ex: ce);

                return CreateResult(logger.ErrorMessage, ce.StatusCode);
            }
            catch (Exception ex)
            {
                // log and return exception
                logger.LogError("Handle<T>", "Exception", NgsaLog.LogEvent500, ex: ex);

                // return 500 error
                return CreateResult("Internal Server Error", HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// ContentResult factory
        /// </summary>
        /// <param name="message">string</param>
        /// <param name="statusCode">int</param>
        /// <returns>JsonResult</returns>
        public static JsonResult CreateResult(string message, HttpStatusCode statusCode)
        {
            JsonResult res = new (new ErrorResult { Error = statusCode, Message = message })
            {
                StatusCode = (int)statusCode,
            };

            return res;
        }

        public static JsonResult CreateResult(List<ValidationError> errorList, string path)
        {
            Dictionary<string, object> data = new ()
            {
                { "type", ValidationError.GetErrorLink(path) },
                { "title", "Parameter validation error" },
                { "detail", "One or more invalid parameters were specified." },
                { "status", (int)HttpStatusCode.BadRequest },
                { "instance", path },
                { "validationErrors", errorList },
            };

            JsonResult res = new (data)
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                ContentType = "application/problem+json",
            };

            return res;
        }
    }
}
