using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ModInfoPanel.Localization;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ModInfoPanel.Data
{
    /// <summary>
    /// 分类 → 获取来源映射（acquisition.json，首次自动生成，可手工编辑并热重载）。
    /// 来源 id：medical/food/container/dropcapsule/capsule/trader/corpse。
    /// </summary>
    internal static class AcquisitionMap
    {
        private const string FileName = "acquisition.json";
        private const string DefaultJson = @"{
  ""categorySources"": {
    ""medical"": [""medical"", ""trader""],
    ""food"": [""food"", ""trader""],
    ""drug"": [""medical"", ""trader""],
    ""container"": [""container"", ""trader""],
    ""utility"": [""container"", ""trader""],
    ""custom"": [""container"", ""dropcapsule"", ""trader""],
    ""water"": [""trader""],
    ""tool"": [""trader""]
  },
  ""traderCategories"": [""medical"", ""food"", ""water"", ""tool"", ""drug"", ""container"", ""utility"", ""custom""]
}";

        private static readonly Dictionary<string, List<string>> CategorySources =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> TraderCategories =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static string _path;
        private static DateTime _writeTime = DateTime.MinValue;
        private static float _nextCheck;
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _path = Path.Combine(Plugin.PluginDir, FileName);
            EnsureFile();
            Reload(force: true);
        }

        public static List<string> Resolve(string category, int value)
        {
            MaybeReload();
            List<string> result = new List<string>();
            if (string.IsNullOrWhiteSpace(category))
            {
                return result;
            }

            string key = category.Trim().ToLowerInvariant();
            if (key == "trash" || key == "unobtainable")
            {
                return result;
            }

            if (!CategorySources.TryGetValue(key, out List<string> sources) || sources == null)
            {
                return result;
            }

            foreach (string source in sources)
            {
                if (string.IsNullOrWhiteSpace(source))
                {
                    continue;
                }

                if (string.Equals(source, "trader", StringComparison.OrdinalIgnoreCase))
                {
                    if (value <= 0 || !TraderCategories.Contains(key))
                    {
                        continue;
                    }
                }

                string label = Loc.T("source_" + source.Trim().ToLowerInvariant(), source.Trim());
                if (!string.IsNullOrWhiteSpace(label) && !result.Contains(label))
                {
                    result.Add(label);
                }
            }

            return result;
        }

        public static string SourceLabel(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return null;
            }

            return Loc.T("source_" + source.Trim().ToLowerInvariant(), source.Trim());
        }

        private static void MaybeReload()
        {
            if (Time.unscaledTime < _nextCheck)
            {
                return;
            }

            _nextCheck = Time.unscaledTime + 2f;
            Reload();
        }

        private static void Reload(bool force = false)
        {
            try
            {
                if (!File.Exists(_path))
                {
                    EnsureFile();
                }

                DateTime write = File.GetLastWriteTimeUtc(_path);
                if (!force && write == _writeTime)
                {
                    return;
                }

                _writeTime = write;
                Parse(File.ReadAllText(_path));
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("[ModInfoPanel] acquisition.json 读取失败: " + ex.Message);
                try
                {
                    Parse(DefaultJson);
                }
                catch
                {
                }
            }
        }

        private static void EnsureFile()
        {
            try
            {
                if (File.Exists(_path))
                {
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                File.WriteAllText(_path, DefaultJson, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Plugin.Debug("[AcquisitionMap] 写入默认映射失败: " + ex.Message);
            }
        }

        private static void Parse(string json)
        {
            CategorySources.Clear();
            TraderCategories.Clear();

            JObject root = JObject.Parse(json);
            if (root["categorySources"] is JObject categories)
            {
                foreach (JProperty prop in categories.Properties())
                {
                    List<string> sources = new List<string>();
                    if (prop.Value is JArray array)
                    {
                        foreach (JToken token in array)
                        {
                            string value = token?.ToString();
                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                sources.Add(value.Trim());
                            }
                        }
                    }
                    else if (prop.Value.Type == JTokenType.String)
                    {
                        sources.Add(prop.Value.ToString().Trim());
                    }

                    CategorySources[prop.Name] = sources;
                }
            }

            if (root["traderCategories"] is JArray traders)
            {
                foreach (JToken token in traders)
                {
                    string value = token?.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        TraderCategories.Add(value.Trim());
                    }
                }
            }

            if (CategorySources.Count == 0)
            {
                Parse(DefaultJson);
            }
        }
    }
}
