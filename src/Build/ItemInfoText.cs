using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using CUCoreLib.Data;
using ModInfoPanel.Data;
using ModInfoPanel.Localization;

namespace ModInfoPanel.Build
{
    internal static class ItemInfoText
    {
        private static readonly HashSet<string> KnownCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "medical", "drug", "tool", "container", "utility", "water", "food", "custom", "trash", "unobtainable", "nospawn"
        };

        public static string Build(string id, ItemInfo stats)
        {
            if (stats == null)
            {
                return null;
            }

            if (!ModContentIndex.IsModItem(id, stats) && !ModContentIndex.IsDescriptionMissing(stats, id))
            {
                return null;
            }

            bool showRecipes = Plugin.Cfg == null || Plugin.Cfg.ShowRecipes.Value;
            bool showSource = Plugin.Cfg == null || Plugin.Cfg.ShowSourceMod.Value;
            bool showCustomData = Plugin.Cfg != null && Plugin.Cfg.ShowCustomData.Value;

            StatusIndex.RecordItem(id, stats);
            ModContentIndex.TryGetCustomInfo(stats, out CustomItemInfo custom);

            List<string> blue = new List<string>();
            List<string> green = new List<string>();
            List<string> orange = new List<string>();
            List<string> yellow = new List<string>();
            List<string> traits = new List<string>();

            CollectEquipment(stats, blue);
            CollectEffects(stats, custom, green);
            EffectExtractor.CollectItem(stats, green, orange);
            AppendCustomDataLines(custom, "effects", green);
            CollectMetadata(stats, custom, green);
            AppendCustomDataLines(custom, "info", green);
            CollectCosts(stats, custom, orange);

            string acquire = ResolveAcquisition(stats, custom);
            if (!string.IsNullOrEmpty(acquire))
            {
                yellow.Add(Loc.T("acquire", "获取：") + acquire);
            }

            if (showRecipes)
            {
                yellow.AddRange(RecipeInfoText.BuildLines(id));
            }

            CollectTraits(stats, traits);
            AppendCustomDataLines(custom, "features", traits);

            Dedupe(green);
            Dedupe(orange);
            Dedupe(yellow);
            Dedupe(traits);

            StringBuilder sb = new StringBuilder();
            Format.AppendBlock(sb, Format.Blue, blue);
            Format.AppendBlock(sb, Format.Green, green);
            Format.AppendBlock(sb, Format.Orange, orange);
            Format.AppendBlock(sb, Format.Yellow, yellow);

            if (traits.Count > 0)
            {
                sb.Append(Format.Line(Format.Feature, Loc.T("features", "特点：") + string.Join("，", traits)));
            }

            if (showSource)
            {
                string owner = ModContentIndex.GetItemOwner(id);
                if (!string.IsNullOrWhiteSpace(owner))
                {
                    sb.Append(Format.Line(Format.Gray, Loc.T("source", "来源模组：") + owner));
                }
            }

            if (showCustomData)
            {
                AppendRawCustomData(sb, custom);
            }

            string result = sb.ToString().TrimEnd('\n');
            return result.Length == 0 ? null : result;
        }

