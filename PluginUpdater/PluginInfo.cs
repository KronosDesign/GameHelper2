// <copyright file="PluginInfo.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace PluginUpdater
{
    using System.Collections.Generic;

    /// <summary>
    /// Information about a plugin.
    /// </summary>
    public class PluginInfo
    {
        public required string Name { get; set; }
        public required string Path { get; set; }
        public string CurrentCommit { get; set; } = "";
        public string LatestCommit { get; set; } = "";
        public string BehindAhead { get; set; } = "";
        public int BehindBy { get; set; } = 0;
        public int AheadBy { get; set; } = 0;
        public string LastMessage { get; set; } = "";
        public string LatestCommitMessage { get; set; } = "";
        public string PreviousCommit { get; set; } = "";
        public int UncommittedChangeCount { get; set; }
        public List<string> AvailableBranches { get; set; } = new();
        public string CurrentBranch { get; set; } = "";
        public string Error { get; set; } = "";
    }
}