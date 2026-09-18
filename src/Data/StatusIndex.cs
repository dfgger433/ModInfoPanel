using System;
using System.Collections.Generic;
using CUCoreLib.Data;

namespace ModInfoPanel.Data
{
    /// <summary>记录“状态字段 → 修改它的物品/液体”，供 moodle 悬停框显示相关物品。</summary>
    internal static class StatusIndex
    {
        private const int MaxPerField = 16;

        private static readonly Dictionary<string, List<string>> ItemFields =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, List<string>> LiquidFields =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        private static readonly string[] Suffixes =
        {
            "amount", "level", "drugs", "blood", "tolerance", "offset"
        };

        public static void RecordItem(string itemId, ItemInfo stats)
        {
            if (string.IsNullOrWhiteSpace(itemId) || stats == null)
            {
                return;
            }

            Record(ItemFields, itemId, stats.useAction);
            Record(ItemFields, itemId, stats.useLimbAction);
        }

        public static void RecordLiquid(string liquidId, CustomLiquidInfo info)
        {
            if (string.IsNullOrWhiteSpace(liquidId) || info == null)
            {
                return;
            }

            Record(LiquidFields, liquidId, info.onDrink);
            Record(LiquidFields, liquidId, info.onApplyToLimb);
            Record(LiquidFields, liquidId, info.onInject);
        }

        public static List<string> GetRelatedItems(string fieldName)
        {
            return Get(ItemFields, fieldName);
        }

        public static List<string> GetRelatedLiquids(string fieldName)
        {
            return Get(LiquidFields, fieldName);
        }

        private static void Record(Dictionary<string, List<string>> map, string id, Delegate callback)
        {
            if (callback == null)
            {
                return;
            }

            List<EffectExtractor.Delta> deltas = EffectExtractor.GetDeltas(callback);
            foreach (EffectExtractor.Delta delta in deltas)
            {
                if (delta.IsCondition || string.IsNullOrWhiteSpace(delta.FieldName))
                {
                    continue;
                }

                if (!map.TryGetValue(delta.FieldName, out List<string> list))
                {
                    list = new List<string>();
                    map[delta.FieldName] = list;
                }

                if (list.Count < MaxPerField && !list.Contains(id))
                {
                    list.Add(id);
                }
            }
        }

        private static List<string> Get(Dictionary<string, List<string>> map, string fieldName)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                return result;
            }

            if (map.TryGetValue(fieldName, out List<string> exact))
            {
                AddRange(result, exact);
                return result;
            }

            string norm = Normalize(fieldName);
            if (norm.Length < 3)
            {
                return result;
            }

            foreach (KeyValuePair<string, List<string>> kv in map)
            {
                string key = Normalize(kv.Key);
                if (key.Length < 3)
                {
                    continue;
                }

                bool match = string.Equals(key, norm, StringComparison.OrdinalIgnoreCase)
                             || norm.StartsWith(key, StringComparison.OrdinalIgnoreCase)
                             || key.StartsWith(norm, StringComparison.OrdinalIgnoreCase)
                             || norm.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0
                             || key.IndexOf(norm, StringComparison.OrdinalIgnoreCase) >= 0;
                if (match)
                {
                    AddRange(result, kv.Value);
                }
            }

            return result;
        }

        private static void AddRange(List<string> target, List<string> source)
        {
            if (source == null)
            {
                return;
            }

            foreach (string id in source)
            {
                if (!string.IsNullOrWhiteSpace(id) && !target.Contains(id))
                {
                    target.Add(id);
                }
            }
        }

        private static string Normalize(string value)
        {
            string result = (value ?? "").ToLowerInvariant();
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (string suffix in Suffixes)
                {
                    if (result.Length > suffix.Length && result.EndsWith(suffix, StringComparison.Ordinal))
                    {
                        result = result.Substring(0, result.Length - suffix.Length);
                        changed = true;
                        break;
                    }
                }
            }

            return result;
        }
    }
}
