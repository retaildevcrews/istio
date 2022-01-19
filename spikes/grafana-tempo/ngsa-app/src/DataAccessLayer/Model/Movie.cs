// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Lucene.Net.Documents;
using static Lucene.Net.Documents.Field;

namespace Imdb.Model
{
    public class Movie : ICloneable
    {
        public string Id { get; set; }
        public string PartitionKey { get; set; }
        public string MovieId { get; set; }
        public string Type { get; set; }
        public string Title { get; set; }
        public int Year { get; set; }
        public int Runtime { get; set; }
        public double Rating { get; set; }
        public long Votes { get; set; }
        public long TotalScore { get; set; }
        public string TextSearch { get; set; }
        public List<string> Genres { get; set; }
        public List<Role> Roles { get; set; }

        /// <summary>
        /// Compute the partition key based on the movieId or actorId
        ///
        /// For this sample, the partitionkey is the id mod 10
        ///
        /// In a full implementation, you would update the logic to determine the partition key
        /// </summary>
        /// <param name="id">document id</param>
        /// <returns>the partition key</returns>
        public static string ComputePartitionKey(string id)
        {
            // validate id
            if (!string.IsNullOrWhiteSpace(id) &&
                id.Length > 5 &&
                (id.StartsWith("tt", StringComparison.OrdinalIgnoreCase) ||
                 id.StartsWith("zz", StringComparison.OrdinalIgnoreCase)) &&
                int.TryParse(id[2..], out int idInt))
            {
                return (idInt % 10).ToString(CultureInfo.InvariantCulture);
            }

            throw new ArgumentException("Invalid Partition Key");
        }

        /// <summary>
        /// Comparer for sort by Title
        /// </summary>
        /// <param name="x">first comparison</param>
        /// <param name="y">second comparison</param>
        /// <returns>int sort order</returns>
        public static int TitleCompare(Movie x, Movie y)
        {
            int result;

            result = string.Compare(x?.Title, y?.Title, StringComparison.OrdinalIgnoreCase);

            if (result == 0)
            {
                return string.Compare(y.Id, y.Id, StringComparison.OrdinalIgnoreCase);
            }

            return result;
        }

        /// <summary>
        /// Duplicate this movie for upsert testing
        /// </summary>
        /// <returns>Movie</returns>
        public Movie DuplicateForUpsert()
        {
            Movie m = (Movie)MemberwiseClone();

            m.MovieId = m.MovieId.Replace("tt", "zz");
            m.Id = m.MovieId;
            m.Type = "Movie-Dupe";

            return m;
        }

        /// <summary>
        /// IClonable::Clone
        /// </summary>
        /// <returns>Movie as object</returns>
        public object Clone()
        {
            return MemberwiseClone();
        }

        /// <summary>
        /// Get the Movie as a content string for indexing
        /// </summary>
        /// <returns>string</returns>
        public string GetContent()
        {
            string content = $"{MovieId} {Title} {Year} ";

            if (Genres != null && Genres.Count > 0)
            {
                foreach (string g in Genres)
                {
                    content += $"{g} ";
                }
            }

            if (Roles != null && Roles.Count > 0)
            {
                foreach (Role r in Roles)
                {
                    content += $"{r.ActorId} {r.Name} ";

                    if (r.Characters != null && r.Characters.Count > 0)
                    {
                        foreach (string c in r.Characters)
                        {
                            content += $"{c} ";
                        }
                    }
                }
            }

            return content.Trim();
        }

        /// <summary>
        /// Convert the Movie to a Lucene Document for indexing
        /// </summary>
        /// <returns>Lucene Document</returns>
        public Document ToDocument()
        {
            Document doc = new ()
                {
                    new StringField("id", Id, Store.YES),
                    new Int32Field("partitionKey", int.Parse(PartitionKey), Store.YES),
                    new StringField("type", Type, Store.YES),
                    new StringField("movieId", MovieId, Store.YES),
                    new TextField("title", Title, Store.YES),
                    new StringField("titleSort", Title.ToLowerInvariant(), Store.YES),
                    new Int32Field("runtime", Runtime, Store.YES),
                    new Int64Field("totalScore", TotalScore, Store.YES),
                    new Int64Field("votes", Votes, Store.YES),
                    new DoubleField("rating", Rating, Store.YES),
                    new Int32Field("year", Year, Store.YES),
                };

            if (Genres != null && Genres.Count > 0)
            {
                foreach (string g in Genres)
                {
                    doc.Add(new TextField("genre", g.Replace("-", string.Empty), Store.YES));
                }
            }

            if (Roles != null && Roles.Count > 0)
            {
                foreach (Role r in Roles)
                {
                    doc.Add(new StringField("role.actorId", r.ActorId, Store.YES));
                    doc.Add(new TextField("role.name", r.Name, Store.YES));
                    doc.Add(new StringField("role.category", r.Category, Store.YES));

                    if (r.BirthYear != null && r.BirthYear > 0)
                    {
                        doc.Add(new Int32Field("role.birthYear", (int)r.BirthYear, Store.YES));

                        if (r.DeathYear != null && r.DeathYear != 0 && r.DeathYear > r.BirthYear)
                        {
                            doc.Add(new Int32Field("role.deathYear", (int)r.DeathYear, Store.YES));
                        }
                    }

                    if (r.Characters != null && r.Characters.Count > 0)
                    {
                        foreach (string c in r.Characters)
                        {
                            doc.Add(new TextField("role.character", c, Store.YES));
                        }
                    }
                }
            }

            doc.Add(new TextField("content", GetContent(), Store.NO));
            doc.Add(new StoredField("json", JsonSerializer.SerializeToUtf8Bytes<Movie>(this)));

            return doc;
        }
    }
}