        /// <summary>蓝色块：护甲/保温/损耗系数/装备槽/跳跃/容重比。</summary>
        private static void CollectEquipment(ItemInfo stats, List<string> lines)
        {
            if (stats.wearable)
            {
                if (stats.wearableArmor > 0f)
                {
                    lines.Add(Format.Pair(Loc.T("data_armor", "护甲"), Format.Num(stats.wearableArmor)));
                }

                if (stats.wearableIsolation > 0f)
                {
                    lines.Add(Format.Pair(Loc.T("data_insulation", "保温"), Format.Pct(stats.wearableIsolation)));
                }

                if (stats.wearableHitDurabilityLossMultiplier > 0f
                    && Math.Abs(stats.wearableHitDurabilityLossMultiplier - 1f) > 0.001f)
                {
                    lines.Add(Format.Pair(Loc.T("data_hit_loss", "损耗系数"),
                        Format.Num(stats.wearableHitDurabilityLossMultiplier)));
                }

                if (!string.IsNullOrWhiteSpace(stats.wearSlotId))
                {
                    lines.Add(Format.Pair(Loc.T("data_slot", "装备槽"), stats.wearSlotId));
                }
            }

            if (Math.Abs(stats.jumpHeightMultChange) > 0.0001f)
            {
                string sign = stats.jumpHeightMultChange > 0f ? "+" : "";
                lines.Add(Format.Pair(Loc.T("data_jump", "跳跃高度"), sign + Format.Pct(stats.jumpHeightMultChange)));
            }

            if (stats is LiquidItemInfo liquid && liquid.capacity > 0f && stats.weight > 0f)
            {
                lines.Add(Format.Pair(Loc.T("data_density", "容重比"),
                    Format.Num(stats.weight / liquid.capacity * 100f) + "u/100mL"));
            }
        }

        /// <summary>绿色块：效果数值 + 容量/液体 + 元数据。</summary>
        private static void CollectEffects(ItemInfo stats, CustomItemInfo custom, List<string> lines)
        {
            if (custom != null)
            {
                CollectBandage(custom.Bandage, lines);
                CollectTool(custom.Tool, lines);
                CollectGun(custom.Gun, lines);
                CollectSyringe(custom.Syringe, lines);
                CollectBattery(custom.Battery, lines);
                CollectLight(custom.Light, lines);
            }

            if (stats is LiquidItemInfo liquid)
            {
                if (liquid.capacity > 0f)
                {
                    lines.Add(Format.Pair(Loc.T("syringe_capacity", "容量"), Format.Num(liquid.capacity) + "mL"));
                }

                AppendContents(lines, liquid.defaultContents, Loc.T("syringe_contents", "默认液体"));
            }
        }

        /// <summary>绿色块：分类/价值/识别/品质/容器。</summary>
        private static void CollectMetadata(ItemInfo stats, CustomItemInfo custom, List<string> lines)
        {
            string category = CategoryLabel(stats.category);
            if (!string.IsNullOrWhiteSpace(category))
            {
                lines.Add(Format.Pair(Loc.T("data_category", "分类"), category));
            }

            if (stats.value > 0)
            {
                lines.Add(Format.Pair(Loc.T("data_value", "价值"), stats.value.ToString()));
            }

            if (stats.rec != null && stats.rec.min > 0)
            {
                lines.Add(Format.Pair(Loc.T("data_recognition", "识别所需INT"), stats.rec.min.ToString()));
            }

            if (stats.qualities != null && stats.qualities.Count > 0)
            {
                List<string> qualities = new List<string>();
                foreach (CraftingQuality quality in stats.qualities)
                {
                    if (quality == null || string.IsNullOrWhiteSpace(quality.id))
                    {
                        continue;
                    }

                    string name;
                    try
                    {
                        name = quality.LocaleName;
                    }
                    catch
                    {
                        name = quality.id;
                    }

                    qualities.Add(name + " (" + Format.Num(quality.amount) + ")");
                }

                if (qualities.Count > 0)
                {
                    lines.Add(Format.Pair(Loc.T("data_qualities", "品质"), string.Join(", ", qualities)));
                }
            }

            if (custom?.Container != null)
            {
                if (custom.Container.Capacity > 0f)
                {
                    lines.Add(Format.Pair(Loc.T("container_capacity", "最大承重"),
                        Format.Num(custom.Container.Capacity) + "u"));
                }

                if (custom.Container.MaxWeightPerItem > 0f)
                {
                    lines.Add(Format.Pair(Loc.T("container_max_item", "单件上限"),
                        Format.Num(custom.Container.MaxWeightPerItem) + "u"));
                }

                float reduction = 1f - custom.Container.EncumbranceReduction;
                if (reduction > 0.0001f)
                {
                    lines.Add(Format.Pair(Loc.T("container_encumbrance", "负重减免"), Format.Pct(reduction)));
                }
            }
        }

