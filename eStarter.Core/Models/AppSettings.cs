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

        [JsonPropertyName("showNotifications")]
        public bool ShowNotifications { get; set; } = true;

        [JsonPropertyName("enableAnimations")]
        public bool EnableAnimations { get; set; } = true;

        [JsonPropertyName("userPicturePath")]
        public string UserPicturePath { get; set; } = string.Empty;

        // Notification settings
        [JsonPropertyName("showLockScreenNotifications")]
        public bool ShowLockScreenNotifications { get; set; } = true;

        [JsonPropertyName("playNotificationSounds")]
        public bool PlayNotificationSounds { get; set; } = true;

        [JsonPropertyName("quietHoursEnabled")]
        public bool QuietHoursEnabled { get; set; } = false;

        // Privacy settings
        [JsonPropertyName("allowLocation")]
        public bool AllowLocation { get; set; } = false;

        [JsonPropertyName("allowCamera")]
        public bool AllowCamera { get; set; } = true;

        [JsonPropertyName("allowMicrophone")]
        public bool AllowMicrophone { get; set; } = true;

        [JsonPropertyName("allowFileSystem")]
        public bool AllowFileSystem { get; set; } = true;

        [JsonPropertyName("allowNetwork")]
        public bool AllowNetwork { get; set; } = true;

        [JsonPropertyName("allowIpc")]
        public bool AllowIpc { get; set; } = true;

        // Time settings
        [JsonPropertyName("autoSetTime")]
        public bool AutoSetTime { get; set; } = true;

        [JsonPropertyName("autoSetTimeZone")]
        public bool AutoSetTimeZone { get; set; } = false;
    }
}
