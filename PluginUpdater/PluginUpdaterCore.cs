using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using GameHelper.Plugin;

namespace PluginUpdater
{
    public class PluginUpdaterCore : PCore<PluginUpdaterSettings>
    {
        private PluginRenderer _pluginRenderer;
        
        public static PluginUpdaterCore Instance { get; private set; }

        public override void OnEnable(bool isGameOpened)
        {
            Instance = this;
            
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _pluginRenderer = new PluginRenderer(Settings);

            Task.Run(async () =>
            {
                _pluginRenderer.Startup();
                await GitUpdater.Instance.UpdateGitInfoAsync();
            });
            
            Console.WriteLine("PluginUpdater started with direct deletion.");
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
