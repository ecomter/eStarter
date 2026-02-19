using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace eStarter.Core.Kernel
{
    /// <summary>
    /// Manages persistent permission grants and user authorization.
    /// </summary>
    public sealed class PermissionManager
    {
        private readonly ConcurrentDictionary<string, PermissionGrant> _grants = new();
        private readonly string _storagePath;
        private readonly Kernel _kernel;

        public event Action<string, Permission, bool>? PermissionRequested;

        public PermissionManager(Kernel kernel, string? storagePath = null)
        {
            _kernel = kernel;
            _storagePath = storagePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "eStarter", "permissions.json");

            LoadGrants();
            RegisterApiHandlers();
        }

        /// <summary>
        /// Get stored grants for an app.
        /// </summary>
        public PermissionSet GetStoredPermissions(string appId)
        {
            return _grants.TryGetValue(appId, out var grant)
                ? new PermissionSet(grant.Granted, grant.Denied)
                : PermissionSet.Default;
        }

        /// <summary>
        /// Store permission grant (persisted to disk).
        /// </summary>
        public void StoreGrant(string appId, Permission granted, Permission denied = Permission.None)
        {
            var grant = new PermissionGrant
            {
                AppId = appId,
                Granted = granted,
                Denied = denied,
                UpdatedAt = DateTime.UtcNow
            };

            _grants[appId] = grant;
            _ = SaveGrantsAsync();
        }

        /// <summary>
        /// Request permission from user (raises event for UI to handle).
        /// </summary>
        public Task<bool> RequestUserAuthorizationAsync(string appId, Permission permission)
        {
            var tcs = new TaskCompletionSource<bool>();

            // UI should subscribe to this event and call CompleteRequest,
            // which fires PermissionRequested again with allowed=true/false.
            // We only listen for the completion callback (allowed != initial false).
            void Handler(string id, Permission perm, bool allowed)
            {
                // Ignore the initial request broadcast (allowed=false).
                // Only respond to the completion callback from CompleteRequest.
                if (id == appId && perm == permission && allowed)
                {
                    PermissionRequested -= Handler;
                    tcs.TrySetResult(true);
                }
            }

            // Handler for denial — CompleteRequest fires with allowed=false too,
            // but we need a separate path since the initial broadcast is also allowed=false.
            // Solution: use a flag to skip the initial invocation.
            bool initialRaised = false;
            void FullHandler(string id, Permission perm, bool allowed)
            {
                if (id != appId || perm != permission)
                    return;

                if (!initialRaised)
                {
                    // Skip the initial broadcast we fire below.
                    initialRaised = true;
                    return;
                }

                // This is the completion callback from CompleteRequest.
                PermissionRequested -= FullHandler;
                tcs.TrySetResult(allowed);
            }

            PermissionRequested += FullHandler;

            // Raise event for UI to show the permission dialog.
            PermissionRequested?.Invoke(appId, permission, false);

            // Timeout after 30 seconds
            Task.Delay(30000).ContinueWith(_ =>
            {
                PermissionRequested -= FullHandler;
                tcs.TrySetResult(false);
            });

            return tcs.Task;
        }

        /// <summary>
        /// Complete a permission request (called by UI).
        /// </summary>
        public void CompleteRequest(string appId, Permission permission, bool allowed)
        {
            if (allowed)
            {
                _kernel.GrantPermission(appId, permission);

                // Update stored grants
                if (_grants.TryGetValue(appId, out var existing))
                {
                    StoreGrant(appId, existing.Granted | permission, existing.Denied & ~permission);
                }
                else
                {
                    StoreGrant(appId, Permission.Basic | permission);
                }
            }
            else
            {
                // Add to denied list
                if (_grants.TryGetValue(appId, out var existing))
                {
                    StoreGrant(appId, existing.Granted, existing.Denied | permission);
                }
            }

            PermissionRequested?.Invoke(appId, permission, allowed);
        }

        /// <summary>
        /// Reset all permissions for an app.
        /// </summary>
        public void ResetPermissions(string appId)
        {
            _grants.TryRemove(appId, out _);
            _ = SaveGrantsAsync();
        }

        private void RegisterApiHandlers()
        {
            _kernel.RegisterHandler(ApiCommand.RequestPermission, async (caller, req) =>
            {
                if (req.Data?.TryGetProperty("permission", out var permEl) != true ||
                    !Enum.TryParse<Permission>(permEl.GetString(), out var perm))
                {
                    return ApiResponse.Fail(req.RequestId, ApiStatus.InvalidRequest, "Missing permission parameter");
                }

                // Check if already granted
                if (caller.Permissions.Has(perm))
                {
                    return ApiResponse.Success(req.RequestId, 
                        JsonSerializer.SerializeToElement(new { granted = true, already = true }));
                }

                // Check if previously denied by user
                if (_grants.TryGetValue(caller.AppId, out var stored) && 
                    (stored.Denied & perm) == perm)
                {
                    return ApiResponse.Success(req.RequestId,
                        JsonSerializer.SerializeToElement(new { granted = false, reason = "Previously denied" }));
                }

                // Request user authorization
                var allowed = await RequestUserAuthorizationAsync(caller.AppId, perm);

                return ApiResponse.Success(req.RequestId,
                    JsonSerializer.SerializeToElement(new { granted = allowed }));
            });
        }

        private void LoadGrants()
        {
            try
            {
                if (File.Exists(_storagePath))
                {
                    var json = File.ReadAllText(_storagePath);
                    var grants = JsonSerializer.Deserialize<PermissionGrant[]>(json);
                    if (grants != null)
                    {
                        foreach (var g in grants)
                        {
                            _grants[g.AppId] = g;
                        }
                    }
                }
            }
            catch
            {
                // Ignore load errors
            }
        }

        private async Task SaveGrantsAsync()
        {
            try
            {
                var dir = Path.GetDirectoryName(_storagePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(_grants.Values,
                    new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_storagePath, json);
            }
            catch
            {
                // Ignore save errors
            }
        }

        private sealed class PermissionGrant
        {
            public string AppId { get; set; } = string.Empty;
            public Permission Granted { get; set; }
            public Permission Denied { get; set; }
            public DateTime UpdatedAt { get; set; }
        }
    }
}
