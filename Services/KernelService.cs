using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using eStarter.Core;
using eStarter.Core.Kernel;
using eStarter.Core.Kernel.FileSystem;

namespace eStarter.Services
{
    /// <summary>
    /// Bridges the Kernel with the WPF UI layer.
    /// Provides thread-safe access to kernel services from UI.
    /// </summary>
    public sealed class KernelService : IDisposable
    {
        private static KernelService? _instance;
        private static readonly object _lock = new();

        public static KernelService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new KernelService();
                    }
                }
                return _instance;
            }
        }

        private readonly Kernel _kernel;
        private readonly SystemBus _systemBus;
        private readonly Dispatcher _dispatcher;
        private NotificationService? _notificationService;
        private bool _disposed;

        // Events for UI binding
        public event Action<ProcessInfo>? ProcessStarted;
        public event Action<ProcessInfo>? ProcessTerminated;
        public event Action<string, Permission>? PermissionRequested;

        // Service accessors
        public Kernel Kernel => _kernel;
        public SystemBus SystemBus => _systemBus;
        public VirtualFileSystem FileSystem => _kernel.FileSystem;
        public PermissionManager Permissions => _kernel.Permissions;
        public SystemSettingsManager SystemSettings => _kernel.SystemSettings;
        public NotificationService Notifications => _notificationService ?? throw new InvalidOperationException("Not initialized");

        public bool IsRunning { get; private set; }

        private KernelService()
        {
            _kernel = Kernel.Instance;
            _systemBus = new SystemBus();
            _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

            // Wire up events with UI thread dispatch
            _kernel.ProcessStarted += p => DispatchToUI(() => ProcessStarted?.Invoke(p));
            _kernel.ProcessTerminated += p => DispatchToUI(() => ProcessTerminated?.Invoke(p));
            _kernel.Permissions.PermissionRequested += OnPermissionRequested;

            // Wire SystemBus to Kernel
            _systemBus.MessageReceived += async (s, msg) =>
            {
                if (msg.Type == Sdk.Ipc.IpcMessageType.ApiRequest)
                {
                    var response = await _kernel.HandleMessageAsync(msg);
                    await _systemBus.SendToAppAsync(msg.SourceAppId, response);
                }
            };

            _systemBus.AppConnected += (s, appId) =>
            {
                // Auto-register process when app connects via handshake
                // This is handled in SystemBus already
            };
        }

        /// <summary>
        /// Initialize and start all kernel services.
        /// </summary>
        public void Start()
        {
            if (IsRunning) return;

            _systemBus.Start();
            _notificationService = new NotificationService(this);
            
            IsRunning = true;
            System.Diagnostics.Debug.WriteLine("[KernelService] Started");
        }

        /// <summary>
        /// Stop all kernel services.
        /// </summary>
        public void Stop()
        {
            if (!IsRunning) return;

            _systemBus.Stop();
            IsRunning = false;
            System.Diagnostics.Debug.WriteLine("[KernelService] Stopped");
        }

        /// <summary>
        /// Launch an app by ID.
        /// </summary>
        public async Task<bool> LaunchAppAsync(string appId, string? arguments = null)
        {
            var appManager = new AppManager(new AppInstaller());
            var entries = appManager.GetInstalledAppEntries();
            
            var app = System.Linq.Enumerable.FirstOrDefault(entries, e => e.Id == appId);
            if (app == null) return false;

            return await LaunchAppAsync(app, arguments);
        }

        /// <summary>
        /// Launch an app entry.
        /// </summary>
        public async Task<bool> LaunchAppAsync(Models.AppEntry app, string? arguments = null)
        {
            try
            {
                var appDir = System.IO.Path.Combine(
                    new AppManager(new AppInstaller()).AppsDirectory, 
                    app.Id);

                var exePath = string.IsNullOrEmpty(app.ExePath)
                    ? System.IO.Path.Combine(appDir, $"{app.Id}.exe")
                    : System.IO.Path.Combine(appDir, app.ExePath);

                if (!System.IO.File.Exists(exePath))
                {
                    await Notifications.ShowAsync("Error", $"App not found: {app.Name}", NotificationType.Error);
                    return false;
                }

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = appDir,
                    Arguments = arguments ?? app.Arguments ?? string.Empty,
                    UseShellExecute = false
                };

                // Set environment variables for SDK
                psi.EnvironmentVariables["ESTARTER_APP_ID"] = app.Id;
                psi.EnvironmentVariables["ESTARTER_APP_VERSION"] = app.Version;

                var process = System.Diagnostics.Process.Start(psi);
                if (process == null) return false;

                // Initialize sandbox for the app
                FileSystem.InitializeAppSandbox(app.Id);

                return true;
            }
            catch (Exception ex)
            {
                await Notifications.ShowAsync("Launch Error", ex.Message, NotificationType.Error);
                return false;
            }
        }

        /// <summary>
        /// Handle permission request from kernel.
        /// </summary>
        private void OnPermissionRequested(string appId, Permission permission, bool _)
        {
            DispatchToUI(() => PermissionRequested?.Invoke(appId, permission));
        }

        /// <summary>
        /// Dispatch action to UI thread.
        /// </summary>
        private void DispatchToUI(Action action)
        {
            if (_dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                _dispatcher.BeginInvoke(action);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Stop();
            _systemBus.Dispose();
            _kernel.Dispose();
        }
    }
}
