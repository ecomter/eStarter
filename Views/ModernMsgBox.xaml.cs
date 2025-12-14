using System.Windows;

namespace eStarter.Views
{
    public partial class ModernMsgBox : Window
    {
        public ModernMsgBox(string message, string title, MessageBoxButton button)
        {
            InitializeComponent();
            TitleText.Text = title;
            MessageText.Text = message;

            if (button == MessageBoxButton.YesNo)
            {
                OkButton.Content = "Yes";
                CancelButton.Content = "No";
                CancelButton.Visibility = Visibility.Visible;
            }
            else if (button == MessageBoxButton.OKCancel)
            {
                OkButton.Content = "OK";
                CancelButton.Content = "Cancel";
                CancelButton.Visibility = Visibility.Visible;
            }
            else
            {
                OkButton.Content = "OK";
                CancelButton.Visibility = Visibility.Collapsed;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public static bool? ShowMessage(string message, string title = "Message", MessageBoxButton button = MessageBoxButton.OK, Window? owner = null)
        {
            var msgBox = new ModernMsgBox(message, title, button);
            if (owner != null)
            {
                msgBox.Owner = owner;
            }
            else if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                msgBox.Owner = Application.Current.MainWindow;
            }
            
            return msgBox.ShowDialog();
        }
    }
}
