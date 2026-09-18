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
    [HarmonyPatch(typeof(BuildingEntity), "Start")]
    [HarmonyAfter("net.cucorelib")]
    internal static class BuildingPatch
    {
        [HarmonyPrefix]
        private static void Prefix(BuildingEntity __instance)
        {
            try
            {
                if (!ShouldHandle(__instance))
                {
                    return;
                }

                // 阻止原版 Start / CUCoreLib postfix 覆盖我们稍后写入的描述。
                __instance.skipDescriptionSet = true;
            }
            catch (Exception ex)
            {
                Plugin.Debug("[BuildingPatch:Prefix] " + ex);
            }
        }

        [HarmonyPostfix]
        private static void Postfix(BuildingEntity __instance)
        {
            try
            {
                if (!ShouldHandle(__instance))
                {
                    return;
                }

                string id = ModContentIndex.NormalizeId(__instance.id);
                if (!BuildingEntityRegistry.TryGetDefinition(id, out CustomBuildingEntityDefinition def) || def == null)
                {
                    return;
                }

                string block = InfoCache.GetCreatureText(id, def, __instance);
                if (string.IsNullOrEmpty(block))
                {
                    return;
                }

                string baseDesc = def.Description;
                if (string.IsNullOrEmpty(baseDesc))
                {
                    try
                    {
                        baseDesc = global::Locale.GetBuilding(id + "dsc");
                    }
                    catch
                    {
                        baseDesc = null;
                    }

                    if (string.Equals(baseDesc, id + "dsc", StringComparison.OrdinalIgnoreCase))
                    {
                        baseDesc = null;
                    }
                }

                if (string.IsNullOrEmpty(baseDesc))
                {
                    baseDesc = __instance.description;
                }

                if (!string.IsNullOrEmpty(baseDesc)
                    && baseDesc.IndexOf(ItemHoverPatch.Marker, StringComparison.Ordinal) >= 0)
                {
                    return;
                }

                __instance.description = (string.IsNullOrEmpty(baseDesc) ? "" : baseDesc + "\n\n")
                                         + ItemHoverPatch.Marker + block;
                __instance.skipDescriptionSet = true;
            }
            catch (Exception ex)
            {
                Plugin.Debug("[BuildingPatch:Postfix] " + ex);
            }
        }

        private static bool ShouldHandle(BuildingEntity instance)
        {
            InfoConfig cfg = Plugin.Cfg;
            if (cfg == null || !cfg.Enabled.Value || !cfg.ShowCreatures.Value || instance == null)
            {
                return false;
            }

            string id = ModContentIndex.NormalizeId(instance.id);
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            return BuildingEntityRegistry.TryGetDefinition(id, out CustomBuildingEntityDefinition def)
                   && def != null
                   && def.Animal;
        }
    }
}
