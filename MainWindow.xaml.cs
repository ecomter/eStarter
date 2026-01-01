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

        this.PreviewTextInput += MainWindow_PreviewTextInput;
        this.PreviewKeyDown += MainWindow_PreviewKeyDown;
    }

    private void MainWindow_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var vm = DataContext as MainViewModel;
        if (vm == null) return;

        // If search is not open, capture input to start search
        if (!vm.IsSearchOpen)
        {
            // Only capture if it's a valid char and we are not already in a text input
            // (Though currently there are no other text inputs on the main screen)
            if (!string.IsNullOrEmpty(e.Text) && e.Text.Length == 1 && char.IsLetterOrDigit(e.Text[0]))
            {
                vm.IsSearchOpen = true;

                // We need to wait for visibility to change before focusing
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    SearchBox.Focus();
                    SearchBox.Text = e.Text;
                    SearchBox.CaretIndex = 1;
                }), System.Windows.Threading.DispatcherPriority.Input);

                e.Handled = true;
            }
        }
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var vm = DataContext as MainViewModel;
        if (vm == null) return;

        if (e.Key == Key.Escape)
        {
            if (vm.IsSearchOpen)
            {
                vm.IsSearchOpen = false;
                e.Handled = true;
                // Return focus to main window content
                this.Focus();
            }
            else if (PageHostContainer.Visibility == Visibility.Visible)
            {
                HidePage();
                e.Handled = true;
            }
        }
    }

    private void Tile_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is Models.AppEntry app)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            int index = vm.InstalledApps.IndexOf(app);
            if (index < 0) index = 0;

            // Staggered animation: 80ms delay per item (slower stagger)
            var delay = TimeSpan.FromMilliseconds(index * 80 + 200);

            var sb = new Storyboard();

            // Opacity Animation
            var fadeAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(800)), // Slower fade
                BeginTime = delay,
                EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 6 }
            };
            Storyboard.SetTarget(fadeAnim, element);
            Storyboard.SetTargetProperty(fadeAnim, new PropertyPath("Opacity"));

            // Slide X Animation
            var slideXAnim = new DoubleAnimation
            {
                From = 400, // Much larger offset for "fly in" effect
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(800)), // Slower slide
                BeginTime = delay,
                EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 6 }
            };
            Storyboard.SetTarget(slideXAnim, element);
            Storyboard.SetTargetProperty(slideXAnim, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

            sb.Children.Add(fadeAnim);
            sb.Children.Add(slideXAnim);

            sb.Begin();
        }
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