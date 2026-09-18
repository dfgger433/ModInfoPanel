using System;
using System.Collections.Generic;
using System.Text;
using ModInfoPanel.Data;
using ModInfoPanel.Localization;

namespace ModInfoPanel.Build
{
    /// <summary>从 Recipes.recipes 生成 “制作(INT)：材料=结果” 行（wiki 风格，纯文本行）。</summary>
    internal static class RecipeInfoText
    {
        private const int MaxRecipesPerItem = 6;

        private sealed class IngredientGroup
        {
            public RecipeItem Item;
            public int Count;
        }

        public static List<string> BuildLines(string resultId)
        {
            List<string> lines = new List<string>();
            if (string.IsNullOrWhiteSpace(resultId))
            {
                return lines;
            }

            List<Recipe> recipes;
            try
            {
                recipes = Recipes.recipes;
            }
            catch
            {
                return lines;
            }

            if (recipes == null || recipes.Count == 0)
            {
                return lines;
            }

            string norm = ModContentIndex.NormalizeId(resultId);
            int shown = 0;
            foreach (Recipe recipe in recipes)
            {
                if (recipe?.result == null || string.IsNullOrWhiteSpace(recipe.result.id))
                {
                    continue;
                }

                if (!string.Equals(ModContentIndex.NormalizeId(recipe.result.id), norm,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                lines.Add(FormatRecipe(recipe));
                shown++;
                if (shown >= MaxRecipesPerItem)
                {
                    break;
                }
            }

            return lines;
        }

        private static string FormatRecipe(Recipe recipe)
        {
            List<IngredientGroup> groups = GroupIngredients(recipe.items);

            List<string> parts = new List<string>();
            foreach (IngredientGroup group in groups)
            {
                parts.Add(FormatIngredient(group));
            }

            string prefix = recipe.isRepair ? Loc.T("recipe_repair", "修复") : Loc.T("recipe", "制作");
            StringBuilder sb = new StringBuilder();
            sb.Append(prefix).Append('(').Append(recipe.INT).Append(")：");
            sb.Append(string.Join("+", parts));

            string result = FormatResult(recipe.result);
            if (!string.IsNullOrEmpty(result))
            {
                sb.Append('=').Append(result);
            }

            return sb.ToString();
        }

        private static List<IngredientGroup> GroupIngredients(List<RecipeItem> items)
        {
            List<IngredientGroup> groups = new List<IngredientGroup>();
            if (items == null)
            {
                return groups;
            }

            Dictionary<string, int> index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (RecipeItem item in items)
            {
                if (item == null)
                {
                    continue;
                }

                string key = BuildKey(item);
                if (index.TryGetValue(key, out int i))
                {
                    groups[i].Count++;
                }
                else
                {
                    index[key] = groups.Count;
                    groups.Add(new IngredientGroup { Item = item, Count = 1 });
                }
            }

            return groups;
        }

        private static string BuildKey(RecipeItem item)
        {
            string quality = item.quality == null ? "" : item.quality.id + "|" + Format.Num(item.quality.amount);
            return (item.isLiquid ? "L|" : "I|")
                   + (item.specificId ?? "")
                   + "|" + quality
                   + "|" + Format.Num(item.minimumCondition)
                   + "|" + (item.destroyItem ? "1" : "0");
        }

        private static string FormatIngredient(IngredientGroup group)
        {
            RecipeItem item = group.Item;
            int count = group.Count;

            if (item.isLiquid)
            {
                float total = item.minimumCondition * count;
                string name;
                if (item.specific && !string.IsNullOrWhiteSpace(item.specificId))
                {
                    name = LocalizeLiquid(item.specificId);
                }
                else
                {
                    string quality = item.quality != null ? QualityName(item.quality) : Loc.T("craft_any_liquid", "任意液体");
                    name = "(" + quality + ")";
                }

                return Format.Num(total) + "mL" + name;
            }

            if (item.specific && !string.IsNullOrWhiteSpace(item.specificId))
            {
                return count + LocalizeItem(item.specificId) + Condition(item);
            }

            string label = item.quality != null ? QualityName(item.quality) : Loc.T("craft_any_item", "任意物品");
            return count + "(" + label + ")" + Condition(item);
        }

        private static string Condition(RecipeItem item)
        {
            if (item.minimumCondition > 0f && Math.Abs(item.minimumCondition - 0.9f) > 0.001f)
            {
                return "(>=" + Format.Pct(item.minimumCondition) + ")";
            }

            return "";
        }

        private static string FormatResult(RecipeResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.id))
            {
                return null;
            }

            if (result.isLiquid)
            {
                return Format.Num(result.resultCondition) + "mL" + LocalizeLiquid(result.id);
            }

            string name = LocalizeItem(result.id);
            if (result.amount > 1)
            {
                name = result.amount + name;
            }

            if (result.resultCondition < 0.999f)
            {
                name += "(" + Format.Pct(result.resultCondition) + ")";
            }

            return name;
        }

        private static string QualityName(CraftingQuality quality)
        {
            try
            {
                string name = quality.LocaleName;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }
            catch
            {
            }

            return quality.id;
        }

        private static string LocalizeItem(string id)
        {
            try
            {
                string text = global::Locale.GetItem(id);
                if (!string.IsNullOrWhiteSpace(text) && !string.Equals(text, id, StringComparison.OrdinalIgnoreCase))
                {
                    return text;
                }
            }
            catch
            {
            }

            return ModContentIndex.NormalizeId(id);
        }

        private static string LocalizeLiquid(string id)
        {
            try
            {
                string text = global::Locale.GetOther(id);
                if (!string.IsNullOrWhiteSpace(text) && !string.Equals(text, id, StringComparison.OrdinalIgnoreCase))
                {
                    return text;
                }
            }
            catch
            {
            }

            return ModContentIndex.NormalizeId(id);
        }
    }
}
