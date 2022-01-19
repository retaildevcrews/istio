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
    public class Actor
    {
        public string Id { get; set; }
        public string ActorId { get; set; }
        public string PartitionKey { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public int? BirthYear { get; set; }
        public int? DeathYear { get; set; }
        public string TextSearch { get; set; }
        public List<string> Profession { get; set; }
        public List<ActorMovie> Movies { get; set; }

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
                id.StartsWith("nm", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(id[2..], out int idInt))
            {
                return (idInt % 10).ToString(CultureInfo.InvariantCulture);
            }

            throw new ArgumentException("Invalid Partition Key");
        }

        /// <summary>
        /// Sort Actor by Name
        /// </summary>
        /// <param name="x">first comparison</param>
        /// <param name="y">second comparison</param>
        /// <returns>int</returns>
        public static int NameCompare(Actor x, Actor y)
        {
            return string.Compare(x?.Name, y?.Name, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Get the Actor as a content string
        /// </summary>
        /// <returns>string</returns>
        public string GetContent()
        {
            string content = $"{ActorId} {Name} ";

            if (BirthYear != null && BirthYear > 0)
            {
                content += $"{BirthYear} ";

                if (DeathYear != null && DeathYear > BirthYear)
                {
                    content += $"{DeathYear} ";
                }
            }

            if (Profession != null && Profession.Count > 0)
            {
                foreach (string p in Profession)
                {
                    content += $"{p} ";
                }
            }

            if (Movies != null && Movies.Count > 0)
            {
                foreach (ActorMovie m in Movies)
                {
                    content += $"{m.MovieId} {m.Title} {m.Year} {string.Join(' ', m.Genres)} ";
                }
            }

            return content;
        }

        /// <summary>
        /// Convert the Actor to a Lucene Document for indexing
        /// </summary>
        /// <returns>Lucene Document</returns>
        public Document ToDocument()
        {
            Document doc = new ()
            {
                new StringField("id", Id, Store.YES),
                new Int32Field("partitionKey", int.Parse(PartitionKey), Store.YES),
                new StringField("type", Type, Store.YES),
                new StringField("actorId", ActorId, Store.YES),
                new TextField("name", Name, Store.YES),
                new StringField("nameSort", Name.ToLowerInvariant(), Store.YES),
            };

            if (BirthYear != null && BirthYear > 0)
            {
                doc.Add(new Int32Field("birthYear", (int)BirthYear, Store.YES));

                if (DeathYear != null && DeathYear > BirthYear)
                {
                    doc.Add(new Int32Field("deathYear", (int)DeathYear, Store.YES));
                }
            }

            if (Profession != null && Profession.Count > 0)
            {
                foreach (string p in Profession)
                {
                    doc.Add(new TextField("profession", p, Store.YES));
                }
            }

            if (Movies != null && Movies.Count > 0)
            {
                foreach (ActorMovie m in Movies)
                {
                    doc.Add(new StringField("movie.movieId", m.MovieId, Store.YES));
                    doc.Add(new TextField("movie.title", m.Title, Store.YES));
                    doc.Add(new Int32Field("movie.year", m.Year, Store.YES));

                    if (m.Genres != null && m.Genres.Count > 0)
                    {
                        foreach (string c in m.Genres)
                        {
                            doc.Add(new TextField("movie.genre", c.Replace("-", string.Empty), Store.YES));
                        }
                    }
                }
            }

            doc.Add(new TextField("content", GetContent(), Store.NO));
            doc.Add(new StoredField("json", JsonSerializer.SerializeToUtf8Bytes<Actor>(this)));

            return doc;
        }
    }
}
