// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Imdb.Model;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Microsoft.Azure.Cosmos;
using Ngsa.Middleware;

/// <summary>
/// This code is used to support performance testing
///
/// This loads the IMDb data into memory which removes the roundtrip to Cosmos
/// This provides higher performance and less variability which allows us to establish
/// baseline performance metrics
/// </summary>
namespace Ngsa.Application.DataAccessLayer
{
    public class InMemoryDal : IDAL
    {
        private const LuceneVersion Version = LuceneVersion.LUCENE_48;

        private const string ActorsSQL = "select m.id, m.partitionKey, m.actorId, m.type, m.name, m.birthYear, m.deathYear, m.profession, m.textSearch, m.movies from m where m.id in ({0}) order by m.textSearch ASC";
        private const string MoviesSQL = "select m.id, m.partitionKey, m.movieId, m.type, m.textSearch, m.title, m.year, m.runtime, m.rating, m.votes, m.totalScore, m.genres, m.roles from m where m.id in ({0}) order by m.textSearch ASC, m.movieId ASC";

        // benchmark results buffer
        private readonly string benchmarkData;
        private readonly List<string> genreList;

        // Lucene in-memory index
        private readonly IndexWriter writer = new (new RAMDirectory(), new IndexWriterConfig(Version, new StandardAnalyzer(Version)));

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryDal"/> class.
        /// </summary>
        public InMemoryDal()
        {
            JsonSerializerOptions jsonOptions = new ()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            // load data
            genreList = LoadGenres(jsonOptions);

            // temporary storage for upsert / delete
            MoviesIndex = new Dictionary<string, Movie>();

            // 16 bytes
            benchmarkData = "0123456789ABCDEF";

            // 1 MB
            while (benchmarkData.Length < 1024 * 1024)
            {
                benchmarkData += benchmarkData;
            }

            // load movies into Lucene index
            foreach (Movie movie in LoadMovies(jsonOptions))
            {
                writer.AddDocument(movie.ToDocument());
            }

            // load actors into Lucene index
            foreach (Actor a in LoadActors(jsonOptions))
            {
                writer.AddDocument(a.ToDocument());
            }

            // flush the writes to the index
            writer.Flush(true, true);
        }

        // used for upsert / delete
        public static Dictionary<string, Movie> MoviesIndex { get; set; }

