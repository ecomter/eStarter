using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace eStarter.Sdk
{
    /// <summary>
    /// Permission API wrapper.
    /// </summary>
    public sealed class PermissionApi
    {
        private readonly EStarterClient _client;

        // API command codes
        private const ushort CmdRequestPermission = 0x0500;
        private const ushort CmdCheckPermission = 0x0501;
        private const ushort CmdGetPermissions = 0x0502;

        internal PermissionApi(EStarterClient client) => _client = client;

        /// <summary>
        /// Request a permission from the user.
        /// </summary>
        public async Task<bool> RequestAsync(string permission)
        {
            var result = await _client.CallApiAsync<PermissionResponse>(CmdRequestPermission, new { permission }).ConfigureAwait(false);
            return result.Success && result.Data?.Granted == true;
        }

        /// <summary>
        /// Check if a permission is granted.
        /// </summary>
        public async Task<bool> HasAsync(string permission)
        {
            var result = await _client.CallApiAsync<CheckResponse>(CmdCheckPermission, new { permission }).ConfigureAwait(false);
            return result.Success && result.Data?.Has == true;
        }

        /// <summary>
        /// Get all granted and denied permissions.
        /// </summary>
        public Task<ApiResult<PermissionInfo>> GetAllAsync()
            => _client.CallApiAsync<PermissionInfo>(CmdGetPermissions);

        #region Permission Names

        // File System
        public static class FileSystem
        {
            public const string Read = "FileRead";
            public const string Write = "FileWrite";
            public const string Delete = "FileDelete";
        }

        // Network
        public static class Network
        {
            public const string Access = "NetworkAccess";
            public const string Listen = "NetworkListen";
        }

        // UI
        public static class UI
        {
            public const string Notification = "Notification";
            public const string Clipboard = "Clipboard";
            public const string Dialog = "Dialog";
            public const string Overlay = "Overlay";
        }

        // System
        public static class System
        {
            public const string ProcessLaunch = "ProcessLaunch";
            public const string ProcessKill = "ProcessKill";
            public const string Settings = "SystemSettings";
            public const string Info = "SystemInfo";
        }

        // IPC
        public static class Ipc
        {
            public const string Send = "IpcSend";
            public const string Receive = "IpcReceive";
            public const string Broadcast = "IpcBroadcast";
        }

        // Hardware
        public static class Hardware
        {
            public const string Camera = "Camera";
            public const string Microphone = "Microphone";
            public const string Location = "Location";
        }

        #endregion

        #region Response DTOs

        private sealed class PermissionResponse
        {
            [JsonPropertyName("granted")]
            public bool Granted { get; set; }

            [JsonPropertyName("already")]
            public bool Already { get; set; }

            [JsonPropertyName("reason")]
            public string? Reason { get; set; }
        }

        private sealed class CheckResponse
        {
            [JsonPropertyName("has")]
            public bool Has { get; set; }

            [JsonPropertyName("permission")]
            public string? Permission { get; set; }
        }

        #endregion
    }

    /// <summary>
    /// Current permission state.
    /// </summary>
    public sealed class PermissionInfo
    {
        [JsonPropertyName("granted")]
        public string Granted { get; set; } = string.Empty;

        [JsonPropertyName("denied")]
        public string Denied { get; set; } = string.Empty;
    }
}
