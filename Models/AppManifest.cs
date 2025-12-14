using System.Text.Json.Serialization;

namespace eStarter.Models
{
    /// <summary>
    /// Represents the metadata defined in an app's manifest.json file.
    /// </summary>
    public class AppManifest
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("publisher")]
        public string? Publisher { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("exePath")]
        public string? ExePath { get; set; }

        [JsonPropertyName("arguments")]
        public string? Arguments { get; set; }

        [JsonPropertyName("background")]
        public string? Background { get; set; }

        [JsonPropertyName("tileSize")]
        public string? TileSize { get; set; } // "Small", "Medium", "Wide", "Large"
    }
}
