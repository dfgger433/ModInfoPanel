using System;
using HarmonyLib;
using ModInfoPanel.Data;

namespace ModInfoPanel.Patches
{
    /// <summary>Recipes.recipes 在进入对局时才生成（CUCoreLib 在此 postfix 注入 mod 配方）。</summary>
    [HarmonyPatch(typeof(Recipes), "SetUpRecipes")]
    [HarmonyAfter("net.cucorelib")]
    internal static class RecipeReadyPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            try
            {
                InfoCache.RequestValidation();
            }
            catch (Exception ex)
            {
                Plugin.Debug("[RecipeReadyPatch] " + ex);
            }
        }
    }
}
