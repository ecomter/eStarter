using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace eStarter.Models
{
    public enum TileSize
    {
        Small,   // 1x1 - 70x70
        Medium,  // 2x2 - 150x150 (default)
        Wide,    // 4x2 - 310x150
        Large    // 4x4 - 310x310
    }

    public class AppEntry : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _name = string.Empty;
        private string? _description;
        private string? _iconPath;
        private string _background = "#FF0078D7";
        private int _badgeCount;
        private TileSize _tileSize = TileSize.Medium;
        private bool _showName = true;
        private bool _showDescription = true;

        // New properties for built-in software support
        private string _publisher = string.Empty;
        private string _version = "1.0.0";
        private System.DateTime _installDate = System.DateTime.Now;
        private long _size; // in bytes
        private string? _exePath;
        private string? _arguments;
        private string _category = "General";
        private string _status = "Ready";

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string? Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public string? IconPath
        {
            get => _iconPath;
            set { _iconPath = value; OnPropertyChanged(); }
        }

        public string Background
        {
            get => _background;
            set { _background = value; OnPropertyChanged(); }
        }

        public int BadgeCount
        {
            get => _badgeCount;
            set { _badgeCount = value; OnPropertyChanged(); }
        }

        public TileSize TileSize
        {
            get => _tileSize;
            set { _tileSize = value; OnPropertyChanged(); }
        }

        public bool ShowName
        {
            get => _showName;
            set { _showName = value; OnPropertyChanged(); }
        }

        public bool ShowDescription
        {
            get => _showDescription;
            set { _showDescription = value; OnPropertyChanged(); }
        }

        // Extended Properties
        public string Publisher
        {
            get => _publisher;
            set { _publisher = value; OnPropertyChanged(); }
        }

        public string Version
        {
            get => _version;
            set { _version = value; OnPropertyChanged(); }
        }

        public System.DateTime InstallDate
        {
            get => _installDate;
            set { _installDate = value; OnPropertyChanged(); }
        }

        public long Size
        {
            get => _size;
            set { _size = value; OnPropertyChanged(); }
        }

        public string? ExePath
        {
            get => _exePath;
            set { _exePath = value; OnPropertyChanged(); }
        }

        public string? Arguments
        {
            get => _arguments;
            set { _arguments = value; OnPropertyChanged(); }
        }

        public string Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
