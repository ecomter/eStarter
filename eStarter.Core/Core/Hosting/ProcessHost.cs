using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using eStarter.Core.Kernel;
using StreamJsonRpc;

namespace eStarter.Core.Hosting
{
    /// <summary>
    /// Hosts a native process with stdio JSON-RPC bridge.
    /// All app API calls are forwarded to <see cref="Kernel.HandleApiAsync"/>.
    /// Thread-safe; designed for one concurrent launch per <see cref="AppId"/>.
    /// </summary>
    public sealed class ProcessHost : IAppHost
    {
        private readonly string _exePath;
        private readonly string _workingDirectory;
        private readonly eStarter.Core.Kernel.Kernel _kernel;
        private readonly SandboxPolicy _policy;
        private readonly string? _arguments;
        private readonly Kernel.Permission _permissions;

        private Process? _process;
        private JsonRpc? _rpc;
        private IDisposable? _osResourceLimit;
        private CancellationTokenSource? _lifetimeCts;
        private int _disposed;
        private int _cleanedUp;

        public string AppId { get; }
        public AppHostState State { get; private set; }
        public event EventHandler<AppHostExitedEventArgs>? Exited;

        /// <summary>The OS process ID, or -1 when not running.</summary>
        public int ProcessId => _process is { HasExited: false } p ? p.Id : -1;

        public ProcessHost(
            string appId,
            string exePath,
            string workingDirectory,
            eStarter.Core.Kernel.Kernel kernel,
            SandboxPolicy policy,
            string? arguments = null,
            Kernel.Permission permissions = Kernel.Permission.Basic)
        {
            ArgumentException.ThrowIfNullOrEmpty(appId);
            ArgumentException.ThrowIfNullOrEmpty(exePath);

            AppId = appId;
            _exePath = exePath;
            _workingDirectory = workingDirectory;
            _kernel = kernel;
            _policy = policy;
            _arguments = arguments;
            _permissions = permissions;
            State = AppHostState.Created;
        }

        // ── IAppHost ──────────────────────────────────────────────────────

        public async Task StartAsync(CancellationToken ct = default)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            if (State != AppHostState.Created)
                throw new InvalidOperationException($"Cannot start from state {State}.");

            State = AppHostState.Starting;
            try
            {
                _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

                var psi = BuildProcessStartInfo();
                _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _process.Exited += OnProcessExited;
                _process.ErrorDataReceived += OnErrorDataReceived;

                if (!_process.Start())
                    throw new InvalidOperationException($"Failed to start process '{_exePath}'.");

                _process.BeginErrorReadLine();

                // Apply OS-level resource limits (Job Object / cgroup) — best-effort.
                _osResourceLimit = OsResourceLimitFactory.TryCreate(_policy, _process.Id);

                // Register process in kernel with manifest-declared permissions.
                _kernel.RegisterProcess(AppId, _process.Id, "1.0", _permissions);

                // Attach JSON-RPC on stdin/stdout.
                AttachJsonRpc(_process.StandardInput.BaseStream,
                              _process.StandardOutput.BaseStream);

                // Enforce max-runtime if configured.
                if (_policy.MaxRuntime > TimeSpan.Zero)
                {
                    _ = EnforceMaxRuntimeAsync(_policy.MaxRuntime, _lifetimeCts.Token);
                }

                State = AppHostState.Running;
            }
            catch (Exception ex)
            {
                State = AppHostState.Faulted;
                RaiseExited(-1, ex);
                throw;
            }
        }

