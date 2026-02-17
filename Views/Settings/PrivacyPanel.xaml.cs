using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using eStarter.Core.Kernel;
using eStarter.Services;

namespace eStarter.Views.Settings
{
    public partial class PrivacyPanel : UserControl, INotifyPropertyChanged
    {
        private int _locationAppCount;
        private int _cameraAppCount;
        private int _microphoneAppCount;
        private int _fileSystemAppCount;
        private int _networkAppCount;
        private int _ipcAppCount;

        public int LocationAppCount
        {
            get => _locationAppCount;
            set { _locationAppCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(LocationAppText)); }
        }

        public int CameraAppCount
        {
            get => _cameraAppCount;
            set { _cameraAppCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(CameraAppText)); }
        }

        public int MicrophoneAppCount
        {
            get => _microphoneAppCount;
            set { _microphoneAppCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(MicrophoneAppText)); }
        }

        public int FileSystemAppCount
        {
            get => _fileSystemAppCount;
            set { _fileSystemAppCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileSystemAppText)); }
        }

        public int NetworkAppCount
        {
            get => _networkAppCount;
            set { _networkAppCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(NetworkAppText)); }
        }

        public int IpcAppCount
        {
            get => _ipcAppCount;
            set { _ipcAppCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(IpcAppText)); }
        }

        public string LocationAppText => FormatAppCount(_locationAppCount);
        public string CameraAppText => FormatAppCount(_cameraAppCount);
        public string MicrophoneAppText => FormatAppCount(_microphoneAppCount);
        public string FileSystemAppText => FormatAppCount(_fileSystemAppCount);
        public string NetworkAppText => FormatAppCount(_networkAppCount);
        public string IpcAppText => FormatAppCount(_ipcAppCount);

        public PrivacyPanel()
        {
            InitializeComponent();
            PermissionDashboard.DataContext = this;
            Loaded += (_, _) => RefreshDashboard();
        }

        /// <summary>
        /// Query the kernel for real per-app permission grants and update dashboard counts.
        /// </summary>
        public void RefreshDashboard()
        {
            if (!KernelService.Instance.IsRunning)
            {
                LocationAppCount = CameraAppCount = MicrophoneAppCount = 0;
                FileSystemAppCount = NetworkAppCount = IpcAppCount = 0;
                return;
            }

            var perApp = KernelService.Instance.SystemSettings.GetPerAppPermissions();

            LocationAppCount = perApp.Count(p => (p.Granted & Permission.Location) != 0);
            CameraAppCount = perApp.Count(p => (p.Granted & Permission.Camera) != 0);
            MicrophoneAppCount = perApp.Count(p => (p.Granted & Permission.Microphone) != 0);
            FileSystemAppCount = perApp.Count(p => (p.Granted & Permission.FileSystem) != 0);
            NetworkAppCount = perApp.Count(p => (p.Granted & Permission.Network) != 0);
            IpcAppCount = perApp.Count(p => (p.Granted & Permission.Ipc) != 0);
        }

        private static string FormatAppCount(int count) =>
            count == 1 ? "1 app" : $"{count} apps";

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
