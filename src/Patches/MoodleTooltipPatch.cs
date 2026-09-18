using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using ModInfoPanel.Build;
using ModInfoPanel.Data;
using ModInfoPanel.Localization;
using UnityEngine;

namespace ModInfoPanel.Patches
{
    /// <summary>给自定义 moodle 的悬停框追加：当前数值 / 阈值 / 相关物品。</summary>
    [HarmonyPatch(typeof(MoodleManager), "AddMoodle")]
    internal static class MoodleTooltipPatch
    {
        private static readonly Regex TagRegex = new Regex("<[^>]*>", RegexOptions.Compiled);

        [HarmonyPostfix]
        private static void Postfix(MoodleManager __instance, string name, string desc)
        {
            try
            {
                if (Plugin.Cfg == null || !Plugin.Cfg.StatusShowMoodleInfo.Value)
                {
                    return;
                }

                if (__instance == null || string.IsNullOrWhiteSpace(name))
                {
                    return;
                }

                MoodleStatusInfo info = MoodleDescriptions.GetStatusInfoByName(name);
                if (info == null)
                {
                    return;
                }

                UITooltip tooltip = FindTooltip(__instance, name);
                if (tooltip == null)
                {
                    return;
                }

                string extra = BuildExtra(info);
                if (!string.IsNullOrEmpty(extra))
                {
                    tooltip.tipDesc = (tooltip.tipDesc ?? "") + extra;
                }
            }
            catch (Exception ex)
            {
                Plugin.Debug("[MoodleTooltipPatch] " + ex);
            }
        }

        private static UITooltip FindTooltip(MoodleManager manager, string name)
        {
            if (manager.moodles == null)
            {
                return null;
            }

            int count = manager.moodles.childCount;
            int floor = Math.Max(0, count - 4);
            for (int i = count - 1; i >= floor; i--)
            {
                Transform child = manager.moodles.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                UITooltip tip = child.GetComponent<UITooltip>();
                if (tip == null)
                {
                    continue;
                }

                if (string.Equals(StripTags(tip.tipName), name, StringComparison.Ordinal))
                {
                    return tip;
                }
            }

            return null;
        }

        private static string BuildExtra(MoodleStatusInfo info)
        {
            StringBuilder sb = new StringBuilder();

            if (!string.IsNullOrEmpty(info.FieldTypeName) && !string.IsNullOrEmpty(info.FieldName)
                && StatusReader.TryRead(info.FieldTypeName, info.FieldName, out float value))
            {
                sb.Append('\n').Append(Format.Col(Format.Gray,
                    Format.Bar + Loc.T("moodle_current", "当前") + "："
                    + EffectExtractor.GetFieldLabel(info.FieldName) + " " + Format.Num(value)));
            }

            if (info.Levels.Count > 0)
            {
                List<string> parts = new List<string>();
                foreach (MoodleEntry entry in info.Levels)
                {
                    if (entry.Threshold.HasValue)
                    {
                        parts.Add(">" + Format.Num(entry.Threshold.Value) + " " + entry.Name);
                    }
                }

                if (parts.Count > 0)
                {
                    sb.Append('\n').Append(Format.Col(Format.Gray,
                        Format.Bar + Loc.T("moodle_thresholds", "阈值") + "：" + string.Join(" / ", parts)));
                }
            }

            List<string> names = new List<string>();
            foreach (string id in StatusIndex.GetRelatedItems(info.FieldName))
            {
                string localized = SafeItem(id);
                if (!string.IsNullOrWhiteSpace(localized) && !names.Contains(localized))
                {
                    names.Add(localized);
                }
            }

            foreach (string id in StatusIndex.GetRelatedLiquids(info.FieldName))
            {
                string localized = SafeLiquid(id);
                if (!string.IsNullOrWhiteSpace(localized) && !names.Contains(localized))
                {
                    names.Add(localized);
                }
            }

            if (names.Count > 8)
            {
                names = names.GetRange(0, 8);
            }

            if (names.Count > 0)
            {
                sb.Append('\n').Append(Format.Col(Format.Gray,
                    Format.Bar + Loc.T("moodle_related", "相关物品") + "：" + string.Join("、", names)));
            }

            return sb.ToString();
        }

        private static string SafeItem(string id)
        {
            try
            {
                string text = global::Locale.GetItem(id);
                return string.IsNullOrWhiteSpace(text) ? id : text;
            }
            catch
            {
                return id;
            }
        }

        private static string SafeLiquid(string id)
        {
            try
            {
                string text = global::Locale.GetOther(id);
                return string.IsNullOrWhiteSpace(text) ? id : text;
            }
            catch
            {
                return id;
            }
        }

        private static string StripTags(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0)
            {
                return text ?? "";
            }

            return TagRegex.Replace(text, "");
        }
    }
}
