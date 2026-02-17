using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace eStarter.Sdk
{
    /// <summary>
    /// Helper utilities for eStarter SDK apps.
    /// </summary>
    public static class SdkHelpers
    {
        /// <summary>
        /// Create and connect a client using manifest.json in app directory.
        /// </summary>
        public static async Task<EStarterClient?> CreateFromManifestAsync(string? manifestPath = null)
        {
            try
            {
                manifestPath ??= Path.Combine(AppContext.BaseDirectory, "manifest.json");
                
                if (!File.Exists(manifestPath))
                    return null;

                var json = await File.ReadAllTextAsync(manifestPath).ConfigureAwait(false);
                var manifest = JsonSerializer.Deserialize<AppManifest>(json);
                
                if (manifest == null || string.IsNullOrEmpty(manifest.Id))
                    return null;

                var client = new EStarterClient(manifest.Id, manifest.Version ?? "1.0.0");
                if (await client.ConnectAsync().ConfigureAwait(false))
                {
                    return client;
                }

                client.Dispose();
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if running inside eStarter environment.
        /// </summary>
        public static bool IsRunningInEStarter()
        {
            // Check for environment variable set by eStarter
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ESTARTER_APP_ID"));
        }

        /// <summary>
        /// Get app ID from environment.
        /// </summary>
        public static string? GetAppIdFromEnvironment()
            => Environment.GetEnvironmentVariable("ESTARTER_APP_ID");

        /// <summary>
        /// Get app version from assembly.
        /// </summary>
        public static string GetAppVersion()
        {
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
        }
    }

    /// <summary>
    /// App manifest for SDK apps.
    /// </summary>
    public sealed class AppManifest
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Publisher { get; set; }
        public string? Version { get; set; }
        public string? Category { get; set; }
        public string[]? Permissions { get; set; }
        public int MinApiVersion { get; set; } = 1;
        public bool Sandboxed { get; set; } = true;
    }

    /// <summary>
    /// Base class for eStarter SDK applications.
    /// Provides automatic connection management.
    /// </summary>
    public abstract class EStarterApp : IAsyncDisposable
    {
        protected EStarterClient Client { get; private set; } = null!;
        protected bool IsConnected => Client?.IsConnected == true;

        private readonly string _appId;
        private readonly string _version;

        protected EStarterApp(string appId, string version = "1.0.0")
        {
            _appId = appId;
            _version = version;
        }

        /// <summary>
        /// Initialize and connect to eStarter.
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            Client = new EStarterClient(_appId, _version);
            Client.Disconnected += OnDisconnected;

            if (!await Client.ConnectAsync().ConfigureAwait(false))
            {
                return false;
            }

            await OnConnectedAsync().ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// Called when connected to eStarter.
        /// </summary>
        protected virtual Task OnConnectedAsync() => Task.CompletedTask;

        /// <summary>
        /// Called when disconnected from eStarter.
        /// </summary>
        protected virtual void OnDisconnected() { }

        public async ValueTask DisposeAsync()
        {
            if (Client != null)
            {
                Client.Disconnected -= OnDisconnected;
                await Client.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
