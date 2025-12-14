using System.Reflection;
using System.Windows.Controls;

namespace eStarter.Views
{
    public partial class AboutPage : UserControl
    {
        public AboutPage()
        {
            InitializeComponent();
            VersionText.Text = "v" + (Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0");
        }
    }
}
