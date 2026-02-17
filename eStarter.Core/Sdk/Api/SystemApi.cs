using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace eStarter.Sdk
{
    /// <summary>
    /// System API wrapper.
    /// </summary>
    public sealed class SystemApi
    {
        private readonly EStarterClient _client;

        // API command codes
        private const ushort CmdPing = 0x0001;
        private const ushort CmdGetSystemInfo = 0x0002;
        private const ushort CmdGetTime = 0x0004;
        private const ushort CmdLaunch = 0x0100;
        private const ushort CmdGetProcessList = 0x0102;
        private const ushort CmdGetProcessInfo = 0x0103;

        internal SystemApi(EStarterClient client) => _client = client;

        /// <summary>
        /// Ping the system (test connection).
        /// </summary>
        public async Task<bool> PingAsync()
        {
            var result = await _client.CallApiAsync(CmdPing, null, 5000).ConfigureAwait(false);
            return result.Success;
        }

        /// <summary>
        /// Get system information.
        /// </summary>
        public Task<ApiResult<SystemInfo>> GetInfoAsync()
            => _client.CallApiAsync<SystemInfo>(CmdGetSystemInfo);

        /// <summary>
        /// Get current system time (UTC milliseconds).
        /// </summary>
        public async Task<ApiResult<long>> GetTimeAsync()
        {
            var result = await _client.CallApiAsync<TimeResponse>(CmdGetTime).ConfigureAwait(false);
            if (!result.Success)
                return ApiResult<long>.Fail(result.Status, result.Error ?? "Failed");

            return ApiResult<long>.Ok(result.Data?.Time ?? 0);
        }

        /// <summary>
        /// Launch another app.
        /// </summary>
        public Task<ApiResult> LaunchAppAsync(string targetAppId, string arguments = "")
            => _client.CallApiAsync(CmdLaunch, new { targetAppId, arguments });

        /// <summary>
        /// Get list of running processes.
        /// </summary>
        public Task<ApiResult<ProcessInfo[]>> GetProcessListAsync()
            => _client.CallApiAsync<ProcessInfo[]>(CmdGetProcessList);

        #region Response DTOs

        private sealed class TimeResponse
        {
            [JsonPropertyName("time")]
            public long Time { get; set; }
        }

        #endregion
    }

    /// <summary>
    /// System information.
    /// </summary>
    public sealed class SystemInfo
    {
        [JsonPropertyName("os")]
        public string Os { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("processes")]
        public int ProcessCount { get; set; }

        [JsonPropertyName("uptime")]
        public double UptimeSeconds { get; set; }
    }

    /// <summary>
    /// Process information.
    /// </summary>
    public sealed class ProcessInfo
    {
        [JsonPropertyName("appId")]
        public string AppId { get; set; } = string.Empty;

        [JsonPropertyName("processId")]
        public int ProcessId { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;
    }
}
