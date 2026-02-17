using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using eStarter.Core.Kernel.FileSystem;
using eStarter.Sdk.Ipc;

namespace eStarter.Core.Kernel
{
    /// <summary>
    /// Process state in the kernel.
    /// </summary>
    public sealed class ProcessInfo
    {
        public required string AppId { get; init; }
        public required int ProcessId { get; init; }
        public required string Version { get; init; }
        public PermissionSet Permissions { get; set; }
        public DateTime StartTime { get; init; } = DateTime.UtcNow;
        public ProcessState State { get; set; } = ProcessState.Running;
    }

    public enum ProcessState : byte
    {
        Starting = 0,
        Running = 1,
        Suspended = 2,
        Terminating = 3,
        Terminated = 4
    }

    /// <summary>
    /// API handler delegate.
    /// </summary>
    public delegate ValueTask<ApiResponse> ApiHandler(ProcessInfo caller, ApiRequest request);

    /// <summary>
    /// eStarter OS Kernel - central coordinator for all system services.
    /// Thread-safe singleton with O(1) permission checks and API routing.
    /// </summary>
    public sealed class Kernel : IDisposable
    {
        private static Kernel? _instance;
        private static readonly object _lock = new();

        public static Kernel Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new Kernel();
                    }
                }
                return _instance;
            }
        }

        // Process registry: AppId -> ProcessInfo
        private readonly ConcurrentDictionary<string, ProcessInfo> _processes = new();

        // API handlers: Command -> Handler
        private readonly ApiHandler?[] _handlers = new ApiHandler[0x0700];

        // System services
        private VirtualFileSystem? _fileSystem;
        private FileSystemApiHandler? _fsApiHandler;
        private PermissionManager? _permissionManager;
        private SystemSettingsManager? _systemSettings;

        // Events
        public event Action<ProcessInfo>? ProcessStarted;
        public event Action<ProcessInfo>? ProcessTerminated;
        public event Action<string, ApiCommand, ApiStatus>? ApiCalled;

        // Service accessors
        public VirtualFileSystem FileSystem => _fileSystem ?? throw new InvalidOperationException("FileSystem not initialized");
        public PermissionManager Permissions => _permissionManager ?? throw new InvalidOperationException("PermissionManager not initialized");
        public SystemSettingsManager SystemSettings => _systemSettings ?? throw new InvalidOperationException("SystemSettings not initialized");

        private Kernel()
        {
            RegisterBuiltInHandlers();
            InitializeServices();
        }

        private void InitializeServices()
        {
            // Initialize file system
            _fileSystem = new VirtualFileSystem(this);
            _fsApiHandler = new FileSystemApiHandler(this, _fileSystem);

            // Initialize permission manager
            _permissionManager = new PermissionManager(this);

            // Initialize system settings
            _systemSettings = new SystemSettingsManager(this);
        }

        #region Process Management

        /// <summary>
        /// Register a new process (called on handshake).
        /// </summary>
        public ProcessInfo RegisterProcess(string appId, int processId, string version, Permission requestedPermissions)
        {
            var process = new ProcessInfo
            {
                AppId = appId,
                ProcessId = processId,
                Version = version,
                Permissions = new PermissionSet(requestedPermissions & Permission.Full) // Filter out admin/kernel
            };

            if (_processes.TryAdd(appId, process))
            {
                ProcessStarted?.Invoke(process);
                Debug.WriteLine($"[Kernel] Process registered: {appId} (PID: {processId})");
            }
            else
            {
                // Update existing
                _processes[appId] = process;
            }

            return process;
        }

        /// <summary>
        /// Unregister a process.
        /// </summary>
        public void UnregisterProcess(string appId)
        {
            if (_processes.TryRemove(appId, out var process))
            {
                process.State = ProcessState.Terminated;
                ProcessTerminated?.Invoke(process);
                Debug.WriteLine($"[Kernel] Process unregistered: {appId}");
            }
        }

        /// <summary>
        /// Get process info by app ID.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ProcessInfo? GetProcess(string appId)
            => _processes.TryGetValue(appId, out var p) ? p : null;

        /// <summary>
        /// Get all running processes.
        /// </summary>
        public ProcessInfo[] GetAllProcesses()
            => [.. _processes.Values];

        #endregion

        #region Permission Management

        /// <summary>
        /// Check if a process has permission. O(1) operation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PermissionResult CheckPermission(string appId, Permission required)
        {
            if (!_processes.TryGetValue(appId, out var process))
                return PermissionResult.Fail(required);

            return process.Permissions.Has(required)
                ? PermissionResult.Success
                : PermissionResult.Fail(required & ~process.Permissions.Granted);
        }

        /// <summary>
        /// Grant additional permissions to a process.
        /// Respects global system policies — if a permission category is globally disabled,
        /// the grant is silently blocked.
        /// </summary>
        public bool GrantPermission(string appId, Permission permission)
        {
            if (!_processes.TryGetValue(appId, out var process))
                return false;

            // Cannot grant admin/kernel permissions
            permission &= Permission.Full;

            // Enforce global policies
            if (_systemSettings != null && !_systemSettings.IsGloballyAllowed(permission))
            {
                Debug.WriteLine($"[Kernel] GrantPermission blocked by system policy: {permission} for {appId}");
                return false;
            }

            process.Permissions = process.Permissions.Grant(permission);
            return true;
        }

        /// <summary>
        /// Revoke permissions from a process.
        /// </summary>
        public bool RevokePermission(string appId, Permission permission)
        {
            if (!_processes.TryGetValue(appId, out var process))
                return false;

            process.Permissions = process.Permissions.Revoke(permission);
            return true;
        }

        #endregion

        #region API Routing

        /// <summary>
        /// Register an API handler.
        /// </summary>
        public void RegisterHandler(ApiCommand command, ApiHandler handler)
        {
            _handlers[(int)command] = handler;
        }

        /// <summary>
        /// Process an API request with permission checking.
        /// </summary>
        public async ValueTask<ApiResponse> HandleApiAsync(string callerAppId, ApiRequest request)
        {
            // Get caller process
            var caller = GetProcess(callerAppId);
            if (caller == null)
            {
                return ApiResponse.Fail(request.RequestId, ApiStatus.PermissionDenied, "Process not registered");
            }

            // Check permission
            var required = ApiPermissionMap.GetRequired(request.Command);
            if (required != Permission.None)
            {
                var check = CheckPermission(callerAppId, required);
                if (!check.Allowed)
                {
                    ApiCalled?.Invoke(callerAppId, request.Command, ApiStatus.PermissionDenied);
                    return ApiResponse.PermissionDenied(request.RequestId, check.Missing);
                }
            }

            // Get handler
            var handler = _handlers[(int)request.Command];
            if (handler == null)
            {
                ApiCalled?.Invoke(callerAppId, request.Command, ApiStatus.NotSupported);
                return ApiResponse.Fail(request.RequestId, ApiStatus.NotSupported, $"Unknown command: {request.Command}");
            }

            try
            {
                var response = await handler(caller, request).ConfigureAwait(false);
                ApiCalled?.Invoke(callerAppId, request.Command, response.Status);
                return response;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Kernel] API error: {request.Command} - {ex.Message}");
                ApiCalled?.Invoke(callerAppId, request.Command, ApiStatus.Error);
                return ApiResponse.Fail(request.RequestId, ApiStatus.Error, ex.Message);
            }
        }

        /// <summary>
        /// Process an IPC message containing an API request.
        /// </summary>
        public async ValueTask<IpcMessage> HandleMessageAsync(IpcMessage message)
        {
            if (message.Type != IpcMessageType.ApiRequest)
            {
                return IpcMessage.CreateApiResponse(message.Id, "system",
                    IpcSerializer.Serialize(ApiResponse.Fail(0, ApiStatus.InvalidRequest, "Expected ApiRequest")));
            }

            var request = new ApiRequest
            {
                Command = (ApiCommand)message.Command,
                RequestId = message.Id,
                Data = string.IsNullOrEmpty(message.Payload) ? null : JsonDocument.Parse(message.Payload).RootElement
            };

            var response = await HandleApiAsync(message.SourceAppId, request).ConfigureAwait(false);

            return IpcMessage.CreateApiResponse(message.Id, "system", IpcSerializer.Serialize(response));
        }

        #endregion

        #region Built-in Handlers

        private void RegisterBuiltInHandlers()
        {
            RegisterHandler(ApiCommand.Ping, (_, req) =>
                new ValueTask<ApiResponse>(ApiResponse.Success(req.RequestId)));

            RegisterHandler(ApiCommand.GetTime, (_, req) =>
            {
                var time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var json = JsonSerializer.SerializeToElement(new { time });
                return new ValueTask<ApiResponse>(ApiResponse.Success(req.RequestId, json));
            });

            RegisterHandler(ApiCommand.GetSystemInfo, (_, req) =>
            {
                var info = new
                {
                    os = "eStarter",
                    version = "1.0.0",
                    processes = _processes.Count,
                    uptime = (DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds
                };
                var json = JsonSerializer.SerializeToElement(info);
                return new ValueTask<ApiResponse>(ApiResponse.Success(req.RequestId, json));
            });

            RegisterHandler(ApiCommand.GetProcessList, (_, req) =>
            {
                var list = new System.Collections.Generic.List<object>();
                foreach (var p in _processes.Values)
                {
                    list.Add(new { p.AppId, p.ProcessId, p.Version, state = p.State.ToString() });
                }
                var json = JsonSerializer.SerializeToElement(list);
                return new ValueTask<ApiResponse>(ApiResponse.Success(req.RequestId, json));
            });

            RegisterHandler(ApiCommand.CheckPermission, (caller, req) =>
            {
                if (req.Data?.TryGetProperty("permission", out var permEl) == true &&
                    Enum.TryParse<Permission>(permEl.GetString(), out var perm))
                {
                    var has = caller.Permissions.Has(perm);
                    var json = JsonSerializer.SerializeToElement(new { has, permission = perm.ToString() });
                    return new ValueTask<ApiResponse>(ApiResponse.Success(req.RequestId, json));
                }
                return new ValueTask<ApiResponse>(ApiResponse.Fail(req.RequestId, ApiStatus.InvalidRequest, "Missing permission parameter"));
            });

                        RegisterHandler(ApiCommand.GetPermissions, (caller, req) =>
                        {
                            var json = JsonSerializer.SerializeToElement(new
                            {
                                granted = caller.Permissions.Granted.ToString(),
                                denied = caller.Permissions.Denied.ToString()
                            });
                            return new ValueTask<ApiResponse>(ApiResponse.Success(req.RequestId, json));
                        });
                    }

                    #endregion

                    public void Dispose()
                    {
                        _fileSystem?.Dispose();
                        _processes.Clear();
                        Array.Clear(_handlers);
                    }
                }
            }
