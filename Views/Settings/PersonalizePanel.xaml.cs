using System;
using System.Windows.Controls;
using System.Windows.Threading;

namespace eStarter.Views.Settings
{
    public partial class PersonalizePanel : UserControl
    {
        private readonly DispatcherTimer _timer;

        public PersonalizePanel()
        {
            InitializeComponent();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => UpdatePreview();
            _timer.Start();

            UpdatePreview();
            Unloaded += (s, e) => _timer.Stop();
        }

        private void UpdatePreview()
        {
            PreviewTime.Text = DateTime.Now.ToString("HH:mm");
        }
    }
}
