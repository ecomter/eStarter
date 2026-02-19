using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Wasmtime;

namespace eStarter.Core.Hosting
{
    /// <summary>
    /// Hosts a WebAssembly module under Wasmtime with full memory isolation.
    /// Exports host functions that the guest calls to invoke Kernel APIs.
    /// Thread-safe; one instance per app.
    /// </summary>
    public sealed class WasmAppHost : IAppHost
    {
        private readonly string _wasmPath;
        private readonly eStarter.Core.Kernel.Kernel _kernel;
        private readonly SandboxPolicy _policy;
        private readonly Kernel.Permission _permissions;

        private Engine? _engine;
        private Module? _module;
        private Linker? _linker;
        private Store? _store;
        private Instance? _instance;
        private Task? _runTask;
        private CancellationTokenSource? _lifetimeCts;
        private int _disposed;

        public string AppId { get; }
        public AppHostState State { get; private set; }
        public event EventHandler<AppHostExitedEventArgs>? Exited;
        /// <summary>Fired on every <c>estarter_log</c> call from the WASM guest.</summary>
        public event Action<string>? LogReceived;
        /// <summary>Fired whenever <see cref="State"/> changes.</summary>
        public event Action<AppHostState>? StateChanged;

        public WasmAppHost(
            string appId,
            string wasmPath,
            eStarter.Core.Kernel.Kernel kernel,
            SandboxPolicy policy,
            Kernel.Permission permissions = Kernel.Permission.Basic)
        {
            ArgumentException.ThrowIfNullOrEmpty(appId);
            ArgumentException.ThrowIfNullOrEmpty(wasmPath);

            AppId = appId;
            _wasmPath = wasmPath;
            _kernel = kernel;
            _policy = policy;
            _permissions = permissions;
            State = AppHostState.Created;
        }

        // ── IAppHost ──────────────────────────────────────────────────────

        public Task StartAsync(CancellationToken ct = default)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            if (State != AppHostState.Created)
                throw new InvalidOperationException($"Cannot start from state {State}.");