        public async Task StopAsync(CancellationToken ct = default)
        {
            if (State is AppHostState.Stopped or AppHostState.Faulted)
                return;

            State = AppHostState.Stopping;
            _lifetimeCts?.Cancel();

            try
            {
                if (_process is { HasExited: false } proc)
                {
                    // Ask nicely first.
                    if (!proc.CloseMainWindow())
                    {
                        proc.Kill(entireProcessTree: true);
                    }

                    using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    waitCts.CancelAfter(TimeSpan.FromSeconds(5));
                    try
                    {
                        await proc.WaitForExitAsync(waitCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        try { proc.Kill(entireProcessTree: true); } catch { }
                    }
                }
            }
            catch
            {
                // Process may have already exited.
            }
            finally
            {
                CleanUp();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            await StopAsync().ConfigureAwait(false);
        }

        // ── JSON-RPC target (called by the child process) ─────────────────

        /// <summary>
        /// RPC target: the child sends API calls here.
        /// Method name exposed over JSON-RPC: <c>api_call</c>.
        /// </summary>
        [JsonRpcMethod("api_call")]
        public async Task<JsonElement> HandleApiCallAsync(string command, JsonElement? data)
        {
            if (!Enum.TryParse<ApiCommand>(command, ignoreCase: true, out var cmd))
            {
                var fail = ApiResponse.Fail(0, ApiStatus.InvalidRequest,
                    $"Unknown command: {command}");
                return JsonSerializer.SerializeToElement(fail);
            }

            var request = new ApiRequest
            {
                Command = cmd,
                RequestId = ApiRequest.NextId(),
                Data = data
            };

            var response = await _kernel.HandleApiAsync(AppId, request)
                .ConfigureAwait(false);

            return JsonSerializer.SerializeToElement(response);
        }

        /// <summary>
        /// RPC target: the child can send log messages.
        /// Method name exposed over JSON-RPC: <c>log</c>.
        /// </summary>
        [JsonRpcMethod("log")]
        public void HandleLog(string level, string message)
        {
            Debug.WriteLine($"[{AppId}/{level}] {message}");
        }

        // ── Private helpers ───────────────────────────────────────────────

        // Environment variables that must be preserved for .NET child processes.
        private static readonly string[] s_preserveEnvVars =
        {
            "PATH", "PATHEXT", "SYSTEMROOT", "WINDIR",
            "TEMP", "TMP", "USERPROFILE", "HOME", "HOMEDRIVE", "HOMEPATH",
            "DOTNET_ROOT", "DOTNET_ROOT(x86)", "DOTNET_HOST_PATH",
            "PROGRAMFILES", "PROGRAMFILES(X86)", "PROGRAMDATA",
            "APPDATA", "LOCALAPPDATA", "COMPUTERNAME", "USERNAME",
            "PROCESSOR_ARCHITECTURE", "OS", "NUMBER_OF_PROCESSORS"
        };

        private ProcessStartInfo BuildProcessStartInfo()
        {
            var psi = new ProcessStartInfo(_exePath)
            {
                WorkingDirectory = _workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            // Keep only the environment variables the .NET runtime needs.
            psi.Environment.Clear();
            foreach (var name in s_preserveEnvVars)
            {
                if (Environment.GetEnvironmentVariable(name) is { } val)
                    psi.Environment[name] = val;
            }

            // eStarter markers.
            psi.Environment["ESTARTER_MODE"] = "hosted";
            psi.Environment["ESTARTER_APP_ID"] = AppId;

            if (!string.IsNullOrEmpty(_arguments))
                psi.Arguments = _arguments;

            return psi;
        }

        private void AttachJsonRpc(Stream input, Stream output)
        {
            // input  = process stdin  (we write TO the child)
            // output = process stdout (we read FROM the child)
            var formatter = new SystemTextJsonFormatter();
            var handler = new HeaderDelimitedMessageHandler(input, output, formatter);
            _rpc = new JsonRpc(handler, this);
            _rpc.Disconnected += OnRpcDisconnected;
            _rpc.StartListening();
        }

        private void OnProcessExited(object? sender, EventArgs e)
        {
            var exitCode = _process?.ExitCode ?? -1;
            Debug.WriteLine($"[ProcessHost] {AppId} exited with code {exitCode}.");
            CleanUp();
            RaiseExited(exitCode);
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
                Debug.WriteLine($"[{AppId}] {e.Data}");
        }

        private void OnRpcDisconnected(object? sender, JsonRpcDisconnectedEventArgs e)
        {
            Debug.WriteLine($"[ProcessHost] JSON-RPC disconnected for {AppId}: {e.Reason}");
        }

        private void RaiseExited(int exitCode, Exception? ex = null)
        {
            State = ex == null ? AppHostState.Stopped : AppHostState.Faulted;
            Exited?.Invoke(this, new AppHostExitedEventArgs(AppId, exitCode, ex));
        }

        private void CleanUp()
        {
            // Guard against concurrent calls from OnProcessExited + StopAsync.
            if (Interlocked.Exchange(ref _cleanedUp, 1) != 0)
                return;

            try { _lifetimeCts?.Cancel(); } catch { }

            var rpc = Interlocked.Exchange(ref _rpc, null);
            if (rpc != null)
            {
                rpc.Disconnected -= OnRpcDisconnected;
                try { rpc.Dispose(); } catch { }
            }

            var limit = Interlocked.Exchange(ref _osResourceLimit, null);
            limit?.Dispose();

            var proc = Interlocked.Exchange(ref _process, null);
            if (proc != null)
            {
                proc.Exited -= OnProcessExited;
                proc.ErrorDataReceived -= OnErrorDataReceived;
                _kernel.UnregisterProcess(AppId);
                try { proc.Kill(entireProcessTree: true); } catch { }
                proc.Dispose();
            }

            if (State is not (AppHostState.Stopped or AppHostState.Faulted))
                State = AppHostState.Stopped;
        }

        private async Task EnforceMaxRuntimeAsync(TimeSpan max, CancellationToken ct)
        {
            try
            {
                await Task.Delay(max, ct).ConfigureAwait(false);
                Debug.WriteLine($"[ProcessHost] {AppId} exceeded max runtime ({max}). Stopping.");
                await StopAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
        }
    }
}
