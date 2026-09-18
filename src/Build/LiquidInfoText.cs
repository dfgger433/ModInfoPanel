using System;
using System.Collections.Generic;
using System.Text;
using CUCoreLib.Data;
using CUCoreLib.Registries;
using ModInfoPanel.Data;
using ModInfoPanel.Localization;

namespace ModInfoPanel.Build
{
    internal static class LiquidInfoText
    {
        /// <summary>液体详情（容器内 / 世界流体通用）：蓝=价值，绿=可用/注射，橙=警告。</summary>
        public static string BuildBlocks(CustomLiquidInfo info)
        {
            if (info == null)
            {
                return null;
            }

            List<string> blue = new List<string>();
            List<string> green = new List<string>();
            List<string> orange = new List<string>();

            if (Math.Abs(info.valuePerLiter) > 0.0001f)
            {
                blue.Add(Format.Pair(Loc.T("liquid_value", "每升价值"), Format.Num(info.valuePerLiter)));
            }

            if (info.healthUsable)
            {
                green.Add(Loc.T("liquid_health", "可用于伤口"));
            }

            if (info.injectable)
            {
                green.Add(Loc.T("liquid_injectable", "可注射"));
            }

            if (info.injectable && info.injectionSickness > 0.0001f
                && Math.Abs(info.injectionSickness - 1f) > 0.0001f)
            {
                orange.Add(Format.Pair(Loc.T("liquid_sickness", "注射病"), "x" + Format.Num(info.injectionSickness)));
            }

            if (info.unobtainable)
            {
                orange.Add(Loc.T("liquid_unobtainable", "无法获取"));
            }

            EffectExtractor.CollectLiquid(info, green);

            StringBuilder sb = new StringBuilder();
            Format.AppendBlock(sb, Format.Blue, blue);
            Format.AppendBlock(sb, Format.Green, green);
            Format.AppendBlock(sb, Format.Orange, orange);
            return sb.ToString();
        }

        public static string BuildForTile(string id, CustomLiquidTileInfo info)
        {
            if (info == null)
            {
                return null;
            }

            List<string> blue = new List<string>();
            List<string> green = new List<string>();
            List<string> yellow = new List<string>();

            if (Math.Abs(info.Buoyancy - 0.6f) > 0.0001f)
            {
                blue.Add(Format.Pair(Loc.T("liquid_buoyancy", "浮力"), Format.Num(info.Buoyancy)));
            }

            if (Math.Abs(info.Drag - 0.915f) > 0.0001f)
            {
                blue.Add(Format.Pair(Loc.T("liquid_drag", "阻力"), Format.Num(info.Drag)));
            }

            AddPerSecond(green, info.WetnessPerSecond, "liquid_wetness", "弄湿");
            AddPerSecond(green, info.TemperaturePerSecond, "liquid_temp", "温度");
            AddPerSecond(green, info.SicknessPerSecond, "liquid_disease", "疾病");
            AddPerSecond(green, info.DirtynessPerSecond, "liquid_dirt", "污垢");
            AddPerSecond(green, info.DisinfectPerSecond, "liquid_disinfect", "消毒");
            AddPerSecond(green, info.SlipPerSecond, "liquid_slip", "滑倒");
            AddPerSecond(green, info.RagdollBarDrainPerSecond, "liquid_ragdoll", "失控条");

            if (!info.PushBodies)
            {
                green.Add(Loc.T("liquid_no_push", "不推动身体"));
            }

            if (info.SpawnAmount > 0f)
            {
                yellow.Add(Format.Pair(Loc.T("liquid_spawn_amount", "生成量"), Format.Num(info.SpawnAmount)));
            }

            if (info.SpawnLayers >= 0)
            {
                yellow.Add(Format.Pair(Loc.T("liquid_spawn_layers", "生成层级"), info.SpawnLayers.ToString()));
            }

            if (!string.IsNullOrWhiteSpace(info.FillLiquidId))
            {
                yellow.Add(Format.Pair(Loc.T("liquid_fill", "填充液体"), info.FillLiquidId));
            }

            StringBuilder sb = new StringBuilder();
            Format.AppendBlock(sb, Format.Blue, blue);
            Format.AppendBlock(sb, Format.Green, green);
            Format.AppendBlock(sb, Format.Yellow, yellow);

            string liquidId = string.IsNullOrWhiteSpace(info.LiquidId) ? id : info.LiquidId;
            if (LiquidRegistry.TryGetCustomInfo(liquidId, out CustomLiquidInfo liquidInfo) && liquidInfo != null)
            {
                sb.Append(BuildBlocks(liquidInfo));
            }

            if (Plugin.Cfg == null || Plugin.Cfg.ShowSourceMod.Value)
            {
                string owner = ModContentIndex.GetLiquidOwner(id);
                if (!string.IsNullOrWhiteSpace(owner))
                {
                    sb.Append(Format.Line(Format.Gray, Loc.T("source", "来源模组：") + owner));
                }
            }

            return sb.ToString().TrimEnd('\n');
        }

        private static void AddPerSecond(List<string> lines, float value, string key, string fallback)
        {
            if (Math.Abs(value) > 0.0001f)
            {
                lines.Add(Format.Pair(Loc.T(key, "每秒" + fallback), Format.Num(value) + "/s"));
            }
        }
    }
}
