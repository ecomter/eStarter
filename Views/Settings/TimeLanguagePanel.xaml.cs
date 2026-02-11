using System;
using System.Windows.Controls;
using System.Windows.Threading;

namespace eStarter.Views.Settings
{
    public partial class TimeLanguagePanel : UserControl
    {
        private readonly DispatcherTimer _timer;

        public TimeLanguagePanel()
        {
            InitializeComponent();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            UpdateTime();
            UpdateLanguagePreview();

            Unloaded += (s, e) => _timer.Stop();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            UpdateTime();
        }

        private void UpdateTime()
        {
            var now = DateTime.Now;
            CurrentTimeText.Text = now.ToString("HH:mm:ss");
            CurrentDateText.Text = now.ToString("dddd, MMMM d, yyyy");
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateLanguagePreview();
        }

        private void UpdateLanguagePreview()
        {
            if (LanguageComboBox?.SelectedItem is string lang)
            {
                if (lang == "中文")
                {
                    PreviewHello.Text = "你好";
                    PreviewSettings.Text = "设置";
                    PreviewAbout.Text = "关于";
                }
                else
                {
                    PreviewHello.Text = "Hello";
                    PreviewSettings.Text = "Settings";
                    PreviewAbout.Text = "About";
                }
            }
        }
    }
}
