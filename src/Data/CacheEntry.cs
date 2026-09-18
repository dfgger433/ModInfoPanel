using System.Collections.Generic;
using Newtonsoft.Json;

namespace ModInfoPanel.Data
{
    internal sealed class CacheFileDto
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonProperty("pluginVersion")]
        public string PluginVersion { get; set; }

        [JsonProperty("language")]
        public string Language { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("sourceMod")]
        public string SourceMod { get; set; }

        [JsonProperty("generatedAt")]
        public string GeneratedAt { get; set; }

        [JsonProperty("runtimeHealth", NullValueHandling = NullValueHandling.Ignore)]
        public bool? RuntimeHealth { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }
    }

    internal sealed class OverridesDto
    {
        [JsonProperty("items")]
        public Dictionary<string, string> Items { get; set; } = new Dictionary<string, string>();

        [JsonProperty("liquidTiles")]
        public Dictionary<string, string> LiquidTiles { get; set; } = new Dictionary<string, string>();

        [JsonProperty("creatures")]
        public Dictionary<string, string> Creatures { get; set; } = new Dictionary<string, string>();

        /// <summary>value = 液体块文本（旧版 {name, details} 对象也会被转换为文本）。</summary>
        [JsonProperty("liquids")]
        public Dictionary<string, string> Liquids { get; set; } = new Dictionary<string, string>();
    }
}
