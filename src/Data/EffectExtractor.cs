using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CUCoreLib.Data;
using ModInfoPanel.Build;
using ModInfoPanel.Localization;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace ModInfoPanel.Data
{
    /// <summary>
    /// 从 useAction / useLimbAction / 液体 onDrink 等委托的 IL 中提取数值效果。
    /// 以 stfld 为锚点回溯：支持两种乘法顺序、Mathf.Min/Max/Clamp 包裹、*= /=、
    /// 以及嵌套委托（DoTimedOp 等延迟效果）。
    /// </summary>
    internal static class EffectExtractor
    {
        private enum Kind
        {
            Item,
            Drink,
            Apply,
            Inject
        }

        internal sealed class Delta
        {
            public string FieldName;
            public string FieldKey;
            public string Label;
            public float Value;
            public bool Scaled;
            public bool Delayed;
            public bool Multiply;
            public bool Divide;
            public bool IsCondition;
        }

        private const int MaxDepth = 3;

        private static readonly string[] Suffixes =
        {
            "amount", "level", "drugs", "blood", "tolerance", "offset"
        };

        private static readonly Dictionary<string, AssemblyDefinition> Assemblies =
            new Dictionary<string, AssemblyDefinition>(StringComparer.Ordinal);

        private static readonly Dictionary<string, MethodDefinition> Methods =
            new Dictionary<string, MethodDefinition>(StringComparer.Ordinal);

        private static readonly Dictionary<string, List<Delta>> Cache =
            new Dictionary<string, List<Delta>>(StringComparer.Ordinal);

        public static void CollectItem(ItemInfo stats, List<string> green, List<string> orange)
        {
            if (stats == null)
            {
                return;
            }

            Scan(stats.useAction, Kind.Item, green, orange);
            Scan(stats.useLimbAction, Kind.Item, green, orange);
        }

        public static void CollectLiquid(CustomLiquidInfo info, List<string> green)
        {
            if (info == null)
            {
                return;
            }

            Scan(info.onDrink, Kind.Drink, green, null);
            Scan(info.onApplyToLimb, Kind.Apply, green, null);
            Scan(info.onInject, Kind.Inject, green, null);
        }

        private static void Scan(Delegate callback, Kind kind, List<string> green, List<string> orange)
        {
            if (callback == null)
            {
                return;
            }

            MethodInfo method = callback.Method;
            if (method == null)
            {
                return;
            }

            List<Delta> deltas = GetDeltas(method);
            if (deltas == null || deltas.Count == 0)
            {
                return;
            }

            AssemblyDefinition assembly = GetAssembly(method);
            bool showStatus = Plugin.Cfg == null || Plugin.Cfg.StatusShowDescriptions.Value;
            HashSet<string> usedStatus = new HashSet<string>(StringComparer.Ordinal);

            Dictionary<string, float> totals = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            foreach (Delta entry in deltas)
            {
                if (entry.IsCondition || entry.Multiply)
                {
                    continue;
                }

                totals.TryGetValue(entry.FieldName, out float sum);
                totals[entry.FieldName] = sum + entry.Value;
            }

            foreach (Delta delta in deltas)
            {
                if (Math.Abs(delta.Value) < 0.000001f)
                {
                    continue;
                }

                string status = null;
                if (showStatus && !delta.IsCondition)
                {
                    totals.TryGetValue(delta.FieldName, out float total);
                    status = MoodleDescriptions.GetForValue(assembly, delta.FieldName, total);
                    if (!string.IsNullOrEmpty(status) && !usedStatus.Add(status))
                    {
                        status = null;
                    }
                }

                string line = FormatDelta(delta, kind, status);
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                List<string> target = delta.IsCondition && orange != null ? orange : green;
                if (!target.Contains(line))
                {
                    target.Add(line);
                }
            }
        }

        private static string FormatDelta(Delta delta, Kind kind, string status)
        {
            string suffix = string.IsNullOrEmpty(status) ? "" : "（" + status + "）";

            if (delta.IsCondition)
            {
                if (delta.Value < 0f)
                {
                    return Loc.T("consume", "消耗") + " " + Format.Pct(-delta.Value) + " "
                           + Loc.T("durability", "耐久");
                }

                return Loc.T("durability", "耐久") + " +" + Format.Pct(delta.Value);
            }

            string number;
            if (delta.Multiply)
            {
                number = (delta.Divide ? "÷" : "×") + Format.Num(Math.Abs(delta.Value));
            }
            else
            {
                string sign = delta.Value >= 0f ? "+" : "-";
                number = sign + Format.Num(Math.Abs(delta.Value));
                if (delta.Scaled && kind == Kind.Item)
                {
                    number += "(" + Loc.T("eff_per100", "每100mL") + ")";
                }
            }

            string label = delta.Delayed
                ? Loc.T("eff_delayed", "延迟") + " " + delta.Label
                : delta.Label;

            if (kind == Kind.Item)
            {
                return Format.Stat(label, number) + suffix;
            }

            string prefix = kind == Kind.Drink
                ? Loc.T("eff_drink", "饮用")
                : kind == Kind.Apply
                    ? Loc.T("eff_apply", "外敷")
                    : Loc.T("eff_inject", "注射");
            if (delta.Scaled)
            {
                prefix += "(" + Loc.T("eff_per100", "每100mL") + ")";
            }

            return prefix + "：" + label + " " + number + suffix;
        }

        internal static List<Delta> GetDeltas(MethodInfo method)
        {
            string cacheKey = null;
            try
            {
                cacheKey = (method.DeclaringType?.Assembly?.Location ?? "") + "|" + method.MetadataToken;
            }
            catch
            {
            }

            if (!string.IsNullOrEmpty(cacheKey) && Cache.TryGetValue(cacheKey, out List<Delta> cached))
            {
                return cached;
            }

            List<Delta> result = new List<Delta>();
            try
            {
                MethodDefinition definition = FindMethod(method);
                if (definition != null)
                {
                    result = Aggregate(ExtractMethod(definition, new HashSet<string>(StringComparer.Ordinal), 0, false));
                }
            }
            catch (Exception ex)
            {
                Plugin.Debug("[EffectExtractor] " + ex.Message);
            }

            if (!string.IsNullOrEmpty(cacheKey))
            {
                Cache[cacheKey] = result;
            }

            return result;
        }

        internal static List<Delta> GetDeltas(Delegate callback)
        {
            if (callback == null)
            {
                return new List<Delta>();
            }

            return GetDeltas(callback.Method);
        }

        private static List<Delta> Aggregate(List<Delta> deltas)
        {
            Dictionary<string, Delta> map = new Dictionary<string, Delta>(StringComparer.Ordinal);
            List<Delta> order = new List<Delta>();
            foreach (Delta delta in deltas)
            {
                string key = delta.FieldKey + (delta.Multiply ? "|m" : "");
                if (map.TryGetValue(key, out Delta existing))
                {
                    existing.Value += delta.Value;
                }
                else
                {
                    map[key] = delta;
                    order.Add(delta);
                }
            }

            return order;
        }

        private static List<Delta> ExtractMethod(MethodDefinition method, HashSet<string> visited, int depth, bool delayed)
        {
            List<Delta> result = new List<Delta>();
            if (method == null || !method.HasBody)
            {
                return result;
            }

            string visitKey;
            try
            {
                visitKey = method.FullName;
            }
            catch
            {
                return result;
            }

            if (!visited.Add(visitKey))
            {
                return result;
            }

            var instructions = method.Body.Instructions;
            for (int i = 0; i < instructions.Count; i++)
            {
                Instruction instr = instructions[i];

                if (instr.OpCode.Code == Code.Ldftn || instr.OpCode.Code == Code.Ldvirtftn)
                {
                    if (depth < MaxDepth && instr.Operand is MethodReference target)
                    {
                        MethodDefinition nested = null;
                        try
                        {
                            nested = target.Resolve();
                        }
                        catch
                        {
                        }

                        if (nested != null)
                        {
                            result.AddRange(ExtractMethod(nested, visited, depth + 1, true));
                        }
                    }

                    continue;
                }

                if (instr.OpCode.Code != Code.Stfld)
                {
                    continue;
                }

                FieldReference field = instr.Operand as FieldReference;
                if (field == null || IsIgnoredType(field))
                {
                    continue;
                }

                int opIndex = i - 1;
                if (opIndex < 0)
                {
                    continue;
                }

                bool negate = false;
                bool multiply = false;
                bool divide = false;

                if (IsAddSub(instructions[opIndex], out negate))
                {
                }
                else if (IsMathfCall(instructions[opIndex]) && opIndex - 1 >= 0
                         && IsAddSub(instructions[opIndex - 1], out negate))
                {
                    opIndex--;
                }
                else if (instructions[opIndex].OpCode.Code == Code.Mul)
                {
                    multiply = true;
                }
                else if (instructions[opIndex].OpCode.Code == Code.Div)
                {
                    multiply = true;
                    divide = true;
                }
                else
                {
                    continue;
                }

                float value = 0f;
                bool hasConst = false;
                bool hasMul = false;
                bool fieldRead = false;
                int limit = Math.Max(0, opIndex - 10);
                for (int j = opIndex - 1; j >= limit; j--)
                {
                    Instruction ins = instructions[j];
                    Code code = ins.OpCode.Code;
                    if (!hasConst && TryConst(ins, out float v))
                    {
                        value = v;
                        hasConst = true;
                        continue;
                    }

                    if (!hasMul && code == Code.Mul)
                    {
                        hasMul = true;
                        continue;
                    }

                    if (code == Code.Ldfld && SameField(ins.Operand as FieldReference, field))
                    {
                        fieldRead = true;
                        break;
                    }
                }

                if (!hasConst || !fieldRead)
                {
                    continue;
                }

                if (!multiply && negate)
                {
                    value = -value;
                }

                string label = LabelFor(field);
                result.Add(new Delta
                {
                    FieldName = field.Name,
                    FieldKey = FieldKey(field) + (delayed ? "|d" : ""),
                    Label = label,
                    Value = value,
                    Scaled = !multiply && hasMul,
                    Delayed = delayed,
                    Multiply = multiply,
                    Divide = divide,
                    IsCondition = string.Equals(label, "__condition__", StringComparison.Ordinal)
                });
            }

            return result;
        }

        private static MethodDefinition FindMethod(MethodInfo method)
        {
            AssemblyDefinition assembly = GetAssembly(method);
            if (assembly == null)
            {
                return null;
            }

            string location = assembly.MainModule.FileName;
            string key = location + "|" + method.MetadataToken;
            if (Methods.TryGetValue(key, out MethodDefinition cached))
            {
                return cached;
            }

            MethodDefinition definition = null;
            try
            {
                definition = assembly.MainModule.LookupToken(method.MetadataToken) as MethodDefinition;
            }
            catch
            {
            }

            if (definition != null)
            {
                Methods[key] = definition;
            }

            return definition;
        }

        private static AssemblyDefinition GetAssembly(MethodInfo method)
        {
            try
            {
                string location = method.DeclaringType?.Assembly?.Location;
                if (string.IsNullOrEmpty(location) || !File.Exists(location))
                {
                    return null;
                }

                if (!Assemblies.TryGetValue(location, out AssemblyDefinition assembly))
                {
                    assembly = AssemblyDefinition.ReadAssembly(location);
                    Assemblies[location] = assembly;
                }

                return assembly;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsIgnoredType(FieldReference field)
        {
            string ns = field.DeclaringType?.Namespace ?? "";
            if (ns.StartsWith("UnityEngine", StringComparison.Ordinal)
                || ns.StartsWith("System", StringComparison.Ordinal)
                || ns.StartsWith("TMPro", StringComparison.Ordinal))
            {
                return true;
            }

            string name = field.DeclaringType?.Name ?? "";
            return name.StartsWith("<", StringComparison.Ordinal);
        }

        private static bool IsMathfCall(Instruction instr)
        {
            if (instr.OpCode.Code != Code.Call && instr.OpCode.Code != Code.Callvirt)
            {
                return false;
            }

            MethodReference method = instr.Operand as MethodReference;
            if (method == null || method.DeclaringType?.FullName != "UnityEngine.Mathf")
            {
                return false;
            }

            switch (method.Name)
            {
                case "Min":
                case "Max":
                case "Clamp":
                case "Clamp01":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsAddSub(Instruction instr, out bool negate)
        {
            negate = false;
            switch (instr.OpCode.Code)
            {
                case Code.Add:
                    return true;
                case Code.Sub:
                    negate = true;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryConst(Instruction instr, out float value)
        {
            value = 0f;
            switch (instr.OpCode.Code)
            {
                case Code.Ldc_R4:
                    value = (float)instr.Operand;
                    return true;
                case Code.Ldc_R8:
                    value = (float)(double)instr.Operand;
                    return true;
                case Code.Ldc_I4:
                case Code.Ldc_I4_S:
                    value = Convert.ToSingle(instr.Operand);
                    return true;
                case Code.Ldc_I4_M1:
                    value = -1f;
                    return true;
                case Code.Ldc_I4_0:
                    value = 0f;
                    return true;
                case Code.Ldc_I4_1:
                    value = 1f;
                    return true;
                case Code.Ldc_I4_2:
                    value = 2f;
                    return true;
                case Code.Ldc_I4_3:
                    value = 3f;
                    return true;
                case Code.Ldc_I4_4:
                    value = 4f;
                    return true;
                case Code.Ldc_I4_5:
                    value = 5f;
                    return true;
                case Code.Ldc_I4_6:
                    value = 6f;
                    return true;
                case Code.Ldc_I4_7:
                    value = 7f;
                    return true;
                case Code.Ldc_I4_8:
                    value = 8f;
                    return true;
                default:
                    return false;
            }
        }

        private static bool SameField(FieldReference a, FieldReference b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            return string.Equals(a.Name, b.Name, StringComparison.Ordinal)
                   && string.Equals(a.DeclaringType?.FullName, b.DeclaringType?.FullName, StringComparison.Ordinal);
        }

        private static string FieldKey(FieldReference field)
        {
            return (field.DeclaringType?.FullName ?? "") + "::" + field.Name;
        }

        private static string LabelFor(FieldReference field)
        {
            string name = field.Name ?? "";
            if (string.Equals(name, "condition", StringComparison.OrdinalIgnoreCase)
                && string.Equals(field.DeclaringType?.Name, "Item", StringComparison.Ordinal))
            {
                return "__condition__";
            }

            return ResolveLabel(name);
        }

        internal static string GetFieldLabel(string fieldName)
        {
            return ResolveLabel(fieldName);
        }

        private static string ResolveLabel(string name)
        {
            name = name ?? "";
            string lower = name.ToLowerInvariant();
            if (LocaleTable.Has("eff_" + lower))
            {
                return Loc.T("eff_" + lower, name);
            }

            foreach (string suffix in Suffixes)
            {
                if (lower.Length > suffix.Length && lower.EndsWith(suffix, StringComparison.Ordinal))
                {
                    string stem = lower.Substring(0, lower.Length - suffix.Length);
                    if (LocaleTable.Has("eff_" + stem))
                    {
                        return Loc.T("eff_" + stem, stem);
                    }
                }
            }

            foreach (string suffix in Suffixes)
            {
                if (lower.Length > suffix.Length && lower.EndsWith(suffix, StringComparison.Ordinal))
                {
                    return name.Substring(0, name.Length - suffix.Length);
                }
            }

            return name;
        }
    }
}