        /// <summary>橙色块：腐败 + 消耗类。</summary>
        private static void CollectCosts(ItemInfo stats, CustomItemInfo custom, List<string> lines)
        {
            float decayRate = stats.rotSpeed > 0f
                ? stats.rotSpeed * 60f
                : (stats.decayMinutes > 0f ? 100f / stats.decayMinutes : 0f);
            if (decayRate > 0f)
            {
                lines.Add(Format.Pair(Loc.T("data_decay", "腐败"),
                    Format.Num(decayRate) + "%/" + Loc.T("min", "分钟")));
            }

            if (custom?.Tool != null && custom.Tool.ConditionLossOnHit > 0f)
            {
                lines.Add(ConsumeLine(custom.Tool.ConditionLossOnHit));
            }

            if (custom?.Gun != null && custom.Gun.ConditionLossPerShot.HasValue
                && custom.Gun.ConditionLossPerShot.Value > 0f)
            {
                lines.Add(ConsumeLine(custom.Gun.ConditionLossPerShot.Value));
            }
        }

        private static string ConsumeLine(float fraction)
        {
            return Loc.T("consume", "消耗") + " " + Format.Pct(fraction) + " " + Loc.T("durability", "耐久");
        }

        private static void Dedupe(List<string> lines)
        {
            if (lines == null || lines.Count < 2)
            {
                return;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                if (!seen.Add(lines[i]))
                {
                    lines.RemoveAt(i);
                }
            }
        }

        private static void CollectTraits(ItemInfo stats, List<string> lines)
        {
            if (stats.destroyAtZeroCondition)
            {
                lines.Add(Loc.T("trait_destroy_zero", "耐久归零销毁"));
            }

            if (stats.scaleWeightWithCondition)
            {
                lines.Add(Loc.T("trait_scale_weight", "重量随耐久变化"));
            }

            if (stats.onlyHoldInHands)
            {
                lines.Add(Loc.T("trait_only_hands", "仅可手持"));
            }

            if (stats.autoAttack)
            {
                lines.Add(Loc.T("trait_auto_attack", "自动攻击"));
            }

            if (stats.ignoreDepression)
            {
                lines.Add(Loc.T("trait_ignore_depression", "无视抑郁"));
            }

            if (stats.combineable)
            {
                lines.Add(Loc.T("trait_combineable", "可合成"));
            }

            if (ModContentIndex.HasTag(stats, "cangetwet"))
            {
                lines.Add(Loc.T("trait_wet", "可被弄湿"));
            }
        }

        private static string ResolveAcquisition(ItemInfo stats, CustomItemInfo custom)
        {
            if (custom != null && TryGetCustomDataStrings(custom, "acquire", out List<string> overrideLines))
            {
                return string.Join("/", overrideLines);
            }

            List<string> sources = new List<string>();
            if (custom != null)
            {
                DropPool pools = custom.DropPool ?? DropPool.None;
                if (pools != DropPool.None)
                {
                    AddPool(sources, pools, DropPool.Corpse, "source_corpse", "尸体");
                    AddPool(sources, pools, DropPool.MedicalCrate, "source_medical", "医疗箱");
                    AddPool(sources, pools, DropPool.FoodCrate, "source_food", "食物箱");
                    AddPool(sources, pools, DropPool.ContainerCrate, "source_container", "物资箱");
                    if ((pools & (DropPool.Trader1 | DropPool.Trader2 | DropPool.Trader3)) != DropPool.None)
                    {
                        AddUnique(sources, Loc.T("source_trader", "交易"));
                    }

                    AddPool(sources, pools, DropPool.DropCapsule, "source_dropcapsule", "空投箱");
                    AddPool(sources, pools, DropPool.CapsuleContainer, "source_capsule", "维生箱");
                }

                if (custom.WorldSpawnPerChunk.HasValue && custom.WorldSpawnPerChunk.Value > 0f)
                {
                    AddUnique(sources, Loc.T("world_spawn", "世界生成") + " (" + Loc.T("per_chunk", "每区块") + " "
                                          + Format.Num(custom.WorldSpawnPerChunk.Value) + ")");
                }
            }

            if (sources.Count == 0)
            {
                foreach (string source in AcquisitionMap.Resolve(stats.category, stats.value))
                {
                    AddUnique(sources, source);
                }
            }

            return sources.Count == 0 ? null : string.Join("/", sources);
        }

