using System.Text.Json.Serialization;
using eStarter.Core.Kernel;

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

        /// <summary>
        /// Permissions requested by the app. Array of permission names.
        /// Example: ["FileRead", "FileWrite", "Notification"]
        /// </summary>
        [JsonPropertyName("permissions")]
        public string[]? Permissions { get; set; }

        /// <summary>
        /// Minimum API version required.
        /// </summary>
        [JsonPropertyName("minApiVersion")]
        public int MinApiVersion { get; set; } = 1;

        /// <summary>
        /// Whether the app runs in sandbox mode (restricted permissions).
        /// </summary>
        [JsonPropertyName("sandboxed")]
        public bool Sandboxed { get; set; } = true;

        /// <summary>
        /// Parse declared permissions to Permission flags.
        /// </summary>
        [JsonIgnore]
        public Permission DeclaredPermissions
        {
            get
            {
                if (Permissions == null || Permissions.Length == 0)
                    return Permission.Basic;

                Permission result = Permission.None;
                foreach (var p in Permissions)
                {
                    if (System.Enum.TryParse<Permission>(p, true, out var perm))
                    {
                        result |= perm;
                    }
                }
                return result;
            }
        }
    }
}
