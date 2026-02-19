using System.Text.Json.Serialization;
using eStarter.Core.Hosting;
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

        // ── Runtime / hosting ──────────────────────────────────────────────

        /// <summary>
        /// Runtime type used to launch this app.
        /// Defaults to <see cref="AppRuntime.Native"/> for backwards compatibility.
        /// </summary>
        [JsonPropertyName("runtime")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AppRuntime Runtime { get; set; } = AppRuntime.Native;

        /// <summary>
        /// Path to the app entry point, relative to the app directory.
        /// For native: path to the .exe.  For wasm: path to the .wasm file.
        /// Falls back to <see cref="ExePath"/> when omitted.
        /// </summary>
        [JsonPropertyName("entry")]
        public string? Entry { get; set; }

        // ── Sandbox resource limits (optional) ────────────────────────────

        /// <summary>Memory limit in MB. 0 = unlimited.</summary>
        [JsonPropertyName("memoryLimitMb")]
        public int MemoryLimitMb { get; set; } = 0;

        /// <summary>Maximum number of child processes. 0 = unlimited.</summary>
        [JsonPropertyName("maxProcesses")]
        public int MaxProcesses { get; set; } = 0;

        /// <summary>CPU quota as a percentage (1–100). 0 = unlimited.</summary>
        [JsonPropertyName("cpuQuota")]
        public int CpuQuota { get; set; } = 0;

        /// <summary>Whether outbound network access is permitted.</summary>
        [JsonPropertyName("networkAllowed")]
        public bool NetworkAllowed { get; set; } = false;

        /// <summary>Maximum wall-clock lifetime in seconds. 0 = unlimited.</summary>
        [JsonPropertyName("maxRuntimeSeconds")]
        public int MaxRuntimeSeconds { get; set; } = 0;

        /// <summary>
        /// Build a <see cref="SandboxPolicy"/> from this manifest.
        /// </summary>
        [JsonIgnore]
        public SandboxPolicy SandboxPolicy => SandboxPolicy.FromManifest(this);

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
