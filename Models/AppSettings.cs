using System.Text.Json.Serialization;

namespace eStarter.Models
{
    public class AppSettings
    {
        [JsonPropertyName("theme")]
        public string Theme { get; set; } = "Dark"; // "Dark" or "Light"

        [JsonPropertyName("accentColor")]
        public string AccentColor { get; set; } = "#FF0078D7"; // Default Blue

        [JsonPropertyName("showTime")]
        public bool ShowTime { get; set; } = true;

        [JsonPropertyName("timeZoneId")]
        public string TimeZoneId { get; set; } = System.TimeZoneInfo.Local.Id;

        [JsonPropertyName("language")]
        public string Language { get; set; } = "en-US"; // "en-US" or "zh-CN"
    }
}
