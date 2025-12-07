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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
