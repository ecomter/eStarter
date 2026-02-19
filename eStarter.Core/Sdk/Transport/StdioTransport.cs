using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StreamJsonRpc;

namespace eStarter.Sdk.Transport
{
    /// <summary>
    /// JSON-RPC transport over stdin/stdout for child apps hosted by <c>ProcessHost</c>.
    /// The host listens on the child's stdout and sends on stdin, so from the child's
    /// perspective: we <b>send</b> on stdout and <b>receive</b> on stdin.
    /// </summary>
    internal sealed class StdioTransport : IAsyncDisposable
    {
        private readonly JsonRpc _rpc;
        private bool _disposed;

        /// <summary>Raised when the JSON-RPC connection drops.</summary>
        public event Action? Disconnected;

        public bool IsConnected => !_disposed && !_rpc.IsDisposed;

        public StdioTransport()
        {
            var stdIn = Console.OpenStandardInput();
            var stdOut = Console.OpenStandardOutput();

            var formatter = new SystemTextJsonFormatter();
            var handler = new HeaderDelimitedMessageHandler(stdOut, stdIn, formatter);
            _rpc = new JsonRpc(handler);
            _rpc.Disconnected += OnDisconnected;
        }

        /// <summary>Start listening for incoming messages.</summary>
        public void Start() => _rpc.StartListening();

        /// <summary>
        /// Invoke an RPC method on the host.
        /// Maps to <c>ProcessHost.HandleApiCallAsync</c> which is registered
        /// under the method name <c>api_call</c>.
        /// </summary>
        public async Task<ApiCallResult> CallApiAsync(
            string command,
            JsonElement? data,
            int timeoutMs = 30_000,
            CancellationToken ct = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);

            try
            {
                var result = await _rpc.InvokeWithCancellationAsync<JsonElement>(
                    "api_call",
                    new object?[] { command, data },
                    cts.Token).ConfigureAwait(false);

                return new ApiCallResult(true, result);
            }
            catch (OperationCanceledException)
            {
                return ApiCallResult.TimedOut;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StdioTransport] api_call failed: {ex.GetType().Name}: {ex.Message}");
                return ApiCallResult.Failed(ex.Message);
            }
        }

        /// <summary>
        /// Send a log message to the host (fire-and-forget).
        /// </summary>
        public void Log(string level, string message)
        {
            if (_disposed) return;
            try
            {
                _rpc.NotifyAsync("log", level, message).ConfigureAwait(false);
            }
            catch { /* best-effort */ }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            _rpc.Disconnected -= OnDisconnected;
            _rpc.Dispose();
            await Task.CompletedTask;
        }

        private void OnDisconnected(object? sender, JsonRpcDisconnectedEventArgs e)
        {
            Debug.WriteLine($"[StdioTransport] Disconnected: {e.Reason}");
            Disconnected?.Invoke();
        }
    }

    /// <summary>Lightweight result from a stdio api_call invocation.</summary>
    internal readonly struct ApiCallResult
    {
        public readonly bool Success;
        public readonly JsonElement? Data;
        public readonly string? Error;

        public ApiCallResult(bool success, JsonElement? data, string? error = null)
        {
            Success = success;
            Data = data;
            Error = error;
        }

        public static ApiCallResult TimedOut => new(false, null, "Timeout");
        public static ApiCallResult Failed(string error) => new(false, null, error);
    }
}
