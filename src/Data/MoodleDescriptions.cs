using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace ModInfoPanel.Data
{
    internal sealed class MoodleEntry
    {
        public int Intensity;
        public string Name;
        public string Description;
        public float? Threshold;
        public bool HasLowerBound;
        public string FieldName;
        public string FieldTypeName;
    }

    internal sealed class MoodleStatusInfo
    {
        public string Key;
        public string FieldName;
        public string FieldTypeName;
        public List<MoodleEntry> Entries = new List<MoodleEntry>();
        public List<MoodleEntry> Levels = new List<MoodleEntry>();
    }

    /// <summary>
    /// 扫描 mod 程序集中的 MoodleRegistry.AddMoodle / AddAnimatedMoodle 调用，
    /// 解析 (key, 名称, 说明) 以及守卫条件里的阈值（如 cannabisAmount > 100）。
    /// </summary>
    internal static class MoodleDescriptions
    {
        private static readonly Dictionary<string, Dictionary<string, List<MoodleEntry>>> Maps =
            new Dictionary<string, Dictionary<string, List<MoodleEntry>>>(StringComparer.Ordinal);

        private static readonly Dictionary<string, string> Cache =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private static readonly Dictionary<string, AssemblyDefinition> Assemblies =
            new Dictionary<string, AssemblyDefinition>(StringComparer.Ordinal);

        private static readonly Dictionary<string, MoodleStatusInfo> NameIndex =
            new Dictionary<string, MoodleStatusInfo>(StringComparer.OrdinalIgnoreCase);

        private static readonly Regex KeyPattern = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        private static readonly string[] NormalizeSuffixes =
        {
            "amount", "level", "drugs", "blood", "tolerance", "offset"
        };

        private static readonly string[] SkippedAssemblyPrefixes =
        {
            "Unity", "System", "mscorlib", "netstandard", "Mono", "BepInEx", "0Harmony",
            "CUCoreLib", "ModInfoPanel", "Newtonsoft", "TMPro", "Microsoft", "nunit"
        };

        private static bool _nameIndexBuilt;

        public static string Get(AssemblyDefinition assembly, string fieldName)
        {
            MoodleStatusInfo info = ResolveInfo(assembly, fieldName);
            if (info == null)
            {
                return null;
            }

            MoodleEntry best = PickBest(info.Entries, true) ?? PickBest(info.Entries, false);
            return best == null ? null : FormatEntry(best);
        }

        /// <summary>按数值匹配等级（阈值 ≤ 数值中最大者）。</summary>
        public static string GetForValue(AssemblyDefinition assembly, string fieldName, float value)
        {
            MoodleStatusInfo info = ResolveInfo(assembly, fieldName);
            if (info == null)
            {
                return null;
            }

            if (info.Levels.Count > 0)
            {
                MoodleEntry best = null;
                foreach (MoodleEntry entry in info.Levels)
                {
                    if (!entry.Threshold.HasValue || entry.Threshold.Value > value)
                    {
                        continue;
                    }

                    if (best == null || entry.Threshold.Value > best.Threshold.Value)
                    {
                        best = entry;
                    }
                }

                return best == null ? null : FormatEntry(best);
            }

            MoodleEntry fallback = PickBest(info.Entries, true) ?? PickBest(info.Entries, false);
            return fallback == null ? null : FormatEntry(fallback);
        }

        public static MoodleStatusInfo GetStatusInfoByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            BuildNameIndex();
            string clean = StripTags(name).Trim();
            NameIndex.TryGetValue(clean, out MoodleStatusInfo info);
            return info;
        }

        private static string FormatEntry(MoodleEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Name))
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(entry.Description)
                ? entry.Name
                : entry.Name + "：" + entry.Description;
        }

        private static MoodleStatusInfo ResolveInfo(AssemblyDefinition assembly, string fieldName)
        {
            Dictionary<string, List<MoodleEntry>> map = GetMap(assembly);
            if (map.Count == 0)
            {
                return null;
            }

            string norm = Normalize(fieldName);
            if (norm.Length < 3)
            {
                return null;
            }

            string bestKey = null;
            int bestLength = 0;
            foreach (KeyValuePair<string, List<MoodleEntry>> kv in map)
            {
                string key = Normalize(kv.Key);
                if (key.Length < 3)
                {
                    continue;
                }

                bool match = string.Equals(key, norm, StringComparison.OrdinalIgnoreCase)
                             || norm.StartsWith(key, StringComparison.OrdinalIgnoreCase)
                             || key.StartsWith(norm, StringComparison.OrdinalIgnoreCase)
                             || norm.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0
                             || key.IndexOf(norm, StringComparison.OrdinalIgnoreCase) >= 0;
                if (match && key.Length > bestLength)
                {
                    bestKey = kv.Key;
                    bestLength = key.Length;
                }
            }

            return bestKey == null ? null : BuildInfo(bestKey, map[bestKey]);
        }

        private static MoodleStatusInfo BuildInfo(string key, List<MoodleEntry> entries)
        {
            MoodleStatusInfo info = new MoodleStatusInfo { Key = key };
            foreach (MoodleEntry entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Name))
                {
                    continue;
                }

                info.Entries.Add(entry);
                if (entry.HasLowerBound && entry.Threshold.HasValue && !IsWithdrawal(entry.Name))
                {
                    info.Levels.Add(entry);
                }

                if (string.IsNullOrEmpty(info.FieldName) && !string.IsNullOrEmpty(entry.FieldName))
                {
                    info.FieldName = entry.FieldName;
                    info.FieldTypeName = entry.FieldTypeName;
                }
            }

            info.Levels.Sort((a, b) => a.Threshold.Value.CompareTo(b.Threshold.Value));
            return info;
        }

        private static Dictionary<string, List<MoodleEntry>> GetMap(AssemblyDefinition assembly)
        {
            string moduleKey = GetModuleKey(assembly);
            if (moduleKey == null)
            {
                return new Dictionary<string, List<MoodleEntry>>(StringComparer.OrdinalIgnoreCase);
            }

            if (!Maps.TryGetValue(moduleKey, out Dictionary<string, List<MoodleEntry>> map))
            {
                map = Scan(assembly);
                Maps[moduleKey] = map;
            }

            return map;
        }

        private static string GetModuleKey(AssemblyDefinition assembly)
        {
            try
            {
                return assembly.MainModule?.FileName ?? assembly.Name?.Name;
            }
            catch
            {
                return assembly.Name?.Name;
            }
        }

        private static void BuildNameIndex()
        {
            if (_nameIndexBuilt)
            {
                return;
            }

            _nameIndexBuilt = true;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!ShouldScan(assembly))
                {
                    continue;
                }

                AssemblyDefinition definition = GetAssemblyDefinition(assembly);
                if (definition == null)
                {
                    continue;
                }

                Dictionary<string, List<MoodleEntry>> map = GetMap(definition);
                foreach (KeyValuePair<string, List<MoodleEntry>> kv in map)
                {
                    MoodleStatusInfo info = BuildInfo(kv.Key, kv.Value);
                    foreach (MoodleEntry entry in kv.Value)
                    {
                        if (!string.IsNullOrEmpty(entry.Name) && !NameIndex.ContainsKey(entry.Name))
                        {
                            NameIndex[entry.Name] = info;
                        }
                    }
                }
            }
        }

        private static bool ShouldScan(Assembly assembly)
        {
            string name;
            try
            {
                name = assembly.GetName().Name ?? "";
            }
            catch
            {
                return false;
            }

            foreach (string prefix in SkippedAssemblyPrefixes)
            {
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static AssemblyDefinition GetAssemblyDefinition(Assembly assembly)
        {
            string location;
            try
            {
                location = assembly.Location;
            }
            catch
            {
                return null;
            }

            if (string.IsNullOrEmpty(location) || !File.Exists(location))
            {
                return null;
            }

            if (Assemblies.TryGetValue(location, out AssemblyDefinition definition))
            {
                return definition;
            }

            try
            {
                definition = AssemblyDefinition.ReadAssembly(location);
                Assemblies[location] = definition;
            }
            catch
            {
                definition = null;
            }

            return definition;
        }

        private static string StripTags(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0)
            {
                return text ?? "";
            }

            return Regex.Replace(text, "<[^>]*>", "");
        }

        private static bool IsWithdrawal(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            return name.IndexOf("戒断", StringComparison.Ordinal) >= 0
                   || name.IndexOf("withdraw", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Normalize(string value)
        {
            string result = (value ?? "").ToLowerInvariant();
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (string suffix in NormalizeSuffixes)
                {
                    if (result.Length > suffix.Length && result.EndsWith(suffix, StringComparison.Ordinal))
                    {
                        result = result.Substring(0, result.Length - suffix.Length);
                        changed = true;
                        break;
                    }
                }
            }

            return result;
        }

        private static MoodleEntry PickBest(List<MoodleEntry> entries, bool skipWithdrawal)
        {
            MoodleEntry best = null;
            int bestScore = int.MinValue;
            foreach (MoodleEntry entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Name))
                {
                    continue;
                }

                if (skipWithdrawal && IsWithdrawal(entry.Name))
                {
                    continue;
                }

                int score = SeverityScore(entry.Name) * 10 + entry.Intensity;
                if (best == null || score > bestScore)
                {
                    best = entry;
                    bestScore = score;
                }
            }

            return best;
        }

        private static int SeverityScore(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return 0;
            }

            if (name.IndexOf("极重", StringComparison.Ordinal) >= 0
                || name.IndexOf("extreme", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 4;
            }

            if (name.IndexOf("重度", StringComparison.Ordinal) >= 0
                || name.IndexOf("严重", StringComparison.Ordinal) >= 0
                || name.IndexOf("severe", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("中毒", StringComparison.Ordinal) >= 0
                || name.IndexOf("poison", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("overdose", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("fatal", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 3;
            }

            if (name.IndexOf("中度", StringComparison.Ordinal) >= 0
                || name.IndexOf("moderate", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 2;
            }

            if (name.IndexOf("轻度", StringComparison.Ordinal) >= 0
                || name.IndexOf("轻微", StringComparison.Ordinal) >= 0
                || name.IndexOf("mild", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("light", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 1;
            }

            return 0;
        }

        private static Dictionary<string, List<MoodleEntry>> Scan(AssemblyDefinition assembly)
        {
            Dictionary<string, List<MoodleEntry>> map = new Dictionary<string, List<MoodleEntry>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (ModuleDefinition module in assembly.Modules)
                {
                    foreach (TypeDefinition type in module.Types)
                    {
                        ScanType(type, map);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Debug("[MoodleDescriptions] " + ex.Message);
            }

            return map;
        }

        private static void ScanType(TypeDefinition type, Dictionary<string, List<MoodleEntry>> map)
        {
            if (type == null)
            {
                return;
            }

            foreach (MethodDefinition method in type.Methods)
            {
                ScanMethod(method, map);
            }

            foreach (TypeDefinition nested in type.NestedTypes)
            {
                ScanType(nested, map);
            }
        }

        private static void ScanMethod(MethodDefinition method, Dictionary<string, List<MoodleEntry>> map)
        {
            if (method == null || !method.HasBody)
            {
                return;
            }

            var instructions = method.Body.Instructions;
            for (int i = 0; i < instructions.Count; i++)
            {
                Instruction instr = instructions[i];
                if (instr.OpCode.Code != Code.Call && instr.OpCode.Code != Code.Callvirt)
                {
                    continue;
                }

                MethodReference called = instr.Operand as MethodReference;
                if (called == null || called.DeclaringType?.FullName != "CUCoreLib.Registries.MoodleRegistry")
                {
                    continue;
                }

                if (called.Name != "AddMoodle" && called.Name != "AddAnimatedMoodle")
                {
                    continue;
                }

                ParseCall(instructions, i, map);
            }
        }

        private static void ParseCall(Mono.Collections.Generic.Collection<Instruction> instructions, int callIndex,
            Dictionary<string, List<MoodleEntry>> map)
        {
            List<string> strings = new List<string>();
            int firstStringIndex = -1;
            int limit = Math.Max(0, callIndex - 40);
            for (int j = callIndex - 1; j >= limit; j--)
            {
                Instruction ins = instructions[j];

                if ((ins.OpCode.Code == Code.Call || ins.OpCode.Code == Code.Callvirt)
                    && ins.Operand is MethodReference previous
                    && previous.DeclaringType?.FullName == "CUCoreLib.Registries.MoodleRegistry"
                    && (previous.Name == "AddMoodle" || previous.Name == "AddAnimatedMoodle"))
                {
                    break;
                }

                if (ins.OpCode.Code == Code.Ldstr && ins.Operand is string text)
                {
                    strings.Insert(0, text);
                    firstStringIndex = j;
                }
            }

            if (strings.Count < 2)
            {
                return;
            }

            string key = null;
            for (int i = strings.Count - 1; i >= 0; i--)
            {
                if (IsKeyLike(strings[i]))
                {
                    key = strings[i];
                    strings.RemoveAt(i);
                    break;
                }
            }

            string name = null;
            int nameIndex = -1;
            for (int i = 0; i < strings.Count; i++)
            {
                if (LooksLikeAsset(strings[i]))
                {
                    continue;
                }

                name = strings[i];
                nameIndex = i;
                break;
            }

            if (name == null)
            {
                return;
            }

            string description = null;
            for (int i = nameIndex + 1; i < strings.Count; i++)
            {
                if (LooksLikeAsset(strings[i]))
                {
                    continue;
                }

                description = strings[i];
                break;
            }

            int intensity = FindIntensity(instructions, callIndex, firstStringIndex);
            MoodleEntry entry = new MoodleEntry
            {
                Intensity = intensity,
                Name = name,
                Description = description
            };
            ExtractThreshold(instructions, callIndex, entry);

            string mapKey = string.IsNullOrWhiteSpace(key) ? name : key;
            if (!map.TryGetValue(mapKey, out List<MoodleEntry> list))
            {
                list = new List<MoodleEntry>();
                map[mapKey] = list;
            }

            list.Add(entry);
        }

        /// <summary>回溯条件比较，取最近的下界阈值（如 cannabisAmount > 50 → 50；支持 && 区间）。</summary>
        private static void ExtractThreshold(Mono.Collections.Generic.Collection<Instruction> instructions, int callIndex,
            MoodleEntry entry)
        {
            float? lower = null;
            float? upper = null;
            string fieldName = null;
            string fieldType = null;
            int callOffset = instructions[callIndex].Offset;
            int limit = Math.Max(0, callIndex - 60);

            for (int j = callIndex - 1; j >= limit; j--)
            {
                Instruction ins = instructions[j];

                if ((ins.OpCode.Code == Code.Call || ins.OpCode.Code == Code.Callvirt)
                    && ins.Operand is MethodReference previous
                    && previous.DeclaringType?.FullName == "CUCoreLib.Registries.MoodleRegistry"
                    && (previous.Name == "AddMoodle" || previous.Name == "AddAnimatedMoodle"))
                {
                    break;
                }

                if ((ins.OpCode.Code == Code.Br || ins.OpCode.Code == Code.Br_S)
                    && ins.Operand is Instruction branchTarget && branchTarget.Offset > callOffset)
                {
                    // 当前条件块结束。
                    break;
                }

                if (!IsCompare(ins.OpCode.Code) && !IsDirectCompareBranch(ins.OpCode.Code))
                {
                    continue;
                }

                int constIndex = j - 1;
                if (constIndex < 0 || !TryConst(instructions[constIndex], out float value))
                {
                    continue;
                }

                FieldReference field = FindFieldBefore(instructions, constIndex);
                if (field == null)
                {
                    continue;
                }

                if (fieldName == null)
                {
                    fieldName = field.Name;
                    fieldType = field.DeclaringType?.FullName;
                }
                else if (!string.Equals(fieldName, field.Name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (IsLowerComparison(ins.OpCode.Code))
                {
                    if (!lower.HasValue)
                    {
                        lower = value;
                    }

                    break;
                }

                if (!upper.HasValue)
                {
                    upper = value;
                }
            }

            entry.FieldName = fieldName;
            entry.FieldTypeName = fieldType;
            if (lower.HasValue)
            {
                entry.Threshold = lower;
                entry.HasLowerBound = true;
            }
            else if (upper.HasValue)
            {
                entry.Threshold = upper;
                entry.HasLowerBound = false;
            }
        }

        private static bool IsLowerComparison(Code code)
        {
            switch (code)
            {
                // 比较指令：cgt 表示 x > V（配合 brfalse 落空执行）。
                case Code.Cgt:
                case Code.Cgt_Un:
                // 直接比较分支的条件是取反的：ble/blt 落空 = 大于/大于等于。
                case Code.Ble:
                case Code.Ble_S:
                case Code.Ble_Un:
                case Code.Ble_Un_S:
                case Code.Blt:
                case Code.Blt_S:
                case Code.Blt_Un:
                case Code.Blt_Un_S:
                    return true;
                default:
                    return false;
            }
        }

        private static FieldReference FindFieldBefore(Mono.Collections.Generic.Collection<Instruction> instructions,
            int index)
        {
            int limit = Math.Max(0, index - 6);
            for (int i = index - 1; i >= limit; i--)
            {
                Code code = instructions[i].OpCode.Code;
                if (code == Code.Ldfld || code == Code.Ldsfld)
                {
                    return instructions[i].Operand as FieldReference;
                }
            }

            return null;
        }

        private static bool IsTrivial(Instruction instr)
        {
            switch (instr.OpCode.Code)
            {
                case Code.Nop:
                case Code.Ldloc:
                case Code.Ldloc_S:
                case Code.Ldloc_0:
                case Code.Ldloc_1:
                case Code.Ldloc_2:
                case Code.Ldloc_3:
                case Code.Stloc:
                case Code.Stloc_S:
                case Code.Stloc_0:
                case Code.Stloc_1:
                case Code.Stloc_2:
                case Code.Stloc_3:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsBranch(Code code)
        {
            switch (code)
            {
                case Code.Brfalse:
                case Code.Brfalse_S:
                case Code.Brtrue:
                case Code.Brtrue_S:
                case Code.Bgt:
                case Code.Bgt_S:
                case Code.Bgt_Un:
                case Code.Bgt_Un_S:
                case Code.Bge:
                case Code.Bge_S:
                case Code.Bge_Un:
                case Code.Bge_Un_S:
                case Code.Blt:
                case Code.Blt_S:
                case Code.Blt_Un:
                case Code.Blt_Un_S:
                case Code.Ble:
                case Code.Ble_S:
                case Code.Ble_Un:
                case Code.Ble_Un_S:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsDirectCompareBranch(Code code)
        {
            switch (code)
            {
                case Code.Bgt:
                case Code.Bgt_S:
                case Code.Bgt_Un:
                case Code.Bgt_Un_S:
                case Code.Bge:
                case Code.Bge_S:
                case Code.Bge_Un:
                case Code.Bge_Un_S:
                case Code.Blt:
                case Code.Blt_S:
                case Code.Blt_Un:
                case Code.Blt_Un_S:
                case Code.Ble:
                case Code.Ble_S:
                case Code.Ble_Un:
                case Code.Ble_Un_S:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsCompare(Code code)
        {
            return code == Code.Cgt || code == Code.Cgt_Un
                   || code == Code.Clt || code == Code.Clt_Un;
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

        private static int FindIntensity(Mono.Collections.Generic.Collection<Instruction> instructions, int callIndex,
            int firstStringIndex)
        {
            int start = firstStringIndex >= 0 ? firstStringIndex : callIndex;
            int limit = Math.Max(0, start - 20);
            for (int j = start - 1; j >= limit; j--)
            {
                if (TryInt(instructions[j], out int value))
                {
                    return value;
                }
            }

            return 0;
        }

        private static bool TryInt(Instruction instr, out int value)
        {
            value = 0;
            switch (instr.OpCode.Code)
            {
                case Code.Ldc_I4:
                case Code.Ldc_I4_S:
                    value = Convert.ToInt32(instr.Operand);
                    return true;
                case Code.Ldc_I4_M1:
                    value = -1;
                    return true;
                case Code.Ldc_I4_0:
                    value = 0;
                    return true;
                case Code.Ldc_I4_1:
                    value = 1;
                    return true;
                case Code.Ldc_I4_2:
                    value = 2;
                    return true;
                case Code.Ldc_I4_3:
                    value = 3;
                    return true;
                case Code.Ldc_I4_4:
                    value = 4;
                    return true;
                case Code.Ldc_I4_5:
                    value = 5;
                    return true;
                case Code.Ldc_I4_6:
                    value = 6;
                    return true;
                case Code.Ldc_I4_7:
                    value = 7;
                    return true;
                case Code.Ldc_I4_8:
                    value = 8;
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsKeyLike(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 64)
            {
                return false;
            }

            if (text.IndexOf('.') >= 0 || text.IndexOf('/') >= 0 || text.IndexOf('\\') >= 0)
            {
                return false;
            }

            return KeyPattern.IsMatch(text);
        }

        private static bool LooksLikeAsset(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }

            if (text.IndexOf('/') >= 0 || text.IndexOf('\\') >= 0)
            {
                return true;
            }

            string lower = text.ToLowerInvariant();
            return lower.EndsWith(".png") || lower.EndsWith(".jpg") || lower.EndsWith(".jpeg")
                   || lower.EndsWith(".gif") || lower.EndsWith(".bmp") || lower.EndsWith(".asset");
        }
    }
}