        /// <summary>
        /// Get a single actor by ID
        /// </summary>
        /// <param name="actorId">ID</param>
        /// <returns>Actor object</returns>
        public async Task<Actor> GetActorAsync(string actorId)
        {
            return await Task.Run(() => { return GetActor(actorId); }).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a single actor by ID
        /// </summary>
        /// <param name="actorId">ID</param>
        /// <returns>Actor object</returns>
        public Actor GetActor(string actorId)
        {
            IndexSearcher searcher = new (writer.GetReader(true));

            // search by actorId
            TopDocs hits = searcher.Search(new PhraseQuery { new Term("actorId", actorId) }, 1);

            if (hits.TotalHits > 0)
            {
                // deserialize the json from the doc
                return JsonSerializer.Deserialize<Actor>(searcher.Doc(hits.ScoreDocs[0].Doc).GetBinaryValue("json").Bytes);
            }

            throw new CosmosException("Not Found", System.Net.HttpStatusCode.NotFound, 404, string.Empty, 0);
        }

        /// <summary>
        /// Get actors by search criteria
        /// </summary>
        /// <param name="actorQueryParameters">search criteria</param>
        /// <returns>List of Actor</returns>
        public Task<IEnumerable<Actor>> GetActorsAsync(ActorQueryParameters actorQueryParameters)
        {
            return Task<IEnumerable<Actor>>.Factory.StartNew(() =>
            {
                return GetActors(actorQueryParameters);
            });
        }

        /// <summary>
        /// Get actor IDs by search criteria
        /// </summary>
        /// <param name="actorQueryParameters">search criteria</param>
        /// <returns>Cosmos select statement</returns>
        public string GetActorIds(ActorQueryParameters actorQueryParameters)
        {
            List<Actor> list;

            if (actorQueryParameters == null)
            {
                list = GetActors(string.Empty, 0, 100);
            }
            else
            {
                list = GetActors(actorQueryParameters.Q, actorQueryParameters.GetOffset(), actorQueryParameters.PageSize);
            }

            string ids = string.Empty;

            if (list != null && list.Count > 0)
            {
                foreach (Actor a in list)
                {
                    ids += $"'{a.ActorId}',";
                }

                ids = ActorsSQL.Replace("{0}", ids[0..^1], StringComparison.Ordinal);
            }

            return ids;
        }

        /// <summary>
        /// Get actors by search criteria
        /// </summary>
        /// <param name="actorQueryParameters">search criteria</param>
        /// <returns>List of Actor</returns>
        public List<Actor> GetActors(ActorQueryParameters actorQueryParameters)
        {
            if (actorQueryParameters == null)
            {
                return GetActors(string.Empty, 0, 100);
            }

            return GetActors(actorQueryParameters.Q, actorQueryParameters.GetOffset(), actorQueryParameters.PageSize);
        }

        /// <summary>
        /// Worker function
        /// </summary>
        /// <param name="q">search query (optional)</param>
        /// <param name="offset">result offset</param>
        /// <param name="limit">page size</param>
        /// <returns>List of Actor</returns>
        public List<Actor> GetActors(string q, int offset = 0, int limit = 100)
        {
            List<Actor> res = new ();
            int start = 0;
            int end = limit;

            // compute start and end for paging
            if (offset > 0)
            {
                start = offset + limit;
                end = start + limit;
            }

            IndexSearcher searcher = new (writer.GetReader(true));

            // type == Actor
            BooleanQuery bq = new ()
            {
                { new PhraseQuery { new Term("type", "Actor") }, Occur.MUST },
            };

            // nameSort == name.ToLower()
            TopFieldCollector collector = TopFieldCollector.Create(new Sort(new SortField("nameSort", SortFieldType.STRING)), end, false, false, false, false);

            // add the search query
            if (!string.IsNullOrWhiteSpace(q))
            {
                bq.Add(new WildcardQuery(new Term("name", $"*{q.ToLowerInvariant()}*")), Occur.MUST);
            }

            // search
            searcher.Search(bq, collector);

            TopDocs results = collector.GetTopDocs();

            // check array bounds
            end = end <= results.ScoreDocs.Length ? end : results.ScoreDocs.Length;

            // deserialize each Actor
            for (int i = start; i < end; i++)
            {
                res.Add(JsonSerializer.Deserialize<Actor>(searcher.Doc(results.ScoreDocs[i].Doc).GetBinaryValue("json").Bytes));
            }

            return res;
        }

        /// <summary>
        /// Get list of featured Movie IDs
        /// </summary>
        /// <returns>List of IDs</returns>
        public Task<List<string>> GetFeaturedMovieListAsync()
        {
            return Task<List<string>>.Factory.StartNew(() =>
            {
                return GetFeaturedMovieList();
            });
        }

        /// <summary>
        /// Get list of featured Movie IDs
        /// </summary>
        /// <returns>List of IDs</returns>
        public List<string> GetFeaturedMovieList()
        {
            return new List<string>
            {
                "tt0133093",
                "tt0120737",
                "tt0167260",
                "tt0167261",
                "tt0372784",
                "tt0172495",
                "tt0317705",
            };
        }

        /// <summary>
        /// Get list of Genres
        /// </summary>
        /// <returns>List of Genres</returns>
        public async Task<IEnumerable<string>> GetGenresAsync()
        {
            return await Task<List<string>>.Factory.StartNew(() =>
            {
                return genreList;
            }).ConfigureAwait(false);
        }

        public async Task<string> GetBenchmarkDataAsync(int size)
        {
            return await Task<string>.Factory.StartNew(() =>
            {
                return benchmarkData[0..size];
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Get Movie by ID
        /// </summary>
        /// <param name="movieId">ID</param>
        /// <returns>Movie</returns>
        public async Task<Movie> GetMovieAsync(string movieId)
        {
            return await Task<Movie>.Factory.StartNew(() => { return GetMovie(movieId); }).ConfigureAwait(false);
        }

        /// <summary>
        /// Get Movie by ID
        /// </summary>
        /// <param name="movieId">ID</param>
        /// <returns>Movie</returns>
        public Movie GetMovie(string movieId)
        {
            if (movieId.StartsWith("tt"))
            {
                IndexSearcher searcher = new (writer.GetReader(true));

                // search by movieId
                TopDocs hits = searcher.Search(new PhraseQuery { new Term("movieId", movieId) }, 1);

                if (hits.TotalHits > 0)
                {
                    // deserialze the json from the index
                    return JsonSerializer.Deserialize<Movie>(searcher.Doc(hits.ScoreDocs[0].Doc).GetBinaryValue("json").Bytes);
                }
            }
            else
            {
                // handle the upserted movies
                if (MoviesIndex.ContainsKey(movieId))
                {
                    return MoviesIndex[movieId];
                }
            }

            throw new CosmosException("Not Found", System.Net.HttpStatusCode.NotFound, 404, string.Empty, 0);
        }

        /// <summary>
        /// Get Cosmos query string based on query parameters
        /// </summary>
        /// <param name="movieQueryParameters">query params</param>
        /// <returns>Cosmos query string</returns>
        public string GetMovieIds(MovieQueryParameters movieQueryParameters)
        {
            List<Movie> cache;
            string ids = string.Empty;

            if (movieQueryParameters == null)
            {
                cache = GetMovies(string.Empty, string.Empty, offset: 0, limit: 100);
            }
            else
            {
                cache = GetMovies(movieQueryParameters.Q, movieQueryParameters.Genre, movieQueryParameters.Year, movieQueryParameters.Rating, movieQueryParameters.ActorId, movieQueryParameters.GetOffset(), movieQueryParameters.PageSize);
            }

            foreach (Movie m in cache)
            {
                ids += $"'{m.Id}',";
            }

            // nothing found
            if (string.IsNullOrWhiteSpace(ids))
            {
                return string.Empty;
            }

            return MoviesSQL.Replace("{0}", ids[0..^1], StringComparison.Ordinal);
        }

        /// <summary>
        /// Get list of Movies based on query parameters
        /// </summary>
        /// <param name="movieQueryParameters">query params</param>
        /// <returns>List of Movie</returns>
        public List<Movie> GetMovies(MovieQueryParameters movieQueryParameters)
        {
            if (movieQueryParameters == null)
            {
                return GetMovies(string.Empty, string.Empty, offset: 0, limit: 100);
            }

            return GetMovies(movieQueryParameters.Q, movieQueryParameters.Genre, movieQueryParameters.Year, movieQueryParameters.Rating, movieQueryParameters.ActorId, movieQueryParameters.GetOffset(), movieQueryParameters.PageSize);
        }

        /// <summary>
        /// Get List of Movie by search params
        /// </summary>
        /// <param name="q">match title</param>
        /// <param name="genre">match genre</param>
        /// <param name="year">match year</param>
        /// <param name="rating">match rating</param>
        /// <param name="actorId">match Actor ID</param>
        /// <param name="offset">page offset</param>
        /// <param name="limit">page size</param>
        /// <returns>List of Movie</returns>
        public List<Movie> GetMovies(string q, string genre, int year = 0, double rating = 0.0, string actorId = "", int offset = 0, int limit = 100)
        {
            List<Movie> res = new ();

            int start = 0;
            int end = limit;

            // compute start and end for paging
            if (offset > 0)
            {
                start = offset + limit;
                end = start + limit;
            }

            IndexSearcher searcher = new (writer.GetReader(true));

            // type == Movie
            BooleanQuery bq = new ()
            {
                { new PhraseQuery { new Term("type", "Movie") }, Occur.MUST },
            };

            // titleSort == title.ToLower()
            TopFieldCollector collector = TopFieldCollector.Create(new Sort(new SortField("titleSort", SortFieldType.STRING)), end, false, false, false, false);

            // add the search term
            if (!string.IsNullOrWhiteSpace(q))
            {
                bq.Add(new WildcardQuery(new Term("title", $"*{q.ToLowerInvariant()}*")), Occur.MUST);
            }

            // add the actorId
            if (!string.IsNullOrWhiteSpace(actorId))
            {
                bq.Add(new PhraseQuery { new Term("role.actorId", actorId.ToLowerInvariant()) }, Occur.MUST);
            }

            // add the year
            if (year > 0)
            {
                bq.Add(NumericRangeQuery.NewInt32Range("year", year, year, true, true), Occur.MUST);
            }

            // add the genre
            if (!string.IsNullOrWhiteSpace(genre))
            {
                bq.Add(new PhraseQuery { new Term("genre", genre.ToLowerInvariant().Replace("-", string.Empty)) }, Occur.MUST);
            }

            // add the rating
            if (rating > 0)
            {
                bq.Add(NumericRangeQuery.NewDoubleRange("rating", rating, 100, true, true), Occur.MUST);
            }

            // run the search
            searcher.Search(bq, collector);

            TopDocs results = collector.GetTopDocs();

            // check array index bounds
            end = end <= results.ScoreDocs.Length ? end : results.ScoreDocs.Length;

            for (int i = start; i < end; i++)
            {
                // deserialze the json from the document
                res.Add(JsonSerializer.Deserialize<Movie>(searcher.Doc(results.ScoreDocs[i].Doc).GetBinaryValue("json").Bytes));
            }

            return res;
        }

        /// <summary>
        /// Get list of Movies based on query parameters
        /// </summary>
        /// <param name="movieQueryParameters">query params</param>
        /// <returns>List of Movie</returns>
        public Task<IEnumerable<Movie>> GetMoviesAsync(MovieQueryParameters movieQueryParameters)
        {
            return Task<IEnumerable<Movie>>.Factory.StartNew(() =>
            {
                return GetMovies(movieQueryParameters);
            });
        }

        /// <summary>
        /// Upsert a movie
        ///
        /// Do not store in index or WebV tests will break
        /// </summary>
        /// <param name="movie">Movie to upsert</param>
        /// <returns>Movie</returns>
        public async Task<Movie> UpsertMovieAsync(Movie movie)
        {
            await Task.Run(() =>
            {
                if (MoviesIndex.ContainsKey(movie.MovieId))
                {
                    movie = MoviesIndex[movie.MovieId];
                }
                else
                {
                    MoviesIndex.Add(movie.MovieId, movie);
                }
            }).ConfigureAwait(false);

            return movie;
        }

        /// <summary>
        /// Delete the movie from temporary storage
        /// </summary>
        /// <param name="movieId">Movie ID</param>
        /// <returns>void</returns>
        public async Task DeleteMovieAsync(string movieId)
        {
            await Task.Run(() =>
            {
                if (MoviesIndex.ContainsKey(movieId))
                {
                    MoviesIndex.Remove(movieId);
                }
            }).ConfigureAwait(false);
        }

        // load actor list from json file
        private static List<Actor> LoadActors(JsonSerializerOptions options)
        {
            // load the data from the json file
            return JsonSerializer.Deserialize<List<Actor>>(File.ReadAllText("src/data/actors.json"), options);
        }

        // load genre list from json file
        private static List<string> LoadGenres(JsonSerializerOptions options)
        {
            List<string> genres = new ();

            // load the data from the json file
            List<Genre> list = JsonSerializer.Deserialize<List<Genre>>(File.ReadAllText("src/data/genres.json"), options);

            // Convert Genre object to List<string> per API spec
            foreach (Genre g in list)
            {
                genres.Add(g.Name);
            }

            genres.Sort();

            return genres;
        }

        // load Movie List from json file
        private static List<Movie> LoadMovies(JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize<List<Movie>>(File.ReadAllText("src/data/movies.json"), options);
        }
    }
}
