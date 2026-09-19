using BepInEx.Configuration;

namespace ModInfoPanel.Configuration
{
    internal enum DescriptionMode
    {
        Append,
        FillMissing,
        Replace
    }

    internal enum LanguageMode
    {
        Auto,
        ZhCN,
        EN
    }

    internal sealed class InfoConfig
    {
        public readonly ConfigEntry<bool> Enabled;
        public readonly ConfigEntry<bool> ShowItems;
        public readonly ConfigEntry<bool> ShowLiquids;
        public readonly ConfigEntry<bool> ShowCreatures;
        public readonly ConfigEntry<bool> ExpandOnly;
        public readonly ConfigEntry<DescriptionMode> DescriptionMode;
        public readonly ConfigEntry<bool> ShowSourceMod;
        public readonly ConfigEntry<bool> ShowCustomData;
        public readonly ConfigEntry<bool> ShowRecipes;
        public readonly ConfigEntry<bool> StatusShowDescriptions;
        public readonly ConfigEntry<bool> StatusShowMoodleInfo;
        public readonly ConfigEntry<LanguageMode> Language;
        public readonly ConfigEntry<bool> CacheEnabled;
        public readonly ConfigEntry<bool> CacheForceRebuild;
        public readonly ConfigEntry<string> CacheOverridesFile;
        public readonly ConfigEntry<bool> CreatureUseRuntimeValues;
        public readonly ConfigEntry<bool> UseVanillaLocaleBaseline;
        public readonly ConfigEntry<bool> DebugLog;

        public InfoConfig(ConfigFile cfg)
        {
            Enabled = cfg.Bind(
                "General", "Enabled", true,
                "总开关：是否启用 ModInfoPanel。");

            ShowItems = cfg.Bind(
                "General", "ShowItems", true,
                "为 mod 物品追加自动生成的信息块。");

            ShowLiquids = cfg.Bind(
                "General", "ShowLiquids", true,
                "为 mod 流体追加自动生成的信息：仅在按住展开键（Shift）时显示在对应液体描述之后、性质之前；世界流体悬停与液体条目始终生效。");

            ShowCreatures = cfg.Bind(
                "General", "ShowCreatures", true,
                "为 CUCoreLib 注册的自定义动物追加自动生成的信息。");

            ExpandOnly = cfg.Bind(
                "General", "ExpandOnly", false,
                "true = 仅按住展开描述键（默认 Shift）时显示自动信息；false = 始终显示。");

            DescriptionMode = cfg.Bind(
                "General", "DescriptionMode", Configuration.DescriptionMode.Append,
                "已有描述的处理方式：Append=追加 / FillMissing=仅缺失时生成 / Replace=用自动信息替换原描述。");

            ShowSourceMod = cfg.Bind(
                "General", "ShowSourceMod", true,
                "在信息块末尾显示来源 mod（GUID）。");

            ShowCustomData = cfg.Bind(
                "General", "ShowCustomData", false,
                "显示 CustomItemInfo.CustomData 中未被 effects/info/acquire/features 使用的键值（调试用）。");

            ShowRecipes = cfg.Bind(
                "General", "ShowRecipes", true,
                "显示“制作(INT)：材料=结果”行（来自 Recipes.recipes，含 mod 配方）。");

            StatusShowDescriptions = cfg.Bind(
                "General", "ShowStatusDescriptions", true,
                "状态数值行后附加状态影响说明（扫描 mod 的 MoodleRegistry.AddMoodle 调用）。");

            StatusShowMoodleInfo = cfg.Bind(
                "General", "ShowMoodleInfo", true,
                "在自定义 moodle 悬停框追加：当前数值 / 阈值 / 相关物品。");

            Language = cfg.Bind(
                "Language", "Mode", LanguageMode.Auto,
                "语言：Auto=跟随游戏语言 / ZhCN=简体中文 / EN=English。");

            CacheEnabled = cfg.Bind(
                "Cache", "Enabled", true,
                "启用 JSON 缓存：首次生成 InfoCache/{mod}/{id}.json，之后启动只校验变化，未变不重复生成。");

            CacheForceRebuild = cfg.Bind(
                "Cache", "ForceRebuild", false,
                "true = 每次启动强制重建全部缓存文件（忽略对比）。");

            CacheOverridesFile = cfg.Bind(
                "Cache", "OverridesFile", "overrides.json",
                "手工覆盖文件名（位于插件目录）。命中的条目优先显示且不写入自动缓存。");

            CreatureUseRuntimeValues = cfg.Bind(
                "Creature", "UseRuntimeValues", false,
                "false = 生物信息使用定义静态值（def.Health 等）；true = 缓存 {health} 占位符并在悬停时填当前血量。");

            UseVanillaLocaleBaseline = cfg.Bind(
                "Advanced", "UseVanillaLocaleBaseline", true,
                "用原版 Lang/EN.json 作为原版物品基线，用于识别非 CUCoreLib 的 mod 物品。");

            DebugLog = cfg.Bind(
                "Advanced", "DebugLog", false,
                "输出调试日志（生成/写缓存异常等）。");
        }
    }
}