        private static void AddPool(List<string> list, DropPool pools, DropPool flag, string key, string fallback)
        {
            if (pools.HasFlag(flag))
            {
                AddUnique(list, Loc.T(key, fallback));
            }
        }

        private static void AddUnique(List<string> list, string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !list.Contains(value))
            {
                list.Add(value);
            }
        }

        private static string CategoryLabel(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return null;
            }

            string key = category.Trim().ToLowerInvariant();
            if (KnownCategories.Contains(key))
            {
                return Loc.T("cat_" + key, category);
            }

            return category;
        }

        private static void AppendCustomDataLines(CustomItemInfo custom, string key, List<string> target)
        {
            if (TryGetCustomDataStrings(custom, key, out List<string> lines))
            {
                target.AddRange(lines);
            }
        }

        private static bool TryGetCustomDataStrings(CustomItemInfo custom, string key, out List<string> lines)
        {
            lines = null;
            if (custom?.CustomData == null || custom.CustomData.Count == 0)
            {
                return false;
            }

            foreach (KeyValuePair<string, object> kv in custom.CustomData)
            {
                if (!string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                lines = ToStringList(kv.Value);
                return lines.Count > 0;
            }

            return false;
        }

        private static List<string> ToStringList(object value)
        {
            List<string> list = new List<string>();
            if (value == null)
            {
                return list;
            }

            if (value is string text)
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    list.Add(text);
                }

                return list;
            }

            if (value is IEnumerable enumerable)
            {
                foreach (object entry in enumerable)
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    string line = entry.ToString();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        list.Add(line);
                    }
                }

                return list;
            }

            string single = value.ToString();
            if (!string.IsNullOrWhiteSpace(single))
            {
                list.Add(single);
            }

            return list;
        }

        private static void AppendRawCustomData(StringBuilder sb, CustomItemInfo custom)
        {
            if (custom?.CustomData == null || custom.CustomData.Count == 0)
            {
                return;
            }

            List<string> lines = new List<string>();
            foreach (KeyValuePair<string, object> kv in custom.CustomData)
            {
                if (IsConsumedCustomDataKey(kv.Key))
                {
                    continue;
                }

                lines.Add(kv.Key + " = " + (kv.Value?.ToString() ?? "null"));
            }

            if (lines.Count > 0)
            {
                Format.AppendBlock(sb, Format.Gray, lines);
            }
        }

        private static bool IsConsumedCustomDataKey(string key)
        {
            return string.Equals(key, "effects", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(key, "info", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(key, "acquire", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(key, "features", StringComparison.OrdinalIgnoreCase);
        }

        private static void CollectBandage(BandageProperties bandage, List<string> lines)
        {
            if (bandage == null)
            {
                return;
            }

            if (Math.Abs(bandage.SkinHealAmount) > 0.001f)
            {
                lines.Add(Format.Stat(Loc.T("bandage_skin", "皮肤恢复"), "+" + Format.Num(bandage.SkinHealAmount)));
            }

            if (Math.Abs(bandage.PainReduction) > 0.001f)
            {
                lines.Add(Format.Stat(Loc.T("bandage_pain", "疼痛"), "-" + Format.Num(bandage.PainReduction)));
            }

            if (Math.Abs(bandage.BandageSlowAmount) > 0.001f)
            {
                lines.Add(Format.Stat(Loc.T("bandage_bleed", "止血强度"), Format.Num(bandage.BandageSlowAmount)));
            }

            if (Math.Abs(bandage.BoneHealTimerReduction) > 0.001f)
            {
                lines.Add(Format.Stat(Loc.T("bandage_bone", "骨骼恢复"), Format.Num(bandage.BoneHealTimerReduction)));
            }

            if (Math.Abs(bandage.DislocationTimerReduction) > 0.001f)
            {
                lines.Add(Format.Stat(Loc.T("bandage_dislocation", "脱臼恢复"), Format.Num(bandage.DislocationTimerReduction)));
            }

            if (Math.Abs(bandage.Effectiveness - 8f) > 0.001f)
            {
                lines.Add(Format.Stat(Loc.T("bandage_effectiveness", "效率"), Format.Num(bandage.Effectiveness)));
            }
        }

        private static void CollectTool(ToolProperties tool, List<string> lines)
        {
            if (tool == null)
            {
                return;
            }

            lines.Add(Format.Stat(Loc.T("tool_damage", "伤害"), Format.Num(tool.Damage)));
            if (Math.Abs(tool.StructuralDamage - tool.Damage) > 0.001f)
            {
                lines.Add(Format.Stat(Loc.T("tool_structural", "结构伤害"), Format.Num(tool.StructuralDamage)));
            }

            if (tool.Cooldown > 0f)
            {
                lines.Add(Format.Stat(Loc.T("tool_cooldown", "冷却"), Format.Num(tool.Cooldown) + Loc.T("sec", "秒")));
            }

            if (tool.Distance > 0f)
            {
                lines.Add(Format.Stat(Loc.T("tool_distance", "距离"), Format.Num(tool.Distance)));
            }

            if (tool.KnockBack > 0f)
            {
                lines.Add(Format.Stat(Loc.T("tool_knockback", "击退"), Format.Num(tool.KnockBack)));
            }

            if (tool.StaminaUse > 0f)
            {
                lines.Add(Format.Stat(Loc.T("tool_stamina", "耐力消耗"), Format.Num(tool.StaminaUse)));
            }

            if (tool.Piercing)
            {
                lines.Add(Loc.T("tool_piercing", "穿透"));
            }

            if (tool.MetalMoreDamage)
            {
                lines.Add(Loc.T("tool_metal", "对金属额外伤害"));
            }
        }

        private static void CollectGun(GunProperties gun, List<string> lines)
        {
            if (gun == null)
            {
                return;
            }

            if (gun.AmmoType.HasValue)
            {
                lines.Add(Format.Pair(Loc.T("gun_ammo", "弹药类型"), AmmoLabel(gun.AmmoType.Value)));
            }

            if (gun.FiringMode.HasValue)
            {
                lines.Add(Format.Pair(Loc.T("gun_mode", "射击模式"), FireModeLabel(gun.FiringMode.Value)));
            }

            if (gun.FeedType.HasValue)
            {
                lines.Add(Format.Pair(Loc.T("gun_feed", "供弹方式"), FeedTypeLabel(gun.FeedType.Value)));
            }

            if (gun.MagCapacity.HasValue && gun.MagCapacity.Value > 0)
            {
                lines.Add(Format.Pair(Loc.T("gun_mag", "弹匣容量"), gun.MagCapacity.Value.ToString()));
            }

            if (gun.AnimalDamage.HasValue && gun.AnimalDamage.Value > 0f)
            {
                lines.Add(Format.Stat(Loc.T("gun_animal", "生物伤害"), Format.Num(gun.AnimalDamage.Value)));
            }

            if (gun.StructureDamage.HasValue && gun.StructureDamage.Value > 0f)
            {
                lines.Add(Format.Stat(Loc.T("gun_structural", "结构伤害"), Format.Num(gun.StructureDamage.Value)));
            }

            if (gun.KnockBack.HasValue && gun.KnockBack.Value > 0f)
            {
                lines.Add(Format.Stat(Loc.T("gun_knockback", "击退"), Format.Num(gun.KnockBack.Value)));
            }

            if (gun.Loudness.HasValue && gun.Loudness.Value > 0f)
            {
                lines.Add(Format.Stat(Loc.T("gun_loudness", "噪音"), Format.Num(gun.Loudness.Value)));
            }

            if (gun.ShotsPerFire.HasValue && gun.ShotsPerFire.Value > 1)
            {
                lines.Add(Format.Stat(Loc.T("gun_shots", "每次射击发数"), gun.ShotsPerFire.Value.ToString()));
            }

            if (gun.VerticalSpread.HasValue && gun.VerticalSpread.Value > 0f)
            {
                lines.Add(Format.Stat(Loc.T("gun_spread", "散布"), Format.Num(gun.VerticalSpread.Value)));
            }
        }

        private static void CollectSyringe(SyringeProperties syringe, List<string> lines)
        {
            if (syringe == null)
            {
                return;
            }

            if (syringe.Capacity > 0f)
            {
                lines.Add(Format.Pair(Loc.T("syringe_capacity", "容量"), Format.Num(syringe.Capacity) + "mL"));
            }

            if (syringe.AmountPerFullUse > 0f)
            {
                lines.Add(Format.Pair(Loc.T("syringe_amount", "单次用量"), Format.Num(syringe.AmountPerFullUse) + "mL"));
            }

            if (syringe.AutoFill)
            {
                lines.Add(Loc.T("syringe_autofill", "自动填充"));
            }

            AppendContents(lines, syringe.DefaultContents, Loc.T("syringe_contents", "默认液体"));
        }

        private static void CollectBattery(BatteryProperties battery, List<string> lines)
        {
            if (battery == null)
            {
                return;
            }

            if (battery.MaxCharge > 0f)
            {
                lines.Add(Format.Pair(Loc.T("battery_charge", "电量"), Format.Num(battery.MaxCharge)));
            }

            if (battery.StartCharge >= 0f)
            {
                lines.Add(Format.Pair(Loc.T("battery_start", "初始电量"), Format.Num(battery.StartCharge)));
            }

            if (!string.IsNullOrWhiteSpace(battery.BatteryType))
            {
                lines.Add(Format.Pair(Loc.T("battery_type", "电池类型"), battery.BatteryType));
            }
            else
            {
                lines.Add(Format.Pair(Loc.T("battery_type", "电池类型"), BatteryPresetLabel(battery.Preset)));
            }

            if (battery.SpawnWithBattery)
            {
                lines.Add(Loc.T("battery_spawn", "自带电池"));
            }
        }

        private static void CollectLight(LightProperties light, List<string> lines)
        {
            if (light == null)
            {
                return;
            }

            if (light.Intensity > 0f)
            {
                lines.Add(Format.Pair(Loc.T("light_intensity", "亮度"), Format.Num(light.Intensity)));
            }

            if (light.PointLightOuterRadius > 0f)
            {
                lines.Add(Format.Pair(Loc.T("light_radius", "半径"), Format.Num(light.PointLightOuterRadius)));
            }
        }

        private static void AppendContents(List<string> lines, List<LiquidStack> contents, string label)
        {
            if (contents == null || contents.Count == 0)
            {
                return;
            }

            List<string> parts = new List<string>();
            foreach (LiquidStack stack in contents)
            {
                if (stack == null || string.IsNullOrWhiteSpace(stack.liquidId))
                {
                    continue;
                }

                parts.Add(stack.liquidId + " (" + Format.Num(stack.amount) + "mL)");
            }

            if (parts.Count > 0)
            {
                lines.Add(Format.Pair(label, string.Join("、", parts)));
            }
        }

        private static string AmmoLabel(GunScript.AmmoType type)
        {
            return Loc.T("ammo_" + type.ToString().ToLowerInvariant(), type.ToString());
        }

        private static string FireModeLabel(GunScript.FiringMode mode)
        {
            return Loc.T("fire_" + mode.ToString().ToLowerInvariant(), mode.ToString());
        }

        private static string FeedTypeLabel(GunScript.FeedType type)
        {
            return Loc.T("feed_" + type.ToString().ToLowerInvariant(), type.ToString());
        }

        private static string BatteryPresetLabel(BatteryItem.BatteryPreset preset)
        {
            return Loc.T("battery_" + preset.ToString().ToLowerInvariant(), preset.ToString());
        }
    }
}
