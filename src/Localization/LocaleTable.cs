using System;
using System.Collections.Generic;
using System.IO;
using ModInfoPanel.Configuration;
using Newtonsoft.Json;

namespace ModInfoPanel.Localization
{
    internal static class LocaleTable
    {
        private static readonly Dictionary<string, string> Zh =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> En =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static string _cachedLangName;
        private static bool _initialized;

        public static string ActiveLanguage { get; private set; } = "zh-CN";

        public static void Initialize(string pluginDir)
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            Load(Path.Combine(pluginDir, "Locale", "locale.zh-CN.json"), Zh);
            Load(Path.Combine(pluginDir, "Locale", "locale.EN.json"), En);
            RefreshLanguage();
        }

        public static string T(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key))
            {
                return fallback;
            }

            RefreshLanguage();
            Dictionary<string, string> table = ActiveLanguage == "en" ? En : Zh;
            if (table.TryGetValue(key, out string value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            if (Zh.TryGetValue(key, out string zh) && !string.IsNullOrWhiteSpace(zh))
            {
                return zh;
            }

            return fallback;
        }

        public static bool Has(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            RefreshLanguage();
            Dictionary<string, string> table = ActiveLanguage == "en" ? En : Zh;
            if (table.TryGetValue(key, out string value) && !string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            return Zh.TryGetValue(key, out string zh) && !string.IsNullOrWhiteSpace(zh);
        }

        private static void RefreshLanguage()
        {
            LanguageMode mode = Plugin.Cfg?.Language?.Value ?? LanguageMode.Auto;
            if (mode == LanguageMode.ZhCN)
            {
                ActiveLanguage = "zh-CN";
                return;
            }

            if (mode == LanguageMode.EN)
            {
                ActiveLanguage = "en";
                return;
            }

            string name = null;
            try
            {
                name = global::Locale.currentLangName;
            }
            catch
            {
            }

            if (string.Equals(name, _cachedLangName, StringComparison.Ordinal))
            {
                return;
            }

            _cachedLangName = name;
            if (string.IsNullOrEmpty(name))
            {
                ActiveLanguage = "zh-CN";
            }
            else if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                ActiveLanguage = "zh-CN";
            }
            else
            {
                ActiveLanguage = "en";
            }
        }

        private static void Load(string path, Dictionary<string, string> target)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return;
                }

                Dictionary<string, string> data =
                    JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path));
                if (data == null)
                {
                    return;
                }

                foreach (KeyValuePair<string, string> kv in data)
                {
                    if (!string.IsNullOrWhiteSpace(kv.Key))
                    {
                        target[kv.Key] = kv.Value ?? "";
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[ModInfoPanel] 读取语言文件失败 {path}: {ex.Message}");
            }
        }
    }

    internal static class Loc
    {
        public static string T(string key, string fallback)
        {
            return LocaleTable.T(key, fallback);
        }
    }
}
