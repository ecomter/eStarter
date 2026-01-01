using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using eStarter.ViewModels;

namespace eStarter.Views
{
    public partial class SettingsPage : UserControl
    {
        public SettingsPage()
        {
            InitializeComponent();
        }

        private string GetString(string key)
        {
            return Application.Current.Resources[key] as string ?? key;
        }

        private void SettingsNav_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SettingsNav.SelectedItem is ListBoxItem selectedItem && selectedItem.Tag is string tag)
            {
                // Hide all panels
                if (PersonalizationPanel != null) PersonalizationPanel.Visibility = Visibility.Collapsed;
                if (UpdateRecoveryPanel != null) UpdateRecoveryPanel.Visibility = Visibility.Collapsed;
                if (AboutPanel != null) AboutPanel.Visibility = Visibility.Collapsed;
                if (TimeLanguagePanel != null) TimeLanguagePanel.Visibility = Visibility.Collapsed;
                if (PlaceholderPanel != null) PlaceholderPanel.Visibility = Visibility.Collapsed;

                UIElement? targetPanel = null;

                // Show selected panel
                switch (tag)
                {
                    case "Personalization":
                        if (PersonalizationPanel != null) targetPanel = PersonalizationPanel;
                        break;
                    case "UpdateRecovery":
                        if (UpdateRecoveryPanel != null) targetPanel = UpdateRecoveryPanel;
                        break;
                    case "TimeLanguage":
                        if (TimeLanguagePanel != null) targetPanel = TimeLanguagePanel;
                        break;
                    case "About":
                        if (AboutPanel != null) targetPanel = AboutPanel;
                        break;
                    default:
                        if (PlaceholderPanel != null)
                        {
                            targetPanel = PlaceholderPanel;
                            if (PlaceholderTitle != null) PlaceholderTitle.Text = selectedItem.Content.ToString();
                        }
                        break;
                }

                if (targetPanel != null)
                {
                    targetPanel.Visibility = Visibility.Visible;
                    AnimatePanel(targetPanel);
                }
            }
        }

        private void AnimatePanel(UIElement panel)
        {
            panel.Opacity = 0;
            panel.RenderTransform = new TranslateTransform(20, 0);

            var sb = new Storyboard();
            
            var fadeAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(fadeAnim, panel);
            Storyboard.SetTargetProperty(fadeAnim, new PropertyPath("Opacity"));

            var slideAnim = new DoubleAnimation
            {
                From = 20,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(slideAnim, panel);
            Storyboard.SetTargetProperty(slideAnim, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

            sb.Children.Add(fadeAnim);
            sb.Children.Add(slideAnim);
            sb.Begin();
        }

        private void ResetAppData_Click(object sender, RoutedEventArgs e)
        {
            var result = ModernMsgBox.ShowMessage(
                GetString("Str_ResetConfirmMsg"),
                GetString("Str_ResetConfirmTitle"),
                MessageBoxButton.YesNo,
                Window.GetWindow(this));

            if (result == true)
            {
                try
                {
                    var baseDir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "eStarter");
                    if (Directory.Exists(baseDir))
                    {
                        Directory.Delete(baseDir, true);
                    }

                    ModernMsgBox.ShowMessage(GetString("Str_ResetCompleteMsg"), GetString("Str_ResetCompleteTitle"), MessageBoxButton.OK, Window.GetWindow(this));
                    
                    // Optional: Restart app
                    // System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
                    // Application.Current.Shutdown();
                }
                catch (System.Exception ex)
                {
                    ModernMsgBox.ShowMessage(string.Format(GetString("Str_ResetFailed"), ex.Message), GetString("Str_ErrorTitle"), MessageBoxButton.OK, Window.GetWindow(this));
                }
            }
        }

        private void Theme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm && e.AddedItems.Count > 0)
            {
                if (e.AddedItems[0] is ComboBoxItem item)
                {
                    vm.ChangeThemeCommand.Execute(item.Content.ToString());
                }
            }
        }

        private void AccentColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm && e.AddedItems.Count > 0)
            {
                if (e.AddedItems[0] is string color)
                {
                    vm.ChangeAccentColorCommand.Execute(color);
                }
            }
        }
    }
}
