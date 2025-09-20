using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using GameHelper.Plugin;

namespace PluginUpdater
{
    public sealed class PluginUpdaterCore : PCore<PluginUpdaterSettings>
    {
        private PluginRenderer _pluginRenderer;
        
        public static PluginUpdaterCore Instance { get; private set; }

        static PluginUpdaterCore()
        {
            // Setup assembly resolver for LibGit2Sharp
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (args.Name.StartsWith("LibGit2Sharp"))
            {
                var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var gameHelperDir = assemblyDir?.Replace("\\Plugins\\PluginUpdater", "");
                var libGit2Path = Path.Combine(gameHelperDir ?? "", "LibGit2Sharp.dll");
                
                Console.WriteLine($"Assembly resolve request for: {args.Name}");
                Console.WriteLine($"Looking for LibGit2Sharp at: {libGit2Path}");
                
                if (File.Exists(libGit2Path))
                {
                    Console.WriteLine($"Loading LibGit2Sharp from: {libGit2Path}");
                    return Assembly.LoadFrom(libGit2Path);
                }
                else
                {
                    Console.WriteLine($"LibGit2Sharp.dll not found at: {libGit2Path}");
                }
            }
            return null;
        }

        public override void OnEnable(bool isGameOpened)
        {
            Instance = this;
            
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _pluginRenderer = new PluginRenderer(Settings);

            Console.WriteLine("PluginUpdater started with direct deletion.");
            
            // Initialize GitUpdater after assembly resolver is set up
            Task.Run(async () =>
            {
                try
                {
                    _pluginRenderer.Startup();
                    await GitUpdater.Instance.UpdateGitInfoAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error initializing GitUpdater: {ex.Message}");
                }
            });
        }

        public override void OnDisable()
        {
            _pluginRenderer?.Dispose();
        }

        public override void DrawUI()
        {
            // Interface principal é no DrawSettings
        }

        public override void SaveSettings()
        {
            // Settings são salvos automaticamente pelo GameHelper
        }

        public override void DrawSettings()
        {
            try
            {
                _pluginRenderer?.Render();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Rendering error: {ex.Message}");
            }
        }
    }
}
