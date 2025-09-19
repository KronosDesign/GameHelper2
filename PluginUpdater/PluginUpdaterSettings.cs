// <copyright file="PluginUpdaterSettings.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace PluginUpdater
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using GameHelper.Plugin;

    /// <summary>
    /// Settings for the PluginUpdater.
    /// </summary>
    public class PluginUpdaterSettings : IPSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether to check for updates on startup.
        /// </summary>
        [DisplayName("Check Updates on Startup")]
        public bool CheckUpdatesOnStartup { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether auto-update is enabled.
        /// </summary>
        [DisplayName("Auto Update Enabled")]
        public bool AutoUpdateEnabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the update check interval in hours.
        /// </summary>
        [DisplayName("Update Check Interval (hours)")]
        public double UpdateCheckInterval { get; set; } = 24.0;

        /// <summary>
        /// Gets or sets a value indicating whether to create backups before updating.
        /// </summary>
        [DisplayName("Create Backups")]
        public bool CreateBackups { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of backups to keep.
        /// </summary>
        [DisplayName("Max Backup Count")]
        public int MaxBackupCount { get; set; } = 3;

        /// <summary>
        /// Gets or sets a value indicating whether to show notifications.
        /// </summary>
        [DisplayName("Show Notifications")]
        public bool ShowNotifications { get; set; } = true;

        /// <summary>
        /// Gets or sets the cache timeout in hours.
        /// </summary>
        [DisplayName("Cache Timeout (hours)")]
        public double CacheTimeout { get; set; } = 6.0;

        /// <summary>
        /// Gets or sets a value indicating whether to auto-refresh the catalog.
        /// </summary>
        [DisplayName("Auto Refresh Catalog")]
        public bool AutoRefreshCatalog { get; set; } = true;

        /// <summary>
        /// Gets or sets the preferred categories.
        /// </summary>
        [DisplayName("Preferred Categories")]
        public List<string> PreferredCategories { get; set; } = new();

        /// <summary>
        /// Gets or sets the blocked authors.
        /// </summary>
        [DisplayName("Blocked Authors")]
        public List<string> BlockedAuthors { get; set; } = new();

        // Simple properties for basic functionality
        public bool HasCheckedUpdates { get; set; } = false;
        public bool AutoCheckUpdates { get; set; } = false;
        public bool WrapLogMessages { get; set; } = true;
        public int UpdateCheckIntervalMinutes { get; set; } = 60;
        public DateTime LastUpdateCheck { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Determines if a periodic check should be performed.
        /// </summary>
        /// <returns>True if a periodic check should be performed.</returns>
        public bool ShouldPerformPeriodicCheck()
        {
            if (!AutoUpdateEnabled && !CheckUpdatesOnStartup)
                return false;

            var timeSinceLastCheck = DateTime.Now - LastUpdateCheck;
            return timeSinceLastCheck.TotalHours >= UpdateCheckInterval;
        }

        /// <summary>
        /// Determines if the catalog should be refreshed.
        /// </summary>
        /// <returns>True if the catalog should be refreshed.</returns>
        public bool ShouldRefreshCatalog()
        {
            if (!AutoRefreshCatalog)
                return false;

            return (DateTime.Now - LastUpdateCheck).TotalHours >= CacheTimeout;
        }

        /// <summary>
        /// Checks if an author is blocked.
        /// </summary>
        /// <param name="author">The author to check.</param>
        /// <returns>True if the author is blocked.</returns>
        public bool IsAuthorBlocked(string author)
        {
            return BlockedAuthors.Any(blockedAuthor => 
                string.Equals(blockedAuthor, author, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks if a category is preferred.
        /// </summary>
        /// <param name="category">The category to check.</param>
        /// <returns>True if the category is preferred.</returns>
        public bool IsCategoryPreferred(string category)
        {
            return PreferredCategories.Any(preferredCategory => 
                string.Equals(preferredCategory, category, StringComparison.OrdinalIgnoreCase));
        }
    }
}