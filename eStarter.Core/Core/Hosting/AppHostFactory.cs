using System;
using System.IO;
using eStarter.Models;

namespace eStarter.Core.Hosting
{
    /// <summary>
    /// Creates the correct <see cref="IAppHost"/> implementation based on
    /// the runtime declared in <see cref="AppManifest.Runtime"/>.
    /// </summary>
    public static class AppHostFactory
    {
        /// <summary>
        /// Create an <see cref="IAppHost"/> for <paramref name="manifest"/> using
        /// <paramref name="policy"/> as the resource-limit envelope.
        /// </summary>
        /// <param name="manifest">App manifest describing the app to host.</param>
        /// <param name="appDirectory">Absolute path to the app's installation directory.</param>
        /// <param name="kernel">Kernel instance that will handle API requests from the app.</param>
        /// <param name="policy">
        /// Sandbox policy.  Pass <see langword="null"/> to derive it from the manifest automatically.
        /// </param>
        /// <returns>A new, not-yet-started <see cref="IAppHost"/>.</returns>
        /// <exception cref="NotSupportedException">
        /// Thrown when the requested runtime is not yet supported on this platform.
        /// </exception>
        public static IAppHost Create(
            AppManifest manifest,
            string appDirectory,
            eStarter.Core.Kernel.Kernel kernel,
            SandboxPolicy? policy = null)
        {
            ArgumentNullException.ThrowIfNull(manifest);
            ArgumentNullException.ThrowIfNull(appDirectory);
            ArgumentNullException.ThrowIfNull(kernel);

            policy ??= manifest.SandboxPolicy;

            return manifest.Runtime switch
            {
                AppRuntime.Native => CreateProcessHost(manifest, appDirectory, kernel, policy),
                AppRuntime.Wasm   => CreateWasmHost(manifest, appDirectory, kernel, policy),
                AppRuntime.Dotnet => throw new NotSupportedException(
                    "The 'dotnet' runtime host is not yet implemented."),
                _ => throw new NotSupportedException(
                    $"Unknown runtime '{manifest.Runtime}'.")
            };
        }

        // ── Private helpers ───────────────────────────────────────────────

        private static IAppHost CreateProcessHost(
            AppManifest manifest,
            string appDirectory,
            eStarter.Core.Kernel.Kernel kernel,
            SandboxPolicy policy)
        {
            // Resolve the entry executable path.
            var entryRelative = manifest.Entry ?? manifest.ExePath
                ?? throw new InvalidOperationException(
                    $"Manifest for '{manifest.Id}' has no 'entry' or 'exePath'.");

            var exePath = Path.IsPathRooted(entryRelative)
                ? entryRelative
                : Path.Combine(appDirectory, entryRelative);

            return new ProcessHost(manifest.Id, exePath, appDirectory, kernel, policy,
                manifest.Arguments, manifest.DeclaredPermissions);
        }

        private static IAppHost CreateWasmHost(
            AppManifest manifest,
            string appDirectory,
            eStarter.Core.Kernel.Kernel kernel,
            SandboxPolicy policy)
        {
            var entryRelative = manifest.Entry
                ?? throw new InvalidOperationException(
                    $"Manifest for '{manifest.Id}' must specify 'entry' for the wasm runtime.");

            var wasmPath = Path.IsPathRooted(entryRelative)
                ? entryRelative
                : Path.Combine(appDirectory, entryRelative);

            return new WasmAppHost(manifest.Id, wasmPath, kernel, policy,
                manifest.DeclaredPermissions);
        }
    }
}
