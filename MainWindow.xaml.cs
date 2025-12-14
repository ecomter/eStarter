using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using eStarter.ViewModels;
using eStarter.Views;

namespace eStarter;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    public void ShowPage(UserControl page)
    {
        PageHost.Content = page;
        PageHostContainer.Visibility = Visibility.Visible;
        
        // Slide in animation
        var sb = new Storyboard();
        var slideAnim = new DoubleAnimation
        {
            From = 100,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(300)),
            EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut }
        };
        var fadeAnim = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(200))
        };

        Storyboard.SetTarget(slideAnim, PageHostContainer);
        Storyboard.SetTargetProperty(slideAnim, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
        
        Storyboard.SetTarget(fadeAnim, PageHostContainer);
        Storyboard.SetTargetProperty(fadeAnim, new PropertyPath("Opacity"));

        sb.Children.Add(slideAnim);
        sb.Children.Add(fadeAnim);
        sb.Begin();
    }

    public void HidePage()
    {
        // Slide out animation
        var sb = new Storyboard();
        var slideAnim = new DoubleAnimation
        {
            To = 100,
            Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseIn }
        };
        var fadeAnim = new DoubleAnimation
        {
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(200))
        };

        Storyboard.SetTarget(slideAnim, PageHostContainer);
        Storyboard.SetTargetProperty(slideAnim, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
        
        Storyboard.SetTarget(fadeAnim, PageHostContainer);
        Storyboard.SetTargetProperty(fadeAnim, new PropertyPath("Opacity"));

        sb.Completed += (s, e) =>
        {
            PageHost.Content = null;
            PageHostContainer.Visibility = Visibility.Collapsed;
        };

        sb.Children.Add(slideAnim);
        sb.Children.Add(fadeAnim);
        sb.Begin();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        HidePage();
    }
}