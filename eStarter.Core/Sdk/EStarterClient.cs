using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using eStarter.Sdk.Transport;
using IpcTypes = eStarter.Sdk.Ipc;

namespace eStarter.Sdk
{
    /// <summary>
    /// eStarter SDK client for child applications.
    /// Auto-detects <c>ESTARTER_MODE=hosted</c> to use stdio JSON-RPC transport;
    /// otherwise falls back to named-pipe IPC.
    /// </summary>
    public sealed class EStarterClient : IAsyncDisposable, IDisposable
    {
        private const string PipeName = "eStarterBus";
        private const int DefaultTimeoutMs = 30000;

        private readonly string _appId;
        private readonly string _version;

        // Named-pipe transport (legacy)
        private NamedPipeClientStream? _pipe;
        private readonly ConcurrentDictionary<uint, TaskCompletionSource<IpcTypes.IpcMessage>> _pendingRequests = new();
        private CancellationTokenSource? _cts;
        private Task? _receiveTask;

        // Stdio JSON-RPC transport (hosted mode)
        private StdioTransport? _stdioTransport;

        private bool _disposed;
        private int _connected;

        /// <summary>Whether this client runs inside a <c>ProcessHost</c> (stdio JSON-RPC mode).</summary>
        public bool IsHostedMode { get; }

        // Sub-API accessors
        public FileSystemApi FileSystem { get; }
        public PermissionApi Permissions { get; }
        public SystemApi System { get; }
        public IpcApi Ipc { get; }
        public EventManager Events { get; }

        /// <summary>
        /// Raised when connection is lost.
        /// </summary>
        public event Action? Disconnected;

        /// <summary>
        /// Raised when a system event is received.
        /// </summary>
        public event Action<string, JsonElement?>? EventReceived;

        public bool IsConnected => _connected == 1 &&
            (IsHostedMode ? _stdioTransport?.IsConnected == true : _pipe?.IsConnected == true);
        public string AppId => _appId;

        public EStarterClient(string appId, string version = "1.0.0")
        {
            _appId = appId ?? throw new ArgumentNullException(nameof(appId));
            _version = version;
            IsHostedMode = string.Equals(
                Environment.GetEnvironmentVariable("ESTARTER_MODE"),
                "hosted", StringComparison.OrdinalIgnoreCase);

            FileSystem = new FileSystemApi(this);
            Permissions = new PermissionApi(this);
            System = new SystemApi(this);
            Ipc = new IpcApi(this);
            Events = new EventManager(this);
        }

        #region Connection

        /// <summary>
        /// Connect to the eStarter system bus.
        /// In hosted mode, initialises the stdio JSON-RPC transport immediately.
        /// In pipe mode, connects to the named pipe and performs handshake.
        /// </summary>
        public async Task<bool> ConnectAsync(CancellationToken ct = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(EStarterClient));
            if (IsConnected) return true;

            if (IsHostedMode)
                return ConnectStdio();

            return await ConnectPipeAsync(ct).ConfigureAwait(false);
        }

        private bool ConnectStdio()
        {
            try
            {
                _stdioTransport = new StdioTransport();
                _stdioTransport.Disconnected += () =>
                {
                    Interlocked.Exchange(ref _connected, 0);
                    Disconnected?.Invoke();
                };
                _stdioTransport.Start();
                Interlocked.Exchange(ref _connected, 1);
                Debug.WriteLine("[SDK] Connected via stdio JSON-RPC (hosted mode).");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SDK] Stdio connect failed: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ConnectPipeAsync(CancellationToken ct)
        {
            try
            {
                _pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await _pipe.ConnectAsync(5000, ct).ConfigureAwait(false);

                _cts = new CancellationTokenSource();
                _receiveTask = ReceiveLoopAsync(_cts.Token);

                // Send handshake
                var handshake = new IpcTypes.IpcMessage
                {
                    Type = IpcTypes.IpcMessageType.Handshake,
                    SourceAppId = _appId,
                    Payload = IpcTypes.IpcSerializer.Serialize(new IpcTypes.HandshakePayload
                    {
                        AppId = _appId,
                        Version = _version,
                        ProcessId = Environment.ProcessId
                    })
                };

                await SendAsync(handshake, ct).ConfigureAwait(false);
                Interlocked.Exchange(ref _connected, 1);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SDK] Pipe connect failed: {ex.Message}");
                await DisconnectAsync().ConfigureAwait(false);
                return false;
            }
        }

