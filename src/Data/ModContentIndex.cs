using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Bootstrap;
using CUCoreLib.Data;
using CUCoreLib.Registries;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ModInfoPanel.Data
{
    /// <summary>
    /// 判定 mod 内容并解析来源 mod。数据源：
    /// CUCoreLib 注册表（Item/Liquid/LiquidTile/BuildingEntity）+ 原版 Lang/EN.json 基线 + 插件 GUID 猜测。
    /// </summary>
    internal static class ModContentIndex
    {
        private static readonly string[] InfraGuidFragments =
        {
            "cucore", "krokmp", "krokosha", "rshlib", "librecipeconfig", "modinfopanel",
            "servermodloader", "sml", "qol", "unknownperformance", "cpuoptimization"
        };

        private static HashSet<string> _vanillaItemIds;
        private static bool _vanillaLoaded;

        public static string NormalizeId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return id;
            }

            string text = id.Trim();
            int dollar = text.IndexOf('$');
            if (dollar > 0)
            {
                text = text.Substring(0, dollar);
            }

            return text;
        }

        public static bool IsModItem(string id, ItemInfo stats)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            string norm = NormalizeId(id);

            if (ItemRegistry.TryGetCustomInfo(norm, out _))
            {
                return true;
            }

            if (ItemRegistry.TryGetOwnerModGuid(norm, out string guid) && !string.IsNullOrWhiteSpace(guid))
            {
                return true;
            }

            if (stats is CustomItemInfo)
            {
                return true;
            }

            if (IsDescriptionMissing(stats, norm))
            {
                return true;
            }

            if (Plugin.Cfg == null || Plugin.Cfg.UseVanillaLocaleBaseline.Value)
            {
                HashSet<string> vanilla = GetVanillaItemIds();
                if (vanilla.Count > 0 && !vanilla.Contains(norm))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsDescriptionMissing(ItemInfo stats, string id)
        {
            if (stats == null)
            {
                return true;
            }

            string desc = stats.description;
            if (string.IsNullOrWhiteSpace(desc))
            {
                return true;
            }

            string norm = NormalizeId(id);
            if (string.IsNullOrWhiteSpace(norm))
            {
                return false;
            }

            return string.Equals(desc, norm, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(desc, norm + "dsc", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(desc, id, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(desc, id + "dsc", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetItemOwner(string id)
        {
            string norm = NormalizeId(id);
            try
            {
                if (ItemRegistry.TryGetOwnerModGuid(norm, out string guid) && !string.IsNullOrWhiteSpace(guid))
                {
                    return guid;
                }
            }
            catch
            {
            }

            return GuessOwner(norm);
        }

        public static string GetLiquidOwner(string id)
        {
            string norm = NormalizeId(id);
            try
            {
                if (LiquidRegistry.TryGetOwnerModGuid(norm, out string guid) && !string.IsNullOrWhiteSpace(guid))
                {
                    return guid;
                }
            }
            catch
            {
            }

            return GuessOwner(norm);
        }

        public static string GetCreatureOwner(string id)
        {
            string norm = NormalizeId(id);
            try
            {
                if (BuildingEntityRegistry.TryGetOwnerModGuid(norm, out string guid) && !string.IsNullOrWhiteSpace(guid))
                {
                    return guid;
                }
            }
            catch
            {
            }

            return GuessOwner(norm);
        }

        /// <summary>世界流体 tile 无公开 owner API，只能按 GUID 猜测。</summary>
        public static string GetTileOwner(string id)
        {
            return GuessOwner(NormalizeId(id));
        }

        public static bool HasTag(ItemInfo stats, string tag)
        {
            if (stats == null || string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            try
            {
                if (stats.GetTags() == null)
                {
                    stats.SetTags();
                }

                return stats.HasTag(tag);
            }
            catch
            {
                return false;
            }
        }

        public static bool TryGetCustomInfo(ItemInfo stats, out CustomItemInfo info)
        {
            info = null;
            if (stats == null)
            {
                return false;
            }

            try
            {
                return ItemRegistry.TryGetCustomInfo(stats, out info) && info != null;
            }
            catch
            {
                return false;
            }
        }

        public static HashSet<string> GetVanillaItemIds()
        {
            if (_vanillaLoaded)
            {
                return _vanillaItemIds;
            }

            _vanillaLoaded = true;
            HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string path = Path.Combine(Application.dataPath, "Lang", "EN.json");
                if (File.Exists(path))
                {
                    JObject root = JObject.Parse(File.ReadAllText(path));
                    if (root["main"] is JObject main)
                    {
                        foreach (JProperty prop in main.Properties())
                        {
                            if (prop.Name.EndsWith("dsc", StringComparison.OrdinalIgnoreCase) && prop.Name.Length > 3)
                            {
                                set.Add(prop.Name.Substring(0, prop.Name.Length - 3));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Debug("读取原版 EN.json 基线失败: " + ex.Message);
            }

            _vanillaItemIds = set;
            return set;
        }

        private static string GuessOwner(string contentId)
        {
            if (string.IsNullOrWhiteSpace(contentId))
            {
                return null;
            }

            try
            {
                foreach (KeyValuePair<string, BepInEx.PluginInfo> kv in Chainloader.PluginInfos)
                {
                    string guid = kv.Key;
                    if (string.IsNullOrWhiteSpace(guid) || IsInfraGuid(guid))
                    {
                        continue;
                    }

                    string last = guid.Contains(".") ? guid.Substring(guid.LastIndexOf('.') + 1) : guid;
                    if (last.Length >= 3 && contentId.IndexOf(last, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return guid;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static bool IsInfraGuid(string guid)
        {
            foreach (string fragment in InfraGuidFragments)
            {
                if (guid.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
