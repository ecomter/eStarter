using System.Text.Json.Serialization;

namespace eStarter.Models
{
    public class AppSettings
    {
        [JsonPropertyName("theme")]
        public string Theme { get; set; } = "Dark"; // "Dark" or "Light"

        [JsonPropertyName("accentColor")]
        public string AccentColor { get; set; } = "#FF0078D7"; // Default Blue
    }
}
