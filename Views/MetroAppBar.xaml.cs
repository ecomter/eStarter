using System;
using System.Windows;
using System.Windows.Controls;

namespace eStarter.Views
{
    public partial class MetroAppBar : UserControl
    {
        public event Action? AllAppsClicked;
        public event Action? SearchClicked;
        public event Action? RefreshClicked;
        public event Action? SettingsClicked;
        public event Action? PowerClicked;

        public MetroAppBar()
        {
            InitializeComponent();
        }

        private void AllAppsButton_Click(object sender, RoutedEventArgs e)
        {
            AllAppsClicked?.Invoke();
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchClicked?.Invoke();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshClicked?.Invoke();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsClicked?.Invoke();
        }

        private void PowerButton_Click(object sender, RoutedEventArgs e)
        {
            PowerClicked?.Invoke();
            ShowPowerMenu();
        }

        private void ShowPowerMenu()
        {
            var menu = new ContextMenu
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(31, 31, 31)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(51, 255, 255, 255)),
                Foreground = System.Windows.Media.Brushes.White
            };

            var sleepItem = new MenuItem 
            { 
                Header = "Sleep", 
                Foreground = System.Windows.Media.Brushes.White 
            };
            sleepItem.Click += (s, e) => { /* Sleep action */ };

            var restartItem = new MenuItem 
            { 
                Header = "Restart", 
                Foreground = System.Windows.Media.Brushes.White 
            };
            restartItem.Click += (s, e) => 
            {
                if (MessageBox.Show("Restart eStarter?", "Confirm", 
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(
                        System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "");
                    Application.Current.Shutdown();
                }
            };

            var shutdownItem = new MenuItem 
            { 
                Header = "Shut down", 
                Foreground = System.Windows.Media.Brushes.White 
            };
            shutdownItem.Click += (s, e) => 
            {
                if (MessageBox.Show("Close eStarter?", "Confirm", 
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    Application.Current.Shutdown();
                }
            };

            menu.Items.Add(sleepItem);
            menu.Items.Add(restartItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(shutdownItem);

            menu.PlacementTarget = PowerButton;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            menu.IsOpen = true;
        }
    }
}
