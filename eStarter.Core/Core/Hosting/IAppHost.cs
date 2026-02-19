using System;
using System.Threading;
using System.Threading.Tasks;

namespace eStarter.Core.Hosting
{
    /// <summary>
    /// Runtime type that determines which host implementation is used.
    /// </summary>
    public enum AppRuntime
    {
        /// <summary>Run as a native process (default, backwards-compatible).</summary>
        Native,
        /// <summary>Run as a WebAssembly module under Wasmtime.</summary>
        Wasm,
        /// <summary>Run as a managed .NET assembly inside an AssemblyLoadContext.</summary>
        Dotnet
    }

    /// <summary>
    /// Lifecycle state of an <see cref="IAppHost"/>.
    /// </summary>
    public enum AppHostState
    {
        Created = 0,
        Starting,
        Running,
        Stopping,
        Stopped,
        Faulted
    }

    /// <summary>
    /// Event arguments raised when an app host exits.
    /// </summary>
    public sealed class AppHostExitedEventArgs : EventArgs
    {
        public string AppId { get; }
        public int ExitCode { get; }
        public Exception? Exception { get; }

        public AppHostExitedEventArgs(string appId, int exitCode, Exception? exception = null)
        {
            AppId = appId;
            ExitCode = exitCode;
            Exception = exception;
        }
    }

    /// <summary>
    /// Unified interface for all app runtime hosts.
    /// Implementations: <c>ProcessHost</c> (native), <c>WasmAppHost</c> (wasm).
    /// </summary>
    public interface IAppHost : IAsyncDisposable
    {
        /// <summary>Identifier of the hosted app.</summary>
        string AppId { get; }

        /// <summary>Current lifecycle state.</summary>
        AppHostState State { get; }

        /// <summary>Raised when the hosted app has exited (normally or due to error).</summary>
        event EventHandler<AppHostExitedEventArgs>? Exited;

        /// <summary>
        /// Start the app. Transitions state from <see cref="AppHostState.Created"/> to
        /// <see cref="AppHostState.Running"/>.
        /// </summary>
        Task StartAsync(CancellationToken ct = default);

        /// <summary>
        /// Request a graceful shutdown. Transitions to <see cref="AppHostState.Stopped"/>.
        /// </summary>
        Task StopAsync(CancellationToken ct = default);
    }
}
