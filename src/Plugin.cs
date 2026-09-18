using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ModInfoPanel.Configuration;
using ModInfoPanel.Data;
using ModInfoPanel.Localization;
using ModInfoPanel.Patches;

namespace ModInfoPanel
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("net.cucorelib", BepInDependency.DependencyFlags.HardDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static Plugin Instance;
        internal static ManualLogSource Log;
        internal static InfoConfig Cfg;
        internal static string PluginDir;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            PluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Paths.PluginPath;

            Cfg = new InfoConfig(Config);
            LocaleTable.Initialize(PluginDir);
            InfoCache.Initialize();
            AcquisitionMap.Initialize();

            _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            _harmony.PatchAll();
            UeiTooltipPatch.Install(_harmony);

            Logger.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} 已加载（语言: {LocaleTable.ActiveLanguage}）");

            StartCoroutine(InfoCache.ValidateWhenReady());
        }

        private void OnDestroy()
        {
            try
            {
                _harmony?.UnpatchSelf();
            }
            catch
            {
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        internal static void Debug(string message)
        {
            if (Cfg != null && Cfg.DebugLog.Value)
            {
                Log?.LogDebug(message);
            }
        }
    }
}
