using System;
using HarmonyLib;
using ModInfoPanel.Build;
using ModInfoPanel.Configuration;
using ModInfoPanel.Data;
using ModInfoPanel.Localization;
using UnityEngine;

namespace ModInfoPanel.Patches
{
    [HarmonyPatch(typeof(PlayerCamera), "ItemHoverDescription")]
    internal static class ItemHoverPatch
    {
        internal const string Marker = "\u200B";

        [HarmonyPostfix]
        private static void Postfix(Item item, ref (string, string) __result)
        {
            try
            {
                InfoConfig cfg = Plugin.Cfg;
                if (cfg == null || !cfg.Enabled.Value || item == null)
                {
                    return;
                }

                if (cfg.ExpandOnly.Value
                    && !PlayerCamera.alwaysExpandDescriptions
                    && !Input.GetKey(KeyBinds.GetBind("expanddesc")))
                {
                    return;
                }

                ItemInfo stats;
                try
                {
                    stats = item.Stats;
                }
                catch
                {
                    return;
                }

                if (stats == null)
                {
                    return;
                }

                string id = ModContentIndex.NormalizeId(item.id);
                string auto = "";
                if (cfg.ShowItems.Value)
                {
                    auto += InfoCache.GetItemText(id, stats) ?? "";
                }

                if (cfg.ShowLiquids.Value)
                {
                    auto += InfoCache.BuildContainerLiquidBlock(item.GetComponent<WaterContainerItem>()) ?? "";
                }

                if (string.IsNullOrEmpty(auto))
                {
                    return;
                }

                string desc = __result.Item2 ?? "";
                if (desc.IndexOf(Marker, StringComparison.Ordinal) >= 0)
                {
                    return;
                }

                bool missing = ModContentIndex.IsDescriptionMissing(stats, id);
                bool prepend = false;
                switch (cfg.DescriptionMode.Value)
                {
                    case DescriptionMode.FillMissing:
                        if (!missing)
                        {
                            return;
                        }

                        break;
                    case DescriptionMode.Replace:
                        string baseDesc = stats.description ?? "";
                        if (!missing && baseDesc.Length > 0 && desc.StartsWith(baseDesc, StringComparison.Ordinal))
                        {
                            desc = desc.Substring(baseDesc.Length).TrimStart('\n');
                        }

                        prepend = true;
                        break;
                }

                auto = Marker + auto;
                if (prepend)
                {
                    desc = desc.TrimStart('\n');
                    __result.Item2 = desc.Length == 0 ? auto : auto + "\n\n" + desc;
                }
                else if (desc.Length == 0)
                {
                    __result.Item2 = auto;
                }
                else if (desc.EndsWith("\n", StringComparison.Ordinal))
                {
                    __result.Item2 = desc + auto;
                }
                else
                {
                    __result.Item2 = desc + "\n\n" + auto;
                }
            }
            catch (Exception ex)
            {
                Plugin.Debug("[ItemHoverPatch] " + ex);
            }
        }
    }
}
