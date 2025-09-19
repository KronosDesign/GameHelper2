// <copyright file="GitUpdater.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace PluginUpdater
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using LibGit2Sharp;
    using LibGit2Sharp.Handlers;

    /// <summary>
    /// Git-based plugin updater based on exCore2 implementation.
    /// </summary>
    public class GitUpdater : IDisposable
    {
        private static GitUpdater _instance;
        public static GitUpdater Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Get path correctly - we're in Plugins/PluginUpdater/, need to go to net8.0/
                    var currentPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
                    var gameHelperPath = Path.Combine(currentPath, "..", "..");
                    gameHelperPath = Path.GetFullPath(gameHelperPath);
                    
                    var pluginsPath = Path.Combine(gameHelperPath, "Plugins");
                    var sourcesPath = Path.Combine(gameHelperPath, "PluginSources");
                    _instance = new GitUpdater(sourcesPath, pluginsPath);
                }
                return _instance;
            }
        }

        private readonly string _sourceFolder;
        private readonly string _pluginFolder;
        private readonly ConcurrentDictionary<string, PluginInfo> _pluginInfo = new(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource _updateCts;
        private Task _updateTask;

        public event Action<int, int> ProgressChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="GitUpdater"/> class.
        /// </summary>
        /// <param name="sourceFolder">The folder containing plugin sources.</param>
        /// <param name="pluginFolder">The folder containing compiled plugins.</param>
        public GitUpdater(string sourceFolder, string pluginFolder)
        {
            _sourceFolder = sourceFolder ?? throw new ArgumentNullException(nameof(sourceFolder));
            _pluginFolder = pluginFolder ?? throw new ArgumentNullException(nameof(pluginFolder));
            Directory.CreateDirectory(_sourceFolder);
            Directory.CreateDirectory(_pluginFolder);
            
            // Note: Deletion processing now happens earlier in PluginUpdaterCore.OnEnable()
            UpdateLocal();
        }

        private void ReportProgress(int current, int total) => ProgressChanged?.Invoke(current, total);

        /// <summary>
        /// Gets plugins that are managed by Git.
        /// </summary>
        /// <returns>Collection of Git-managed plugins.</returns>
        private IEnumerable<PluginInfo> GetGitPlugins() =>
            Directory.GetDirectories(_sourceFolder).Select(x =>
            {
                var gitPath = Path.Join(x, ".git");
                if (Directory.Exists(gitPath) || File.Exists(gitPath))
                {
                    return CreatePluginInfo(Path.GetFileName(x), x);
                }

                return null;
            }).Where(p => p != null);

        /// <summary>
        /// Gets plugins that were manually downloaded (not Git-managed).
        /// </summary>
        /// <returns>Collection of manually downloaded plugins.</returns>
        public IEnumerable<PluginInfo> GetManualPlugins() =>
            Directory.GetDirectories(_pluginFolder).Select(x =>
            {
                var folderName = Path.GetFileName(x);
                
                // Ignore system folders and invalid plugin folders
                if (folderName.Equals("PluginSources", StringComparison.OrdinalIgnoreCase) ||
                    folderName.Equals("Sources", StringComparison.OrdinalIgnoreCase) ||
                    folderName.StartsWith(".", StringComparison.OrdinalIgnoreCase) ||
                    folderName.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                    folderName.Equals("obj", StringComparison.OrdinalIgnoreCase))
                    return null;
                
                // Check if there's a corresponding source folder with Git
                var sourcePath = Path.Combine(_sourceFolder, folderName);
                var gitPath = Path.Combine(sourcePath, ".git");
                
                // If there's no source folder OR no git in source folder, it's manual
                if (!Directory.Exists(sourcePath) || (!Directory.Exists(gitPath) && !File.Exists(gitPath)))
                {
                    return CreatePluginInfo(folderName, x);
                }

                return null;
            }).Where(p => p != null);

        private static PluginInfo CreatePluginInfo(string name, string folder)
        {
            return new PluginInfo { Name = name, Path = folder };
        }

        /// <summary>
        /// Updates local plugin information.
        /// </summary>
        public void UpdateLocal()
        {
            _pluginInfo.Clear();

            var folders = GetGitPlugins().ToList();

            foreach (var pluginInfo in folders)
            {
                try
                {
                    // Verificar se é realmente um repositório Git válido
                    if (!IsValidGitRepository(pluginInfo.Path))
                    {
                        Console.WriteLine($"Skipping {pluginInfo.Name} - not a valid Git repository");
                        continue;
                    }

                    using var repo = new Repository(pluginInfo.Path);
                    SetPluginInfo(pluginInfo, repo);
                    _pluginInfo[pluginInfo.Name] = pluginInfo;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error processing {pluginInfo.Path}: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Checks if a directory contains a valid Git repository.
        /// </summary>
        private bool IsValidGitRepository(string path)
        {
            try
            {
                var gitPath = Path.Combine(path, ".git");
                return Directory.Exists(gitPath) || File.Exists(gitPath);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Updates Git information for all plugins asynchronously.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task UpdateGitInfoAsync()
        {
            if (_updateTask != null && !_updateTask.IsCompleted)
            {
                _updateCts?.Cancel();

                try
                {
                    await Task.WhenAny(_updateTask, Task.Delay(5000));
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during task cancellation: {ex.Message}");
                }
            }

            _updateCts = new CancellationTokenSource();

            try
            {
                _updateTask = UpdateGitInfoInternalAsync(_updateCts.Token);
                await _updateTask;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Git update operation was cancelled");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during git update: {ex.Message}");
                throw;
            }
        }

        private void SetPluginInfo(PluginInfo plugin, Repository repository)
        {
            SetBranchInfo(plugin, repository);
            
            plugin.CurrentCommit = repository.Head.Tip.Sha[..7];
            plugin.UncommittedChangeCount = repository.Diff.Compare<TreeChanges>().Count;
            var trackingBranch = repository.Head.TrackedBranch;
            if (trackingBranch != null)
            {
                plugin.LatestCommit = trackingBranch.Tip.Sha[..7];
                plugin.LatestCommitMessage = trackingBranch.Tip.MessageShort;

                if (plugin.CurrentCommit != plugin.LatestCommit)
                {
                    var ahead = repository.Head.TrackingDetails.AheadBy ?? 0;
                    var behind = repository.Head.TrackingDetails.BehindBy ?? 0;
                    plugin.BehindAhead = $"{behind} behind, {ahead} ahead";
                    plugin.BehindBy = behind;
                    plugin.AheadBy = ahead;
                }
                else
                {
                    plugin.BehindBy = 0;
                    plugin.AheadBy = 0;
                    plugin.BehindAhead = "";
                }
            }
            else
            {
                plugin.LatestCommit = plugin.CurrentCommit;
                plugin.LatestCommitMessage = null;
                plugin.BehindAhead = "";
            }

            var previousCommit = repository.Head.Tip.Parents.FirstOrDefault();
            plugin.PreviousCommit = previousCommit != null 
                ? $"{previousCommit.Sha[..7]} {previousCommit.MessageShort}" 
                : null;
        }

        private void SetBranchInfo(PluginInfo plugin, Repository repository)
        {
            // Get all local branches
            var localBranches = repository.Branches
                .Where(b => !b.IsRemote)
                .Where(b => b.FriendlyName != "HEAD")
                .ToList();

            var trackedBranches = localBranches
                .Select(x => GetTrackedBranchSafe(repository, x))
                .Where(x => x != null)
                .ToHashSet();

            // Get all remote branches
            var remoteBranches = repository.Branches
                .Where(b => b.IsRemote)
                .Where(b => !b.FriendlyName.EndsWith("/HEAD"))
                .Except(trackedBranches)
                .Distinct();

            // Combine and deduplicate branches
            plugin.AvailableBranches = localBranches
                .Union(remoteBranches)
                .Select(b => b.FriendlyName)
                .Order()
                .ToList();
            
            plugin.CurrentBranch = repository.Head.FriendlyName;
        }

        private static Branch GetTrackedBranchSafe(Repository repository, Branch x)
        {
            return x.RemoteName == null ||
                   x.RemoteName.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ||
                   repository.Network.Remotes[x.RemoteName] == null
                ? null
                : x.TrackedBranch;
        }

        /// <summary>
        /// Gets plugin information for all plugins (Git-managed and manual).
        /// </summary>
        /// <returns>List of plugin information.</returns>
        public List<PluginInfo> GetPluginInfo()
        {
            var allPlugins = new List<PluginInfo>(_pluginInfo.Values);
            
            // Add manual plugins (non-Git)
            foreach (var manualPlugin in GetManualPlugins())
            {
                if (!allPlugins.Any(p => p.Name.Equals(manualPlugin.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    allPlugins.Add(manualPlugin);
                }
            }
            
            return allPlugins.OrderBy(p => p.Name).ToList();
        }

        private async Task UpdateGitInfoInternalAsync(CancellationToken cancellationToken)
        {
            _pluginInfo.Clear();
            var folders = GetGitPlugins().ToList();
            var totalFolders = folders.Count;
            var currentFolder = 0;

            ReportProgress(0, totalFolders);

            foreach (var pluginInfo in folders)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Verificar se é realmente um repositório Git válido
                    if (!IsValidGitRepository(pluginInfo.Path))
                    {
                        Console.WriteLine($"Skipping {pluginInfo.Name} - not a valid Git repository");
                        currentFolder++;
                        ReportProgress(currentFolder, totalFolders);
                        continue;
                    }

                    await Task.Run(() =>
                    {
                        using var repo = new Repository(pluginInfo.Path);
                        SetPluginInfo(pluginInfo, repo);
                        _pluginInfo[pluginInfo.Name] = pluginInfo;

                        var branchName = repo.Head.FriendlyName;
                        var trackingBranch = repo.Head.TrackedBranch;

                        var remote = GetHeadRemote(repo, false);
                        if (remote != null)
                        {
                            var refSpecs = remote.FetchRefSpecs.Select(r => r.Specification);
                            Commands.Fetch(repo, remote.Name, refSpecs, null, null);

                            if (trackingBranch == null)
                            {
                                var remoteBranch = repo.Branches[$"origin/{branchName}"];
                                if (remoteBranch != null)
                                {
                                    repo.Branches.Update(repo.Head,
                                        b => b.TrackedBranch = remoteBranch.CanonicalName);
                                }
                            }
                        }

                        SetPluginInfo(pluginInfo, repo);
                    }, cancellationToken);

                    _pluginInfo[pluginInfo.Name] = pluginInfo;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error processing {pluginInfo.Path}: {e}");
                    pluginInfo.Error = e.Message;
                    if (_pluginInfo.ContainsKey(pluginInfo.Name))
                    {
                        _pluginInfo[pluginInfo.Name] = pluginInfo;
                    }
                    else
                    {
                        // Verificar se é realmente um repositório Git válido antes de tentar abrir
                        if (IsValidGitRepository(pluginInfo.Path))
                        {
                            try
                            {
                                using var repo = new Repository(pluginInfo.Path);
                                SetPluginInfo(pluginInfo, repo);
                                _pluginInfo.TryAdd(pluginInfo.Name, pluginInfo);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Additional error opening repository {pluginInfo.Path}: {ex.Message}");
                                pluginInfo.Error = ex.Message;
                                _pluginInfo.TryAdd(pluginInfo.Name, pluginInfo);
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Skipping invalid repository {pluginInfo.Path}");
                            _pluginInfo.TryAdd(pluginInfo.Name, pluginInfo);
                        }
                    }
                }
                finally
                {
                    currentFolder++;
                    ReportProgress(currentFolder, totalFolders);
                }
            }
        }

        private static Remote GetHeadRemote(Repository repo, bool throwOnNoTracking = true)
        {
            try
            {
                var remote = repo.Network.Remotes["origin"];
                return remote;
            }
            catch (Exception)
            {
                if (throwOnNoTracking)
                    throw;
                return null;
            }
        }

        /// <summary>
        /// Clones a repository asynchronously.
        /// </summary>
        /// <param name="repoUrl">The repository URL.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task CloneRepositoryAsync(string repoUrl)
        {
            var repoName = ExtractRepoNameAndBranch(repoUrl, out string branch);
            var targetPath = Path.Combine(_sourceFolder, repoName);

            try
            {
                await Task.Run(() =>
                {
                    if (Directory.Exists(targetPath))
                    {
                        Directory.Delete(targetPath, true);
                    }

                    Repository.Clone(repoUrl, targetPath);
                    Console.WriteLine($"Repository {repoName} cloned successfully!");

                    var pluginInfo = new PluginInfo
                    {
                        Name = repoName,
                        Path = targetPath
                    };

                    using var repo = new Repository(targetPath);
                    SetPluginInfo(pluginInfo, repo);

                    var trackingBranch = repo.Head.TrackedBranch;
                    if (trackingBranch != null)
                        pluginInfo.LatestCommit = trackingBranch.Tip.Sha[..7];

                    _pluginInfo[pluginInfo.Name] = pluginInfo;
                });

                // Compile and deploy the plugin after cloning
                Console.WriteLine($"Building and deploying plugin {repoName}...");
                await BuildAndDeployPluginAsync(repoName, targetPath);
                Console.WriteLine($"Plugin {repoName} installed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during clone/build process: {ex.Message}");
                
                if (Directory.Exists(targetPath))
                {
                    try
                    {
                        Directory.Delete(targetPath, true);
                    }
                    catch (Exception deleteEx)
                    {
                        Console.WriteLine($"Unable to delete {targetPath}: {deleteEx.Message}");
                    }
                }
                throw; // Re-throw so the UI can handle it
            }
        }

        private static string ExtractRepoNameAndBranch(string repoUrl, out string branch)
        {
            branch = "main";
            
            var uri = new Uri(repoUrl);
            var segments = uri.Segments;
            var repoName = segments[^1].Replace(".git", "");
            
            return repoName;
        }

        /// <summary>
        /// Updates a plugin asynchronously.
        /// </summary>
        /// <param name="pluginName">The plugin name.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task UpdatePluginAsync(string pluginName)
        {
            var folder = GetGitPlugins().FirstOrDefault(f => f.Name.Equals(pluginName, StringComparison.InvariantCultureIgnoreCase));
            if (folder == null) return;

            var plugin = _pluginInfo.GetValueOrDefault(pluginName);
            if (plugin == null) return;

            MergeResult mergeResult = null;
            
            await Task.Run(() =>
            {
                try
                {
                    // Verificar se é realmente um repositório Git válido
                    if (!IsValidGitRepository(folder.Path))
                    {
                        plugin.LastMessage = "Invalid Git repository";
                        plugin.Error = "Repository is corrupted or not a valid Git repository";
                        Console.WriteLine($"Skipping {pluginName} - not a valid Git repository");
                        return;
                    }

                    using var repo = new Repository(folder.Path);

                    var remote = GetHeadRemote(repo);
                    var refSpecs = remote.FetchRefSpecs.Select(r => r.Specification);
                    Commands.Fetch(repo, remote.Name, refSpecs, null, null);

                    mergeResult = Commands.Pull(
                        repo,
                        new Signature("Plugin Updater", "updater@local", DateTimeOffset.Now),
                        new PullOptions());

                    plugin.LastMessage = mergeResult.Status == MergeStatus.UpToDate
                        ? "Already up to date"
                        : $"Updated to {mergeResult.Commit.Id.Sha[..7]}";

                    SetPluginInfo(plugin, repo);

                    Console.WriteLine($"{folder.Path} updated to {plugin.CurrentCommit}");
                    Console.WriteLine(plugin.LastMessage);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating plugin {pluginName}: {ex}");
                    throw;
                }
            });
            
            // Build and deploy after updating
            if (mergeResult != null && mergeResult.Status != MergeStatus.UpToDate)
            {
                await BuildAndDeployPluginAsync(pluginName, folder.Path);
            }
        }

        /// <summary>
        /// Builds and deploys a plugin to the plugins folder.
        /// </summary>
        /// <param name="pluginName">The plugin name.</param>
        /// <param name="sourcePath">The source path of the plugin.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task BuildAndDeployPluginAsync(string pluginName, string sourcePath)
        {
            Console.WriteLine($"=== Starting build and deploy for {pluginName} ===");
            Console.WriteLine($"Source path: {sourcePath}");
            Console.WriteLine($"Plugin folder: {_pluginFolder}");
            
            try
            {
                Console.WriteLine($"Building plugin {pluginName}...");
                
                // Find the .csproj file
                var csprojFiles = Directory.GetFiles(sourcePath, "*.csproj", SearchOption.AllDirectories);
                if (!csprojFiles.Any())
                {
                    Console.WriteLine($"No .csproj file found for {pluginName}");
                    return;
                }

                var csprojPath = csprojFiles.First();
                var projectDir = Path.GetDirectoryName(csprojPath);
                
                // Fix project references before building
                await FixProjectReferencesAsync(csprojPath);

                // Clean and build the project
                var cleanProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = "clean",
                        WorkingDirectory = projectDir,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                cleanProcess.Start();
                cleanProcess.WaitForExit(30000); // 30 seconds timeout
                Console.WriteLine("Clean process completed");

                var buildProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = $"build \"{csprojPath}\" --configuration Release",
                        WorkingDirectory = projectDir,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                Console.WriteLine($"Starting build process for {pluginName}...");
                buildProcess.Start();
                
                // Read output streams concurrently to avoid deadlock
                var outputTask = buildProcess.StandardOutput.ReadToEndAsync();
                var errorTask = buildProcess.StandardError.ReadToEndAsync();
                
                // Wait for process to complete with timeout
                if (!buildProcess.WaitForExit(60000)) // 60 seconds timeout
                {
                    buildProcess.Kill();
                    throw new TimeoutException($"Build process for {pluginName} timed out");
                }
                
                // Wait for streams to complete
                var buildOutput = await outputTask;
                var buildError = await errorTask;
                
                Console.WriteLine($"Build process completed with exit code: {buildProcess.ExitCode}");

                if (buildProcess.ExitCode == 0)
                {
                    Console.WriteLine($"Build successful for {pluginName}");
                    
                    // Find the built DLL
                    var dllPath = Path.Combine(projectDir, "bin", "Release", "net8.0", $"{pluginName}.dll");
                    if (!File.Exists(dllPath))
                    {
                        // Try to find any DLL in the output directory
                        var outputDir = Path.Combine(projectDir, "bin", "Release", "net8.0");
                        var dlls = Directory.GetFiles(outputDir, "*.dll", SearchOption.TopDirectoryOnly);
                        if (dlls.Any())
                        {
                            dllPath = dlls.First();
                        }
                        else
                        {
                            Console.WriteLine($"No DLL found in output directory for {pluginName}");
                            return;
                        }
                    }

                    // Create the plugin folder structure
                    var targetPluginDir = Path.Combine(_pluginFolder, pluginName);
                    Console.WriteLine($"Creating plugin directory: {targetPluginDir}");
                    Directory.CreateDirectory(targetPluginDir);

                    // Copy only the plugin DLL and its specific dependencies (not GameHelper files)
                    var buildOutputDir = Path.GetDirectoryName(dllPath);
                    var files = Directory.GetFiles(buildOutputDir, "*.*", SearchOption.TopDirectoryOnly);
                    
                    // Filter out GameHelper-related files that shouldn't be copied
                    var pluginFiles = files.Where(file =>
                    {
                        var fileName = Path.GetFileName(file).ToLowerInvariant();
                        
                        // Skip GameHelper and GameOffsets files - plugin shouldn't include these
                        if (fileName.StartsWith("gamehelper.") || fileName.StartsWith("gameoffsets."))
                            return false;
                            
                        // Skip common system files
                        if (fileName.EndsWith(".runtimeconfig.json") && !fileName.StartsWith(pluginName.ToLowerInvariant()))
                            return false;
                            
                        return true;
                    }).ToArray();
                    
                    Console.WriteLine($"Copying {pluginFiles.Length} plugin-specific files from {buildOutputDir}");
                    
                    foreach (var file in pluginFiles)
                    {
                        var fileName = Path.GetFileName(file);
                        var targetFile = Path.Combine(targetPluginDir, fileName);
                        File.Copy(file, targetFile, true);
                        Console.WriteLine($"Copied: {fileName}");
                    }

                    Console.WriteLine($"Plugin {pluginName} deployed successfully to {targetPluginDir}");
                    Console.WriteLine($"=== Build and deploy completed for {pluginName} ===");
                }
                else
                {
                    Console.WriteLine($"Build failed for {pluginName}:");
                    Console.WriteLine(buildOutput);
                    Console.WriteLine(buildError);
                    throw new InvalidOperationException($"Build failed for plugin {pluginName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error building plugin {pluginName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Fixes project references to point to the correct GameHelper paths.
        /// </summary>
        /// <param name="csprojPath">The path to the .csproj file.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task FixProjectReferencesAsync(string csprojPath)
        {
            try
            {
                var content = await File.ReadAllTextAsync(csprojPath);
                var originalContent = content;

                // Fix common GameHelper reference patterns
                content = content.Replace(@"..\..\GameHelper2-MyWork\GameHelper\GameHelper.csproj", @"..\..\GameHelper2\GameHelper\GameHelper.csproj");
                content = content.Replace(@"..\..\GameHelper2-MyWork\GameOffsets\GameOffsets.csproj", @"..\..\GameHelper2\GameOffsets\GameOffsets.csproj");
                content = content.Replace(@"..\GameHelper2-MyWork\GameHelper\GameHelper.csproj", @"..\GameHelper2\GameHelper\GameHelper.csproj");
                content = content.Replace(@"..\GameHelper2-MyWork\GameOffsets\GameOffsets.csproj", @"..\GameHelper2\GameOffsets\GameOffsets.csproj");

                // Determine the correct relative path from the plugin to GameHelper
                var pluginDir = Path.GetDirectoryName(csprojPath);
                var gameHelperRoot = @"C:\Users\couss\Documents\GameHelper2";
                var gameHelperProject = Path.Combine(gameHelperRoot, "GameHelper", "GameHelper.csproj");
                var gameOffsetsProject = Path.Combine(gameHelperRoot, "GameOffsets", "GameOffsets.csproj");

                // Calculate relative paths
                var relativeGameHelper = Path.GetRelativePath(pluginDir, gameHelperProject);
                var relativeGameOffsets = Path.GetRelativePath(pluginDir, gameOffsetsProject);

                // Replace any absolute or incorrect relative paths
                content = System.Text.RegularExpressions.Regex.Replace(
                    content,
                    @"<ProjectReference\s+Include\s*=\s*""[^""]*GameHelper\.csproj""",
                    $"<ProjectReference Include=\"{relativeGameHelper.Replace('\\', '/')}\"",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                content = System.Text.RegularExpressions.Regex.Replace(
                    content,
                    @"<ProjectReference\s+Include\s*=\s*""[^""]*GameOffsets\.csproj""",
                    $"<ProjectReference Include=\"{relativeGameOffsets.Replace('\\', '/')}\"",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                // Only write back if content changed
                if (content != originalContent)
                {
                    await File.WriteAllTextAsync(csprojPath, content);
                    Console.WriteLine($"Fixed project references in {Path.GetFileName(csprojPath)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fixing project references: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes read-only attributes from files and directories recursively.
        /// </summary>
        /// <param name="path">The path to process.</param>
        private void RemoveReadOnlyAttributesRecursive(string path)
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
                return;
            }

            if (!Directory.Exists(path)) return;

            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
                }
                catch
                {
                    // Ignore errors on individual files
                }
            }

            foreach (var dir in Directory.GetDirectories(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(dir, File.GetAttributes(dir) & ~FileAttributes.ReadOnly);
                }
                catch
                {
                    // Ignore errors on individual directories
                }
            }
        }

        /// <summary>
        /// Disposes resources.
        /// </summary>
        public void Dispose()
        {
            _updateCts?.Cancel();
            _updateCts?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}