// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Imdb.Model
{
    public class Genre
    {
        public string Id { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("genre")]
        public string Name { get; set; }
        public string PartitionKey { get; set; }
        public string Type { get; set; }
    }
}
