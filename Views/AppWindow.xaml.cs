using System.Windows;
using eStarter.Models;

namespace eStarter.Views
{
    public partial class AppWindow : Window
    {
        private readonly AppEntry _app;

        public AppWindow(AppEntry app)
        {
            InitializeComponent();
            _app = app;
            AppName.Text = app?.Name ?? "App";

            // Populate demo tiles inside the app to mimic main UI
            for (int i = 0; i < 8; i++)
            {
                var btn = new System.Windows.Controls.Button
                {
                    Content = $"{app?.Name} Tile {i+1}",
                    Width = 150,
                    Height = 150,
                    Margin = new Thickness(8),
                    Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(app?.Background ?? "#FF0078D7"),
                    Foreground = System.Windows.Media.Brushes.White
                };
                AppTiles.Children.Add(btn);
            }
        }
    }
}