        /// <summary>
        /// Disconnect from the system bus.
        /// </summary>
        public async Task DisconnectAsync()
        {
            Interlocked.Exchange(ref _connected, 0);

            if (_stdioTransport != null)
            {
                await _stdioTransport.DisposeAsync().ConfigureAwait(false);
                _stdioTransport = null;
            }

            _cts?.Cancel();

            if (_receiveTask != null)
            {
                try { await _receiveTask.ConfigureAwait(false); } catch { }
            }

            _pipe?.Dispose();
            _pipe = null;
            _cts?.Dispose();
            _cts = null;
        }

        #endregion

        #region API Calls

        /// <summary>
        /// Send an API request and wait for response.
        /// Routes through stdio JSON-RPC in hosted mode, named pipe otherwise.
        /// </summary>
        internal async Task<ApiResult<T>> CallApiAsync<T>(ushort command, object? data = null, int timeoutMs = DefaultTimeoutMs)
        {
            if (!IsConnected)
                return ApiResult<T>.Fail(ApiStatus.Error, "Not connected");

            if (IsHostedMode)
                return await CallApiViaStdioAsync<T>(command, data, timeoutMs).ConfigureAwait(false);

            return await CallApiViaPipeAsync<T>(command, data, timeoutMs).ConfigureAwait(false);
        }

        private async Task<ApiResult<T>> CallApiViaStdioAsync<T>(ushort command, object? data, int timeoutMs)
        {
            if (_stdioTransport == null)
                return ApiResult<T>.Fail(ApiStatus.Error, "Stdio transport not initialised");

            // Resolve enum name from the ushort command code.
            var cmdName = Enum.IsDefined(typeof(Core.Kernel.ApiCommand), command)
                ? ((Core.Kernel.ApiCommand)command).ToString()
                : command.ToString();

            JsonElement? jsonData = data != null
                ? JsonSerializer.SerializeToElement(data)
                : null;

            var result = await _stdioTransport.CallApiAsync(cmdName, jsonData, timeoutMs)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                var status = result.Error == "Timeout" ? ApiStatus.Timeout : ApiStatus.Error;
                return ApiResult<T>.Fail(status, result.Error ?? "RPC failed");
            }

