// <copyright file="StorePluginInfo.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace PluginUpdater
{
    /// <summary>
    /// Information about a plugin available in the store.
    /// </summary>
    public class StorePluginInfo
    {
        /// <summary>
        /// Gets or sets the plugin name.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Gets or sets the GitHub repository URL.
        /// </summary>
        public string RepositoryUrl { get; set; } = "";

        /// <summary>
        /// Gets or sets the plugin description.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Gets or sets the author name.
        /// </summary>
        public string Author { get; set; } = "";

        /// <summary>
        /// Gets or sets the plugin category.
        /// </summary>
        public string Category { get; set; } = "";

        /// <summary>
        /// Gets or sets a value indicating whether the plugin is already installed.
        /// </summary>
        public bool IsInstalled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the plugin is being installed.
        /// </summary>
        public bool IsInstalling { get; set; }
    }
}