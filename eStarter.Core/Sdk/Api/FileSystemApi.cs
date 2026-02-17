using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace eStarter.Sdk
{
    /// <summary>
    /// File system API wrapper.
    /// </summary>
    public sealed class FileSystemApi
    {
        private readonly EStarterClient _client;

        // API command codes
        private const ushort CmdReadFile = 0x0200;
        private const ushort CmdWriteFile = 0x0201;
        private const ushort CmdDeleteFile = 0x0202;
        private const ushort CmdListDirectory = 0x0203;
        private const ushort CmdCreateDirectory = 0x0204;
        private const ushort CmdFileExists = 0x0205;
        private const ushort CmdGetFileInfo = 0x0206;

        internal FileSystemApi(EStarterClient client) => _client = client;

        /// <summary>
        /// Read file contents as bytes.
        /// </summary>
        public async Task<ApiResult<byte[]>> ReadBytesAsync(string path)
        {
            var result = await _client.CallApiAsync<ReadFileResponse>(CmdReadFile, new { path }).ConfigureAwait(false);
            if (!result.Success)
                return ApiResult<byte[]>.Fail(result.Status, result.Error ?? "Read failed");

            var bytes = System.Convert.FromBase64String(result.Data?.Data ?? string.Empty);
            return ApiResult<byte[]>.Ok(bytes);
        }

        /// <summary>
        /// Read file contents as text (UTF-8).
        /// </summary>
        public async Task<ApiResult<string>> ReadTextAsync(string path)
        {
            var bytesResult = await ReadBytesAsync(path).ConfigureAwait(false);
            if (!bytesResult.Success)
                return ApiResult<string>.Fail(bytesResult.Status, bytesResult.Error ?? "Read failed");

            var text = System.Text.Encoding.UTF8.GetString(bytesResult.Data ?? []);
            return ApiResult<string>.Ok(text);
        }

        /// <summary>
        /// Write bytes to file.
        /// </summary>
        public async Task<ApiResult<long>> WriteBytesAsync(string path, byte[] data)
        {
            var base64 = System.Convert.ToBase64String(data);
            var result = await _client.CallApiAsync<WriteFileResponse>(CmdWriteFile, new { path, data = base64 }).ConfigureAwait(false);
            
            if (!result.Success)
                return ApiResult<long>.Fail(result.Status, result.Error ?? "Write failed");

            return ApiResult<long>.Ok(result.Data?.Written ?? 0);
        }

        /// <summary>
        /// Write text to file (UTF-8).
        /// </summary>
        public async Task<ApiResult<long>> WriteTextAsync(string path, string text)
        {
            var result = await _client.CallApiAsync<WriteFileResponse>(CmdWriteFile, new { path, text }).ConfigureAwait(false);
            
            if (!result.Success)
                return ApiResult<long>.Fail(result.Status, result.Error ?? "Write failed");

            return ApiResult<long>.Ok(result.Data?.Written ?? 0);
        }

        /// <summary>
        /// Delete a file.
        /// </summary>
        public Task<ApiResult> DeleteAsync(string path)
            => _client.CallApiAsync(CmdDeleteFile, new { path });

        /// <summary>
        /// Check if file exists.
        /// </summary>
        public async Task<bool> ExistsAsync(string path)
        {
            var result = await _client.CallApiAsync<ExistsResponse>(CmdFileExists, new { path }).ConfigureAwait(false);
            return result.Success && result.Data?.Exists == true;
        }

        /// <summary>
        /// Get file information.
        /// </summary>
        public Task<ApiResult<FileInfo>> GetInfoAsync(string path)
            => _client.CallApiAsync<FileInfo>(CmdGetFileInfo, new { path });

        /// <summary>
        /// List directory contents.
        /// </summary>
        public async Task<ApiResult<FileInfo[]>> ListDirectoryAsync(string path)
        {
            var result = await _client.CallApiAsync<ListDirectoryResponse>(CmdListDirectory, new { path }).ConfigureAwait(false);
            
            if (!result.Success)
                return ApiResult<FileInfo[]>.Fail(result.Status, result.Error ?? "List failed");

            return ApiResult<FileInfo[]>.Ok(result.Data?.Items ?? []);
        }

        /// <summary>
        /// Create a directory.
        /// </summary>
        public Task<ApiResult> CreateDirectoryAsync(string path)
            => _client.CallApiAsync(CmdCreateDirectory, new { path });

        #region Helper methods

        /// <summary>
        /// Get path to app's data directory.
        /// </summary>
        public static string AppDataPath(string appId, string relativePath = "")
            => $"/appdata/{appId}/{relativePath}".TrimEnd('/');

        /// <summary>
        /// Get path to app's cache directory.
        /// </summary>
        public static string CachePath(string appId, string relativePath = "")
            => $"/cache/{appId}/{relativePath}".TrimEnd('/');

        /// <summary>
        /// Get path to app's temp directory.
        /// </summary>
        public static string TempPath(string appId, string relativePath = "")
            => $"/temp/{appId}/{relativePath}".TrimEnd('/');

        /// <summary>
        /// Get path to shared storage.
        /// </summary>
        public static string SharedPath(string appId, string relativePath = "")
            => $"/shared/{appId}/{relativePath}".TrimEnd('/');

        #endregion

        #region Response DTOs

        private sealed class ReadFileResponse
        {
            [JsonPropertyName("path")]
            public string? Path { get; set; }
            [JsonPropertyName("size")]
            public long Size { get; set; }
            [JsonPropertyName("data")]
            public string? Data { get; set; }
        }

        private sealed class WriteFileResponse
        {
            [JsonPropertyName("path")]
            public string? Path { get; set; }
            [JsonPropertyName("written")]
            public long Written { get; set; }
        }

        private sealed class ExistsResponse
        {
            [JsonPropertyName("exists")]
            public bool Exists { get; set; }
        }

        private sealed class ListDirectoryResponse
        {
            [JsonPropertyName("path")]
            public string? Path { get; set; }
            [JsonPropertyName("items")]
            public FileInfo[]? Items { get; set; }
        }

        #endregion
    }

    /// <summary>
    /// File information.
    /// </summary>
    public sealed class FileInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("isDir")]
        public bool IsDirectory { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("modified")]
        public long ModifiedTicks { get; set; }

        public System.DateTime ModifiedTime => new(ModifiedTicks, System.DateTimeKind.Utc);
    }
}
