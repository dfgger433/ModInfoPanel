using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CUCoreLib.Data;
using CUCoreLib.Registries;
using ModInfoPanel.Build;
using ModInfoPanel.Configuration;
using ModInfoPanel.Localization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ModInfoPanel.Data
{
    /// <summary>
    /// mod 信息 JSON 缓存：
    /// - 首次启动生成 InfoCache/{kind}/{mod}/{id}.json；
    /// - 之后启动只校验一次，数据未变不写盘，变了只更新对应文件；
    /// - overrides.json 优先显示且跳过自动生成。
    /// </summary>
    internal static class InfoCache
    {
        private const int SchemaVersion = 1;
        private const string KindItems = "items";
        private const string KindLiquids = "liquids";
        private const string KindTiles = "liquidTiles";
        private const string KindCreatures = "creatures";
        private enum WriteResult
        {
            Unchanged,
            Created,
            Updated
        }

        private static readonly Dictionary<string, string> Items =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> Liquids =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> Tiles =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> Creatures =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static OverridesDto _overrides = new OverridesDto();
        private static DateTime _overridesWriteTime = DateTime.MinValue;
        private static float _nextOverrideCheck;
        private static string _cacheRoot;
        private static string _overridesPath;
        private static bool _initialized;
        private static bool _validated;
        private static bool _validationScheduled;
        private static int _lastRecipeCount = -1;
        private static string _memoryLanguage;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _cacheRoot = Path.Combine(Plugin.PluginDir, "InfoCache");
            string fileName = Plugin.Cfg?.CacheOverridesFile?.Value;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "overrides.json";
            }

            _overridesPath = Path.Combine(Plugin.PluginDir, fileName);
            ReloadOverrides(force: true);
        }

        public static IEnumerator ValidateWhenReady()
        {
            InfoConfig cfg = Plugin.Cfg;
            if (cfg == null || !cfg.CacheEnabled.Value)
            {
                yield break;
            }

            float timeout = 90f;
            while (timeout > 0f)
            {
                bool ready = false;
                try
                {
                    ready = Item.GlobalItems != null && Item.GlobalItems.Count > 0;
                }
                catch
                {
                }

                if (ready)
                {
                    break;
                }

                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            yield return null;
            yield return null;

            try
            {
                ValidateAndUpdate();
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("[ModInfoPanel] 缓存校验失败: " + ex.Message);
            }
        }

        public static void ValidateAndUpdate()
        {
            InfoConfig cfg = Plugin.Cfg;
            if (cfg == null || !cfg.CacheEnabled.Value)
            {
                return;
            }

            ReloadOverrides();
            bool force = cfg.CacheForceRebuild.Value;
            string language = LocaleTable.ActiveLanguage;

            Items.Clear();
            Liquids.Clear();
            Tiles.Clear();
            Creatures.Clear();

            int scanned = 0;
            int kept = 0;
            int created = 0;
            int updated = 0;
            int skipped = 0;

            HashSet<string> seenItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (Item.GlobalItems != null)
                {
                    foreach (KeyValuePair<string, ItemInfo> kv in Item.GlobalItems)
                    {
                        string id = ModContentIndex.NormalizeId(kv.Key);
                        if (string.IsNullOrWhiteSpace(id) || kv.Value == null || !seenItems.Add(id))
                        {
                            continue;
                        }

                        if (!ModContentIndex.IsModItem(id, kv.Value)
                            && !ModContentIndex.IsDescriptionMissing(kv.Value, id))
                        {
                            continue;
                        }

                        scanned++;
                        if (_overrides.Items.ContainsKey(id))
                        {
                            skipped++;
                            continue;
                        }

                        string text = ItemInfoText.Build(id, kv.Value);
                        if (string.IsNullOrEmpty(text))
                        {
                            continue;
                        }

                        CountResult(ProcessItemEntry(id, text, force, language), ref kept, ref created, ref updated);
                    }
                }

                foreach (string rawId in ItemRegistry.GetRegisteredItemIds())
                {
                    string id = ModContentIndex.NormalizeId(rawId);
                    if (string.IsNullOrWhiteSpace(id) || !seenItems.Add(id))
                    {
                        continue;
                    }

                    if (!ItemRegistry.TryGetCustomInfo(id, out CustomItemInfo info) || info == null)
                    {
                        continue;
                    }

                    scanned++;
                    if (_overrides.Items.ContainsKey(id))
                    {
                        skipped++;
                        continue;
                    }

                    string text = ItemInfoText.Build(id, info);
                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    CountResult(ProcessItemEntry(id, text, force, language), ref kept, ref created, ref updated);
                }
            }
            catch (Exception ex)
            {
                Plugin.Debug("[InfoCache] 物品校验: " + ex);
            }

            try
            {
                foreach (string rawId in LiquidRegistry.GetRegisteredLiquidIds())
                {
                    string id = ModContentIndex.NormalizeId(rawId);
                    if (string.IsNullOrWhiteSpace(id)
                        || !LiquidRegistry.TryGetCustomInfo(id, out CustomLiquidInfo info)
                        || info == null)
                    {
                        continue;
                    }

                    scanned++;
                    if (_overrides.Liquids.ContainsKey(id))
                    {
                        skipped++;
                        continue;
                    }

                    CountResult(ProcessLiquidEntry(id, info, force, language), ref kept, ref created, ref updated);
                }
            }
            catch (Exception ex)
            {
                Plugin.Debug("[InfoCache] 流体校验: " + ex);
            }

            try
            {
                foreach (string rawId in LiquidTileRegistry.GetRegisteredIds())
                {
                    string id = ModContentIndex.NormalizeId(rawId);
                    if (string.IsNullOrWhiteSpace(id)
                        || !LiquidTileRegistry.TryGet(id, out CustomLiquidTileInfo info)
                        || info == null)
                    {
                        continue;
                    }

                    scanned++;
                    if (_overrides.LiquidTiles.ContainsKey(id))
                    {
                        skipped++;
                        continue;
                    }

                    string text = LiquidInfoText.BuildForTile(id, info);
                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    CountResult(ProcessTileEntry(id, text, force, language), ref kept, ref created, ref updated);
                }
            }
            catch (Exception ex)
            {
                Plugin.Debug("[InfoCache] 世界流体校验: " + ex);
            }

            try
            {
                bool runtimeHealth = cfg.CreatureUseRuntimeValues.Value;
                foreach (KeyValuePair<string, CustomBuildingEntityDefinition> kv
                         in BuildingEntityRegistry.GetRegisteredDefinitions())
                {
                    CustomBuildingEntityDefinition def = kv.Value;
                    if (def == null || !def.Animal)
                    {
                        continue;
                    }

                    string id = ModContentIndex.NormalizeId(kv.Key);
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        continue;
                    }

                    scanned++;
                    if (_overrides.Creatures.ContainsKey(id))
                    {
                        skipped++;
                        continue;
                    }

                    string text = CreatureInfoText.Build(def, runtimeHealth);
                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    CountResult(ProcessCreatureEntry(id, text, runtimeHealth, force, language),
                        ref kept, ref created, ref updated);
                }
            }
            catch (Exception ex)
            {
                Plugin.Debug("[InfoCache] 生物校验: " + ex);
            }

            _validated = true;
            _memoryLanguage = language;
            _lastRecipeCount = GetRecipeCount();

            Plugin.Log?.LogInfo(
                $"[ModInfoPanel] 缓存校验: 扫描 {scanned}, 复用 {kept}, 新建 {created}, 更新 {updated}, 跳过(override) {skipped}");
        }

        /// <summary>配方/内容就绪后请求一次增量校验（同帧去重）。</summary>
        public static void RequestValidation()
        {
            InfoConfig cfg = Plugin.Cfg;
            if (cfg == null || !cfg.CacheEnabled.Value || _validationScheduled || Plugin.Instance == null)
            {
                return;
            }

            _validationScheduled = true;
            Plugin.Instance.StartCoroutine(CoValidateSoon());
        }

        private static IEnumerator CoValidateSoon()
        {
            yield return null;
            _validationScheduled = false;
            try
            {
                ValidateAndUpdate();
            }
            catch (Exception ex)
            {
                Plugin.Debug("[InfoCache] 延迟校验: " + ex);
            }
        }

        private static int GetRecipeCount()
        {
            try
            {
                return Recipes.recipes?.Count ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        public static string GetItemText(string id, ItemInfo stats)
        {
            string norm = ModContentIndex.NormalizeId(id);
            if (string.IsNullOrWhiteSpace(norm))
            {
                return null;
            }

            EnsureReady();
            if (TryGetOverride(_overrides.Items, norm, out string text))
            {
                return text;
            }

            InfoConfig cfg = Plugin.Cfg;
            if (cfg == null || !cfg.CacheEnabled.Value)
            {
                return ItemInfoText.Build(norm, stats);
            }

            if (Items.TryGetValue(norm, out text))
            {
                return text;
            }

            text = ItemInfoText.Build(norm, stats);
            if (!string.IsNullOrEmpty(text))
            {
                ProcessItemEntry(norm, text, force: false, LocaleTable.ActiveLanguage);
            }

            return text;
        }

        public static string GetLiquidText(string id, CustomLiquidInfo info)
        {
            string norm = ModContentIndex.NormalizeId(id);
            if (string.IsNullOrWhiteSpace(norm))
            {
                return null;
            }

            StatusIndex.RecordLiquid(norm, info);
            EnsureReady();
            if (TryGetOverride(_overrides.Liquids, norm, out string text))
            {
                return text;
            }

            InfoConfig cfg = Plugin.Cfg;
            if (cfg == null || !cfg.CacheEnabled.Value)
            {
                return LiquidInfoText.BuildBlocks(info);
            }

            if (Liquids.TryGetValue(norm, out text))
            {
                return text;
            }

            text = LiquidInfoText.BuildBlocks(info);
            if (!string.IsNullOrEmpty(text))
            {
                ProcessLiquidEntry(norm, info, force: false, LocaleTable.ActiveLanguage);
            }

            return text;
        }

        public static string GetTileText(string id, CustomLiquidTileInfo info)
        {
            string norm = ModContentIndex.NormalizeId(id);
            if (string.IsNullOrWhiteSpace(norm))
            {
                return null;
            }

            EnsureReady();
            if (TryGetOverride(_overrides.LiquidTiles, norm, out string text))
            {
                return text;
            }

            InfoConfig cfg = Plugin.Cfg;
            if (cfg == null || !cfg.CacheEnabled.Value)
            {
                return LiquidInfoText.BuildForTile(norm, info);
            }

            if (Tiles.TryGetValue(norm, out text))
            {
                return text;
            }

            text = LiquidInfoText.BuildForTile(norm, info);
            if (!string.IsNullOrEmpty(text))
            {
                ProcessTileEntry(norm, text, force: false, LocaleTable.ActiveLanguage);
            }

            return text;
        }

        public static string GetCreatureText(string id, CustomBuildingEntityDefinition def, BuildingEntity entity)
        {
            string norm = ModContentIndex.NormalizeId(id);
            if (string.IsNullOrWhiteSpace(norm) || def == null)
            {
                return null;
            }

            EnsureReady();
            InfoConfig cfg = Plugin.Cfg;
            bool runtimeHealth = cfg != null && cfg.CreatureUseRuntimeValues.Value;
            string text;

            if (TryGetOverride(_overrides.Creatures, norm, out text))
            {
                return SubstituteHealth(text, def, entity);
            }

            if (cfg == null || !cfg.CacheEnabled.Value)
            {
                return SubstituteHealth(CreatureInfoText.Build(entity, def, runtimeHealth), def, entity);
            }

            if (!Creatures.TryGetValue(norm, out text))
            {
                text = CreatureInfoText.Build(def, runtimeHealth);
                if (!string.IsNullOrEmpty(text))
                {
                    ProcessCreatureEntry(norm, text, runtimeHealth, force: false, LocaleTable.ActiveLanguage);
                }
            }

            return SubstituteHealth(text, def, entity);
        }

        public static string BuildContainerLiquidBlock(WaterContainerItem container)
        {
            if (container == null)
            {
                return null;
            }

            List<LiquidStack> stack;
            try
            {
                stack = container.stack;
            }
            catch
            {
                return null;
            }

            if (stack == null || stack.Count == 0)
            {
                return null;
            }

            StringBuilder sb = new StringBuilder();
            foreach (LiquidStack entry in stack)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.liquidId))
                {
                    continue;
                }

                if (!LiquidRegistry.TryGetCustomInfo(entry.liquidId, out CustomLiquidInfo info) || info == null)
                {
                    continue;
                }

                string text = GetLiquidText(entry.liquidId, info);
                if (!string.IsNullOrEmpty(text))
                {
                    sb.Append(text).Append('\n');
                }
            }

            if (sb.Length == 0)
            {
                return null;
            }

            return sb.ToString().TrimEnd('\n');
        }

        private static void EnsureReady()
        {
            if (!_initialized)
            {
                Initialize();
            }

            MaybeReloadOverrides();
            MaybeRefreshRecipes();

            if (!_validated)
            {
                return;
            }

            if (string.Equals(_memoryLanguage, LocaleTable.ActiveLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Items.Clear();
            Liquids.Clear();
            Tiles.Clear();
            Creatures.Clear();
            try
            {
                ValidateAndUpdate();
            }
            catch (Exception ex)
            {
                Plugin.Debug("[InfoCache] 语言切换重建: " + ex);
            }
        }

        private static string SubstituteHealth(string text, CustomBuildingEntityDefinition def, BuildingEntity entity)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf("{health}", StringComparison.Ordinal) < 0)
            {
                return text;
            }

            float health = entity != null ? entity.health : def.Health;
            return text.Replace("{health}", Format.Num(health));
        }

        private static WriteResult ProcessItemEntry(string id, string text, bool force, string language)
        {
            Items[id] = text;
            string owner = ModContentIndex.GetItemOwner(id);
            string path = PathFor(KindItems, owner, id);
            CacheFileDto dto = TryReadCache<CacheFileDto>(path);
            if (!force && HeaderMatches(dto, language) && string.Equals(dto.Text, text, StringComparison.Ordinal))
            {
                return WriteResult.Unchanged;
            }

            bool exists = dto != null || File.Exists(path);
            WriteCache(path, new CacheFileDto
            {
                SchemaVersion = SchemaVersion,
                PluginVersion = PluginInfo.PLUGIN_VERSION,
                Language = language,
                Kind = "item",
                Id = id,
                SourceMod = owner,
                GeneratedAt = DateTime.UtcNow.ToString("o"),
                Text = text
            });
            return exists ? WriteResult.Updated : WriteResult.Created;
        }

        private static WriteResult ProcessLiquidEntry(string id, CustomLiquidInfo info, bool force, string language)
        {
            StatusIndex.RecordLiquid(id, info);
            string text = LiquidInfoText.BuildBlocks(info);
            if (string.IsNullOrEmpty(text))
            {
                return WriteResult.Unchanged;
            }

            Liquids[id] = text;
            string owner = ModContentIndex.GetLiquidOwner(id);
            string path = PathFor(KindLiquids, owner, id);
            CacheFileDto dto = TryReadCache<CacheFileDto>(path);
            if (!force && HeaderMatches(dto, language) && string.Equals(dto.Text, text, StringComparison.Ordinal))
            {
                return WriteResult.Unchanged;
            }

            bool exists = dto != null || File.Exists(path);
            WriteCache(path, new CacheFileDto
            {
                SchemaVersion = SchemaVersion,
                PluginVersion = PluginInfo.PLUGIN_VERSION,
                Language = language,
                Kind = "liquid",
                Id = id,
                SourceMod = owner,
                GeneratedAt = DateTime.UtcNow.ToString("o"),
                Text = text
            });
            return exists ? WriteResult.Updated : WriteResult.Created;
        }

        private static WriteResult ProcessTileEntry(string id, string text, bool force, string language)
        {
            Tiles[id] = text;
            string owner = ModContentIndex.GetTileOwner(id);
            string path = PathFor(KindTiles, owner, id);
            CacheFileDto dto = TryReadCache<CacheFileDto>(path);
            if (!force && HeaderMatches(dto, language) && string.Equals(dto.Text, text, StringComparison.Ordinal))
            {
                return WriteResult.Unchanged;
            }

            bool exists = dto != null || File.Exists(path);
            WriteCache(path, new CacheFileDto
            {
                SchemaVersion = SchemaVersion,
                PluginVersion = PluginInfo.PLUGIN_VERSION,
                Language = language,
                Kind = "liquidTile",
                Id = id,
                SourceMod = owner,
                GeneratedAt = DateTime.UtcNow.ToString("o"),
                Text = text
            });
            return exists ? WriteResult.Updated : WriteResult.Created;
        }

        private static WriteResult ProcessCreatureEntry(string id, string text, bool runtimeHealth, bool force, string language)
        {
            Creatures[id] = text;
            string owner = ModContentIndex.GetCreatureOwner(id);
            string path = PathFor(KindCreatures, owner, id);
            CacheFileDto dto = TryReadCache<CacheFileDto>(path);
            if (!force
                && HeaderMatches(dto, language)
                && dto.RuntimeHealth == runtimeHealth
                && string.Equals(dto.Text, text, StringComparison.Ordinal))
            {
                return WriteResult.Unchanged;
            }

            bool exists = dto != null || File.Exists(path);
            WriteCache(path, new CacheFileDto
            {
                SchemaVersion = SchemaVersion,
                PluginVersion = PluginInfo.PLUGIN_VERSION,
                Language = language,
                Kind = "creature",
                Id = id,
                SourceMod = owner,
                GeneratedAt = DateTime.UtcNow.ToString("o"),
                RuntimeHealth = runtimeHealth,
                Text = text
            });
            return exists ? WriteResult.Updated : WriteResult.Created;
        }

        private static bool HeaderMatches(CacheFileDto dto, string language)
        {
            return dto != null
                   && dto.SchemaVersion == SchemaVersion
                   && string.Equals(dto.PluginVersion, PluginInfo.PLUGIN_VERSION, StringComparison.Ordinal)
                   && string.Equals(dto.Language, language, StringComparison.OrdinalIgnoreCase);
        }

        private static void CountResult(WriteResult result, ref int kept, ref int created, ref int updated)
        {
            switch (result)
            {
                case WriteResult.Created:
                    created++;
                    break;
                case WriteResult.Updated:
                    updated++;
                    break;
                default:
                    kept++;
                    break;
            }
        }

        private static string PathFor(string kind, string owner, string id)
        {
            return Path.Combine(_cacheRoot, kind, Sanitize(owner), Sanitize(id) + ".json");
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "custom_items";
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder(name.Length);
            foreach (char c in name.Trim())
            {
                sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            }

            string result = sb.ToString().Trim().TrimEnd('.');
            return string.IsNullOrEmpty(result) ? "custom_items" : result;
        }

        private static T TryReadCache<T>(string path) where T : class
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                return JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
            }
            catch
            {
                return null;
            }
        }

        private static void WriteCache(string path, object dto)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonConvert.SerializeObject(dto, Formatting.Indented);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, json, new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    File.Replace(tmp, path, null);
                }
                else
                {
                    File.Move(tmp, path);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(path, JsonConvert.SerializeObject(dto, Formatting.Indented),
                        new UTF8Encoding(false));
                }
                catch (Exception inner)
                {
                    Plugin.Debug("[InfoCache] 写缓存失败 " + path + ": " + inner.Message);
                }

                Plugin.Debug("[InfoCache] 原子写失败 " + path + ": " + ex.Message);
            }
        }

        private static void MaybeReloadOverrides()
        {
            if (Time.unscaledTime < _nextOverrideCheck)
            {
                return;
            }

            _nextOverrideCheck = Time.unscaledTime + 2f;
            ReloadOverrides();
        }

        private static void MaybeRefreshRecipes()
        {
            int count = GetRecipeCount();
            if (count == _lastRecipeCount)
            {
                return;
            }

            _lastRecipeCount = count;
            RequestValidation();
        }

        private static void ReloadOverrides(bool force = false)
        {
            try
            {
                if (!File.Exists(_overridesPath))
                {
                    if (force || _overridesWriteTime != DateTime.MinValue)
                    {
                        _overrides = new OverridesDto();
                        _overridesWriteTime = DateTime.MinValue;
                    }

                    return;
                }

                DateTime write = File.GetLastWriteTimeUtc(_overridesPath);
                if (!force && write == _overridesWriteTime)
                {
                    return;
                }

                _overridesWriteTime = write;
                OverridesDto parsed = ParseOverrides(File.ReadAllText(_overridesPath));
                _overrides = parsed ?? new OverridesDto();
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("[ModInfoPanel] overrides.json 读取失败: " + ex.Message);
                _overrides = new OverridesDto();
            }
        }

        private static OverridesDto ParseOverrides(string json)
        {
            OverridesDto result = new OverridesDto();
            JObject root = JObject.Parse(json);
            result.Items = ReadStringMap(root["items"]);
            result.LiquidTiles = ReadStringMap(root["liquidTiles"]);
            result.Creatures = ReadStringMap(root["creatures"]);

            if (root["liquids"] is JObject liquids)
            {
                foreach (JProperty prop in liquids.Properties())
                {
                    try
                    {
                        if (prop.Value.Type == JTokenType.String)
                        {
                            result.Liquids[prop.Name] = prop.Value.ToString();
                        }
                        else if (prop.Value is JObject obj)
                        {
                            // 兼容旧格式 {name, details}：details 按灰行块渲染。
                            List<string> details = obj["details"]?.ToObject<List<string>>() ?? new List<string>();
                            result.Liquids[prop.Name] = Format.Block(Format.Gray, details);
                        }
                    }
                    catch
                    {
                    }
                }
            }

            return result;
        }

        private static Dictionary<string, string> ReadStringMap(JToken token)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (token is JObject obj)
            {
                foreach (JProperty prop in obj.Properties())
                {
                    if (prop.Value.Type == JTokenType.String)
                    {
                        map[prop.Name] = prop.Value.ToString();
                    }
                }
            }

            return map;
        }

        private static bool TryGetOverride(Dictionary<string, string> map, string id, out string text)
        {
            text = null;
            if (map == null || string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            return map.TryGetValue(id, out text) && !string.IsNullOrEmpty(text);
        }
    }
}
