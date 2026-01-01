using System.Windows;

namespace eStarter.Views
{
    public partial class ModernMsgBox : Window
    {
        public ModernMsgBox(string message, string title, MessageBoxButton button)
        {
            InitializeComponent();
            TitleText.Text = string.IsNullOrWhiteSpace(title) ? GetString("Str_DefaultMessageTitle") : title;
            MessageText.Text = message;

            if (button == MessageBoxButton.YesNo)
            {
                OkButton.Content = GetString("Str_ButtonYes");
                CancelButton.Content = GetString("Str_ButtonNo");
                CancelButton.Visibility = Visibility.Visible;
            }
            else if (button == MessageBoxButton.OKCancel)
            {
                OkButton.Content = GetString("Str_ButtonOK");
                CancelButton.Content = GetString("Str_ButtonCancel");
                CancelButton.Visibility = Visibility.Visible;
            }
            else
            {
                OkButton.Content = GetString("Str_ButtonOK");
                CancelButton.Visibility = Visibility.Collapsed;
            }
        }

        private static string GetString(string key)
        {
            var value = Application.Current.Resources[key] as string;
            return string.IsNullOrEmpty(value) ? key : value;
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

        public static bool? ShowMessage(string message, string title = "", MessageBoxButton button = MessageBoxButton.OK, Window? owner = null)
        {
            var resolvedTitle = string.IsNullOrWhiteSpace(title) ? GetString("Str_DefaultMessageTitle") : title;
            var msgBox = new ModernMsgBox(message, resolvedTitle, button);
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