            // The host returns a serialised ApiResponse inside result.Data.
            return ParseStdioResponse<T>(result.Data);
        }

        private static ApiResult<T> ParseStdioResponse<T>(JsonElement? element)
        {
            if (element == null)
                return ApiResult<T>.Fail(ApiStatus.Error, "Empty response");

            try
            {
                var el = element.Value;

                byte status = 0;
                if (el.TryGetProperty("status", out var sProp))
                    status = sProp.GetByte();

                if (status != 0)
                {
                    var error = el.TryGetProperty("error", out var eProp) ? eProp.GetString() : "Unknown error";
                    return ApiResult<T>.Fail((ApiStatus)status, error ?? "Unknown error");
                }

                if (el.TryGetProperty("data", out var dProp) && dProp.ValueKind != JsonValueKind.Null)
                {
                    var value = JsonSerializer.Deserialize<T>(dProp.GetRawText());
                    return ApiResult<T>.Ok(value!);
                }

                return ApiResult<T>.Ok(default!);
            }
            catch (Exception ex)
            {
                return ApiResult<T>.Fail(ApiStatus.Error, ex.Message);
            }
        }

        private async Task<ApiResult<T>> CallApiViaPipeAsync<T>(ushort command, object? data, int timeoutMs)
        {
            var payload = data != null ? IpcTypes.IpcSerializer.Serialize(data) : string.Empty;
            var message = IpcTypes.IpcMessage.CreateApiRequest(_appId, command, payload);

            var tcs = new TaskCompletionSource<IpcTypes.IpcMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingRequests[message.Id] = tcs;

            try
            {
                await SendAsync(message, default).ConfigureAwait(false);

                using var cts = new CancellationTokenSource(timeoutMs);
                cts.Token.Register(() => tcs.TrySetCanceled());

                var response = await tcs.Task.ConfigureAwait(false);
                return ParseResponse<T>(response);
            }
            catch (OperationCanceledException)
            {
                return ApiResult<T>.Fail(ApiStatus.Timeout, "Request timeout");
            }
            catch (Exception ex)
            {
                return ApiResult<T>.Fail(ApiStatus.Error, ex.Message);
            }
            finally
            {
                _pendingRequests.TryRemove(message.Id, out _);
            }
        }

        /// <summary>
        /// Send an API request without waiting for response data.
        /// </summary>
        internal async Task<ApiResult> CallApiAsync(ushort command, object? data = null, int timeoutMs = DefaultTimeoutMs)
        {
            var result = await CallApiAsync<JsonElement>(command, data, timeoutMs).ConfigureAwait(false);
            return new ApiResult(result.Success, result.Status, result.Error);
        }

        private static ApiResult<T> ParseResponse<T>(IpcTypes.IpcMessage response)
        {
            if (string.IsNullOrEmpty(response.Payload))
                return ApiResult<T>.Fail(ApiStatus.Error, "Empty response");

            try
            {
                var apiResponse = IpcTypes.IpcSerializer.Deserialize<ApiResponseDto>(response.Payload);
                if (apiResponse == null)
                    return ApiResult<T>.Fail(ApiStatus.Error, "Invalid response format");

                if (apiResponse.Status != 0)
                    return ApiResult<T>.Fail((ApiStatus)apiResponse.Status, apiResponse.Error ?? "Unknown error");

                if (apiResponse.Data.HasValue)
                {
                    var data = JsonSerializer.Deserialize<T>(apiResponse.Data.Value.GetRawText());
                    return ApiResult<T>.Ok(data!);
                }

                return ApiResult<T>.Ok(default!);
            }
            catch (Exception ex)
            {
                return ApiResult<T>.Fail(ApiStatus.Error, ex.Message);
            }
        }

        #endregion

        #region Send/Receive

        private async Task SendAsync(IpcTypes.IpcMessage message, CancellationToken ct)
        {
            if (_pipe == null || !_pipe.IsConnected)
                throw new InvalidOperationException("Not connected");

            await IpcTypes.PipeStreamHelper.WriteMessageAsync(_pipe, message).ConfigureAwait(false);
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && _pipe?.IsConnected == true)
                {
                    var message = await IpcTypes.PipeStreamHelper.ReadMessageAsync(_pipe).ConfigureAwait(false);
                    if (message == null) break;

                    HandleMessage(message);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SDK] Receive error: {ex.Message}");
            }
            finally
            {
                if (_connected == 1)
                {
                    Interlocked.Exchange(ref _connected, 0);
                    Disconnected?.Invoke();
                }
            }
        }

        private void HandleMessage(IpcTypes.IpcMessage message)
        {
            switch (message.Type)
            {
                case IpcTypes.IpcMessageType.ApiResponse:
                    if (_pendingRequests.TryRemove(message.Id, out var tcs))
                    {
                        tcs.TrySetResult(message);
                    }
                    break;

                case IpcTypes.IpcMessageType.Event:
                    try
                    {
                        var eventData = JsonSerializer.Deserialize<EventDto>(message.Payload);
                        EventReceived?.Invoke(eventData?.Name ?? "unknown", eventData?.Data);
                    }
                    catch { }
                    break;

                case IpcTypes.IpcMessageType.Pong:
                    if (_pendingRequests.TryRemove(message.Id, out var pingTcs))
                    {
                        pingTcs.TrySetResult(message);
                    }
                    break;
            }
        }

        #endregion

        #region Dispose

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            await DisconnectAsync().ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DisconnectAsync().GetAwaiter().GetResult();
        }

        #endregion

        #region DTOs

        private sealed class ApiResponseDto
        {
            public uint RequestId { get; set; }
            public byte Status { get; set; }
            public JsonElement? Data { get; set; }
            public string? Error { get; set; }
        }

        private sealed class EventDto
        {
            public string? Name { get; set; }
            public JsonElement? Data { get; set; }
        }

        #endregion
    }

    #region API Result Types

    /// <summary>
    /// API response status.
    /// </summary>
    public enum ApiStatus : byte
    {
        Success = 0,
        Error = 1,
        PermissionDenied = 2,
        NotFound = 3,
        InvalidRequest = 4,
        Timeout = 5,
        Busy = 6,
        NotSupported = 7
    }

    /// <summary>
    /// Result without data.
    /// </summary>
    public readonly struct ApiResult
    {
        public readonly bool Success;
        public readonly ApiStatus Status;
        public readonly string? Error;

        public ApiResult(bool success, ApiStatus status, string? error)
        {
            Success = success;
            Status = status;
            Error = error;
        }

        public static ApiResult Ok() => new(true, ApiStatus.Success, null);
        public static ApiResult Fail(ApiStatus status, string error) => new(false, status, error);
    }

    /// <summary>
    /// Result with typed data.
    /// </summary>
    public readonly struct ApiResult<T>
    {
        public readonly bool Success;
        public readonly ApiStatus Status;
        public readonly T? Data;
        public readonly string? Error;

        private ApiResult(bool success, ApiStatus status, T? data, string? error)
        {
            Success = success;
            Status = status;
            Data = data;
            Error = error;
        }

        public static ApiResult<T> Ok(T data) => new(true, ApiStatus.Success, data, null);
        public static ApiResult<T> Fail(ApiStatus status, string error) => new(false, status, default, error);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetValueOrDefault(T defaultValue) => Success ? Data ?? defaultValue : defaultValue;
    }

    #endregion
}
