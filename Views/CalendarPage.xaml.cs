using System.Windows.Controls;
using System.Windows.Input;

namespace eStarter.Views
{
    public partial class CalendarPage : UserControl
    {
        public CalendarPage()
        {
            InitializeComponent();
        }

        private void Header_Click(object sender, MouseButtonEventArgs e)
        {
            MonthPicker.IsDropDownOpen = true;
        }
    }
}
