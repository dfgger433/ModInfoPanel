using System;
using CUCoreLib.Data;
using CUCoreLib.Registries;
using HarmonyLib;
using ModInfoPanel.Build;
using ModInfoPanel.Configuration;
using ModInfoPanel.Data;
using UnityEngine;

namespace ModInfoPanel.Patches
{
    [HarmonyPatch(typeof(FluidManager), "LiquidName")]
    [HarmonyAfter("net.cucorelib")]
    internal static class FluidHoverPatch
    {
        [HarmonyPostfix]
        private static void Postfix(FluidManager __instance, Vector2Int pos, ref (string, string) __result)
        {
            try
            {
                InfoConfig cfg = Plugin.Cfg;
                if (cfg == null || !cfg.Enabled.Value || !cfg.ShowLiquids.Value || __instance == null)
                {
                    return;
                }

                byte worldByte = __instance.GetLiquid(pos.x, pos.y);
                if (worldByte == 0)
                {
                    return;
                }

                if (!LiquidTileRegistry.TryGetTileId(worldByte, out string id) || string.IsNullOrWhiteSpace(id))
                {
                    return;
                }

                if (!LiquidTileRegistry.TryGet(id, out CustomLiquidTileInfo info) || info == null)
                {
                    return;
                }

                string block = InfoCache.GetTileText(id, info);
                if (string.IsNullOrEmpty(block))
                {
                    return;
                }

                string desc = __result.Item2 ?? "";
                if (desc.IndexOf(ItemHoverPatch.Marker, StringComparison.Ordinal) >= 0)
                {
                    return;
                }

                bool missing = string.IsNullOrWhiteSpace(desc)
                               || string.Equals(desc, id, StringComparison.OrdinalIgnoreCase)
                               || string.Equals(desc, id + "dsc", StringComparison.OrdinalIgnoreCase);
                switch (cfg.DescriptionMode.Value)
                {
                    case DescriptionMode.FillMissing:
                        if (!missing)
                        {
                            return;
                        }

                        break;
                    case DescriptionMode.Replace:
                        desc = "";
                        break;
                }

                string name = __result.Item1;
                if (string.IsNullOrWhiteSpace(name))
                {
                    try
                    {
                        name = global::Locale.GetOther(id);
                    }
                    catch
                    {
                        name = id;
                    }
                }

                __result = (name, (desc.Length == 0 ? "" : desc + "\n\n") + ItemHoverPatch.Marker + block);
            }
            catch (Exception ex)
            {
                Plugin.Debug("[FluidHoverPatch] " + ex);
            }
        }
    }
}
