using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace eStarter.Core.Kernel.FileSystem
{
    /// <summary>
    /// Shared data store for inter-app communication.
    /// Provides key-value storage accessible by apps with IpcSend/IpcReceive permissions.
    /// </summary>
    public sealed class SharedStorage
    {
        private readonly ConcurrentDictionary<string, SharedEntry> _store = new();
        private readonly VirtualFileSystem _fs;
        private readonly ReaderWriterLockSlim _lock = new();

        public SharedStorage(VirtualFileSystem fs)
        {
            _fs = fs;
        }

        /// <summary>
        /// Set a shared value. Other apps can read if they have permission.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(string appId, string key, string value, TimeSpan? expiry = null)
        {
            var fullKey = $"{appId}:{key}";
            var entry = new SharedEntry
            {
                Owner = appId,
                Key = key,
                Value = value,
                Created = DateTime.UtcNow,
                Expires = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : null
            };

            _store[fullKey] = entry;
        }

        /// <summary>
        /// Get a shared value from any app.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? Get(string ownerAppId, string key)
        {
            var fullKey = $"{ownerAppId}:{key}";
            if (_store.TryGetValue(fullKey, out var entry))
            {
                if (entry.Expires.HasValue && DateTime.UtcNow > entry.Expires.Value)
                {
                    _store.TryRemove(fullKey, out _);
                    return null;
                }
                return entry.Value;
            }
            return null;
        }

        /// <summary>
        /// Get own value (app can always read its own shared data).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? GetOwn(string appId, string key) => Get(appId, key);

        /// <summary>
        /// Delete a shared value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Delete(string appId, string key)
        {
            var fullKey = $"{appId}:{key}";
            return _store.TryRemove(fullKey, out _);
        }

        /// <summary>
        /// List all keys owned by an app.
        /// </summary>
        public string[] ListKeys(string appId)
        {
            var prefix = $"{appId}:";
            var keys = new System.Collections.Generic.List<string>();

            foreach (var kvp in _store)
            {
                if (kvp.Key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    keys.Add(kvp.Value.Key);
                }
            }

            return [.. keys];
        }

        /// <summary>
        /// Clear all data owned by an app.
        /// </summary>
        public int ClearApp(string appId)
        {
            var prefix = $"{appId}:";
            int count = 0;

            foreach (var key in _store.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    if (_store.TryRemove(key, out _))
                        count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Persist shared storage to file system.
        /// </summary>
        public async ValueTask<FileResult> PersistAsync(CancellationToken ct = default)
        {
            var vpath = VirtualPath.Create(PathZone.System, "_shared", "store.json");
            var json = JsonSerializer.Serialize(_store.Values);
            return await _fs.WriteFileAsync(vpath, "_system", System.Text.Encoding.UTF8.GetBytes(json), ct)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Load shared storage from file system.
        /// </summary>
        public async ValueTask LoadAsync(CancellationToken ct = default)
        {
            var vpath = VirtualPath.Create(PathZone.System, "_shared", "store.json");
            var (result, content) = await _fs.ReadTextAsync(vpath, "_system", ct).ConfigureAwait(false);

            if (result.Success && !string.IsNullOrEmpty(content))
            {
                try
                {
                    var entries = JsonSerializer.Deserialize<SharedEntry[]>(content);
                    if (entries != null)
                    {
                        foreach (var entry in entries)
                        {
                            // Skip expired entries
                            if (entry.Expires.HasValue && DateTime.UtcNow > entry.Expires.Value)
                                continue;

                            var fullKey = $"{entry.Owner}:{entry.Key}";
                            _store[fullKey] = entry;
                        }
                    }
                }
                catch
                {
                    // Ignore deserialization errors
                }
            }
        }

        private sealed class SharedEntry
        {
            public string Owner { get; set; } = string.Empty;
            public string Key { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
            public DateTime Created { get; set; }
            public DateTime? Expires { get; set; }
        }
    }
}
