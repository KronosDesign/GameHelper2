// <copyright file="PluginStoreData.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PluginUpdater
{
    /// <summary>
    /// Data structure for plugin store JSON file.
    /// </summary>
    public class PluginStoreData
    {
        /// <summary>
        /// Gets or sets the list of plugins.
        /// </summary>
        [JsonPropertyName("plugins")]
        public List<StorePluginJson> Plugins { get; set; } = new();
    }

    /// <summary>
    /// JSON representation of store plugin info.
    /// </summary>
    public class StorePluginJson
    {
        /// <summary>
        /// Gets or sets the plugin name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>
        /// Gets or sets the repository URL.
        /// </summary>
        [JsonPropertyName("repositoryUrl")]
        public string RepositoryUrl { get; set; } = "";

        /// <summary>
        /// Gets or sets the plugin description.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        /// <summary>
        /// Gets or sets the author name.
        /// </summary>
        [JsonPropertyName("author")]
        public string Author { get; set; } = "";

        /// <summary>
        /// Gets or sets the plugin category.
        /// </summary>
        [JsonPropertyName("category")]
        public string Category { get; set; } = "";
    }
}