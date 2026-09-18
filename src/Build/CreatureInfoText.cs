using System.Collections.Generic;
using System.Text;
using CUCoreLib.Data;
using ModInfoPanel.Data;
using ModInfoPanel.Localization;

namespace ModInfoPanel.Build
{
    internal static class CreatureInfoText
    {
        /// <summary>静态定义值版本（用于缓存）。runtimeHealth=true 时血量写成 {health} 占位符。</summary>
        public static string Build(CustomBuildingEntityDefinition def, bool runtimeHealth)
        {
            if (def == null)
            {
                return null;
            }

            return BuildInternal(
                def,
                runtimeHealth ? "{health}" : Format.Num(def.Health),
                def.AlwaysDrop,
                def.ItemsDropOnDestroy);
        }

        /// <summary>运行时实体版本（缓存关闭或运行时血量模式时使用）。</summary>
        public static string Build(BuildingEntity entity, CustomBuildingEntityDefinition def, bool runtimeHealth)
        {
            if (def == null)
            {
                return null;
            }

            string healthText = runtimeHealth
                ? "{health}"
                : Format.Num(entity != null ? entity.health : def.Health);
            ItemDrop[] alwaysDrop = entity != null ? entity.alwaysDrop : def.AlwaysDrop;
            ItemDrop[] randomDrop = entity != null ? entity.itemsDropOnDestroy : def.ItemsDropOnDestroy;
            return BuildInternal(def, healthText, alwaysDrop, randomDrop);
        }

        private static string BuildInternal(
            CustomBuildingEntityDefinition def,
            string healthText,
            ItemDrop[] alwaysDrop,
            ItemDrop[] randomDrop)
        {
            List<string> red = new List<string>();
            List<string> green = new List<string>();
            List<string> yellow = new List<string>();
            List<string> traits = new List<string>();

            red.Add(Format.Pair(Loc.T("creature_health", "生命"), healthText));

            List<string> drops = new List<string>();
            AppendDrops(drops, alwaysDrop, true);
            AppendDrops(drops, randomDrop, false);
            if (drops.Count > 0)
            {
                green.Add(Format.Pair(Loc.T("creature_drops", "掉落"), string.Join("、", drops)));
            }

            List<string> spawn = new List<string>();
            if (def.SpawnMinPerChunk > 0f || def.SpawnMaxPerChunk > 0f)
            {
                spawn.Add(Format.Pair(Loc.T("creature_per_chunk", "每区块"),
                    Format.Num(def.SpawnMinPerChunk) + "~" + Format.Num(def.SpawnMaxPerChunk)));
            }

            if (def.SpawnLayers >= 0)
            {
                spawn.Add(Format.Pair(Loc.T("creature_layers", "层级"), def.SpawnLayers.ToString()));
            }

            if (spawn.Count > 0)
            {
                yellow.Add(Format.Pair(Loc.T("creature_spawn", "生成"), string.Join(" | ", spawn)));
            }

            if (def.Animal)
            {
                traits.Add(Loc.T("creature_animal", "动物"));
            }

            if (def.Metallic)
            {
                traits.Add(Loc.T("creature_metallic", "金属"));
            }

            if (def.CantHit)
            {
                traits.Add(Loc.T("creature_cant_hit", "不可攻击"));
            }

            if (def.GenerationStyle == BuildingGenerationStyle.DropPod)
            {
                traits.Add(Loc.T("creature_drop_pod", "空投生成"));
            }

            StringBuilder sb = new StringBuilder();
            Format.AppendBlock(sb, Format.Red, red);
            Format.AppendBlock(sb, Format.Green, green);
            Format.AppendBlock(sb, Format.Yellow, yellow);

            if (traits.Count > 0)
            {
                sb.Append(Format.Line(Format.Feature, Loc.T("features", "特点：") + string.Join("，", traits)));
            }

            if (Plugin.Cfg == null || Plugin.Cfg.ShowSourceMod.Value)
            {
                string owner = ModContentIndex.GetCreatureOwner(def.ID);
                if (!string.IsNullOrWhiteSpace(owner))
                {
                    sb.Append(Format.Line(Format.Gray, Loc.T("source", "来源模组：") + owner));
                }
            }

            return sb.ToString().TrimEnd('\n');
        }

        private static void AppendDrops(List<string> list, ItemDrop[] drops, bool guaranteed)
        {
            if (drops == null)
            {
                return;
            }

            foreach (ItemDrop drop in drops)
            {
                if (drop == null || string.IsNullOrWhiteSpace(drop.id))
                {
                    continue;
                }

                string text = ModContentIndex.NormalizeId(drop.id);
                if (!guaranteed && drop.chance < 0.999f)
                {
                    text += " (" + Format.Pct(drop.chance) + ")";
                }

                if (drop.conditionMin < 0.999f || drop.conditionMax < 0.999f)
                {
                    text += " [" + Loc.T("creature_condition", "耐久") + " "
                            + Format.Num(drop.conditionMin) + "~" + Format.Num(drop.conditionMax) + "]";
                }

                list.Add(text);
            }
        }
    }
}
