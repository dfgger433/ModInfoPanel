using System;
using System.Reflection;
using CUCoreLib.Data;
using CUCoreLib.Registries;
using HarmonyLib;
using ModInfoPanel.Data;

namespace ModInfoPanel.Patches
{
    /// <summary>
    /// UEI（自定义物品栏）用自己的 BuildTooltipDescription 构建菜单提示，不经过
    /// PlayerCamera.ItemHoverDescription，这里在其结果后追加同样的自动信息。
    /// 采用反射按名挂接，未安装 UEI 时静默跳过。
    /// </summary>
    internal static class UeiTooltipPatch
    {
        public static void Install(Harmony harmony)
        {
            try
            {
                Type panel = FindType("UeiPanel");
                if (panel == null)
                {
                    return;
                }

                MethodInfo target = panel.GetMethod(
                    "BuildTooltipDescription",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (target == null)
                {
                    return;
                }

                MethodInfo postfix = typeof(UeiTooltipPatch).GetMethod(
                    nameof(Postfix), BindingFlags.Static | BindingFlags.NonPublic);
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                Plugin.Log?.LogInfo("[ModInfoPanel] UEI 菜单提示补丁已挂接。");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("[ModInfoPanel] UEI 菜单提示补丁失败: " + ex.Message);
            }
        }

        private static Type FindType(string name)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type type = assembly.GetType(name, throwOnError: false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static void Postfix(object[] __args, ref string __result)
        {
            try
            {
                if (Plugin.Cfg == null || !Plugin.Cfg.Enabled.Value)
                {
                    return;
                }

                if (__args == null || __args.Length == 0 || __args[0] == null)
                {
                    return;
                }

                object entry = __args[0];
                Type entryType = entry.GetType();
                string id = entryType.GetField("Id")?.GetValue(entry) as string;
                if (string.IsNullOrWhiteSpace(id))
                {
                    return;
                }

                if (__result != null && __result.IndexOf(ItemHoverPatch.Marker, StringComparison.Ordinal) >= 0)
                {
                    return;
                }

                object kind = entryType.GetField("Kind")?.GetValue(entry);
                bool isLiquid = kind != null
                                && string.Equals(kind.ToString(), "Liquid", StringComparison.OrdinalIgnoreCase);

                string extra = null;
                if (isLiquid)
                {
                    if (LiquidRegistry.TryGetCustomInfo(id, out CustomLiquidInfo custom) && custom != null)
                    {
                        extra = InfoCache.GetLiquidText(id, custom);
                    }
                }
                else
                {
                    ItemInfo info = entryType.GetField("ItemInfo")?.GetValue(entry) as ItemInfo;
                    if (info != null)
                    {
                        extra = InfoCache.GetItemText(id, info);
                    }
                }

                if (string.IsNullOrEmpty(extra))
                {
                    return;
                }

                __result = (__result ?? "") + "\n\n" + ItemHoverPatch.Marker + extra;
            }
            catch (Exception ex)
            {
                Plugin.Debug("[UeiTooltipPatch] " + ex.Message);
            }
        }
    }
}
