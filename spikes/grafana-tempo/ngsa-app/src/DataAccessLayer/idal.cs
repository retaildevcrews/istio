// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Imdb.Model;
using Ngsa.Middleware;

namespace Ngsa.Application.DataAccessLayer
{
    /// <summary>
    /// Data Access Layer for CosmosDB Interface
    /// </summary>
    public interface IDAL
    {
        Task<Actor> GetActorAsync(string actorId);
        Task<IEnumerable<Actor>> GetActorsAsync(ActorQueryParameters actorQueryParameters);
        Task<IEnumerable<string>> GetGenresAsync();
        Task<Movie> GetMovieAsync(string movieId);
        Task<IEnumerable<Movie>> GetMoviesAsync(MovieQueryParameters movieQueryParameters);
        Task<List<string>> GetFeaturedMovieListAsync();
        Task DeleteMovieAsync(string movieId);
        Task<Movie> UpsertMovieAsync(Movie movie);
    }
}