            State = AppHostState.Starting;
            StateChanged?.Invoke(State);
            _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            try
            {
                InitialiseWasmRuntime();

                // Register process in kernel (PID = 0 for WASM — no OS process).
                _kernel.RegisterProcess(AppId, 0, "1.0", _permissions);

                State = AppHostState.Running;
                StateChanged?.Invoke(State);

                // Run _start on a background thread so StartAsync returns promptly.
                _runTask = Task.Run(() => RunModule(_lifetimeCts.Token), _lifetimeCts.Token);

                // Enforce max-runtime if configured.
                if (_policy.MaxRuntime > TimeSpan.Zero)
                {
                    _ = EnforceMaxRuntimeAsync(_policy.MaxRuntime, _lifetimeCts.Token);
                }

                return Task.CompletedTask;
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

            if (_runTask != null)
            {
                try
                {
                    await _runTask.WaitAsync(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                }
                catch { /* timeout or cancelled — acceptable */ }
            }

            CleanUp();
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            await StopAsync().ConfigureAwait(false);
        }

        // ── WASM runtime setup ────────────────────────────────────────────

        private void InitialiseWasmRuntime()
        {
            _engine = new Engine();
            _module = Module.FromFile(_engine, _wasmPath);
            _linker = new Linker(_engine);
            _store = new Store(_engine);

            // Apply memory limits from SandboxPolicy.
            if (_policy.MemoryLimitBytes > 0)
            {
                _store.SetLimits(
                    memorySize: _policy.MemoryLimitBytes,
                    tableElements: 10_000,
                    instances: 1,
                    tables: 10,
                    memories: 1);
            }

            // Define WASI defaults so simple modules can run.
            _linker.DefineWasi();
            _store.SetWasiConfiguration(
                new WasiConfiguration()
                    .WithInheritedStandardError());

            // Export host functions for Kernel API access.
            DefineHostFunctions();

            _instance = _linker.Instantiate(_store, _module);
        }

        private void DefineHostFunctions()
        {
            // estarter_log(ptr, len) — guest writes a UTF-8 log message.
            _linker.DefineFunction("env", "estarter_log",
                (Caller caller, int ptr, int len) =>
                {
                    var memory = caller.GetMemory("memory");
                    if (memory == null) return;
                    var span = memory.GetSpan<byte>((long)ptr, len);
                    var msg = Encoding.UTF8.GetString(span);
                    Debug.WriteLine($"[{AppId}/wasm] {msg}");
                    LogReceived?.Invoke(msg);
                });

            // estarter_api_call(cmdPtr, cmdLen, dataPtr, dataLen) -> i32 status
            // Synchronous from the guest's perspective.
            _linker.DefineFunction("env", "estarter_api_call",
                (Caller caller, int cmdPtr, int cmdLen, int dataPtr, int dataLen) =>
                {
                    var memory = caller.GetMemory("memory");
                    if (memory == null) return (int)Kernel.ApiStatus.Error;

                    var cmdSpan = memory.GetSpan<byte>((long)cmdPtr, cmdLen);
                    var cmdName = Encoding.UTF8.GetString(cmdSpan);

                    if (!Enum.TryParse<Kernel.ApiCommand>(cmdName, ignoreCase: true, out var cmd))
                        return (int)Kernel.ApiStatus.InvalidRequest;

                    JsonElement? data = null;
                    if (dataLen > 0)
                    {
                        var dataSpan = memory.GetSpan<byte>((long)dataPtr, dataLen);
                        var dataJson = Encoding.UTF8.GetString(dataSpan);
                        data = JsonSerializer.Deserialize<JsonElement>(dataJson);
                    }

                    var request = new Kernel.ApiRequest
                    {
                        Command = cmd,
                        RequestId = Kernel.ApiRequest.NextId(),
                        Data = data
                    };

                    // Block on the async kernel call (WASM guest is single-threaded).
                    var response = _kernel.HandleApiAsync(AppId, request)
                        .AsTask().GetAwaiter().GetResult();

                    return (int)response.Status;
                });
        }

        // ── Module execution ──────────────────────────────────────────────

        private void RunModule(CancellationToken ct)
        {
            int exitCode = 0;
            Exception? fault = null;

            try
            {
                // Invoke the WASI _start entry point.
                var start = _instance?.GetAction("_start");
                if (start != null)
                {
                    start();
                }
                else
                {
                    Debug.WriteLine($"[WasmAppHost] {AppId}: no _start export found.");
                    exitCode = -1;
                }
            }
            catch (OperationCanceledException) { }
            catch (WasmtimeException ex)
            {
                Debug.WriteLine($"[WasmAppHost] {AppId} trapped: {ex.Message}");
                exitCode = 1;
                fault = ex;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WasmAppHost] {AppId} error: {ex.Message}");
                exitCode = 1;
                fault = ex;
            }
            finally
            {
                CleanUp();
                RaiseExited(exitCode, fault);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private void RaiseExited(int exitCode, Exception? ex = null)
        {
            State = ex == null ? AppHostState.Stopped : AppHostState.Faulted;
            StateChanged?.Invoke(State);
            Exited?.Invoke(this, new AppHostExitedEventArgs(AppId, exitCode, ex));
        }

        private void CleanUp()
        {
            _lifetimeCts?.Cancel();
            _kernel.UnregisterProcess(AppId);

            _instance = null;
            _store?.Dispose();
            _store = null;
            _linker?.Dispose();
            _linker = null;
            _module?.Dispose();
            _module = null;
            _engine?.Dispose();
            _engine = null;

            if (State is not (AppHostState.Stopped or AppHostState.Faulted))
                State = AppHostState.Stopped;
        }

        private async Task EnforceMaxRuntimeAsync(TimeSpan max, CancellationToken ct)
        {
            try
            {
                await Task.Delay(max, ct).ConfigureAwait(false);
                Debug.WriteLine($"[WasmAppHost] {AppId} exceeded max runtime ({max}). Stopping.");
                // Fuel-based cancellation is preferred; for now cancel the token.
                _lifetimeCts?.Cancel();
            }
            catch (OperationCanceledException) { }
        }
    }
}
