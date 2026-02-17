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
using eStarter.Core;
using eStarter.Models;
using eStarter.ViewModels;
using eStarter.Views;

namespace eStarter;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    // Drag-to-reorder state
    private Point _dragStartPoint;
    private bool _isDragInProgress;
    private AppEntry? _draggedApp;
    private Window? _dragGhostWindow;
    private DropIndicatorAdorner? _dropAdorner;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();

        this.PreviewTextInput += MainWindow_PreviewTextInput;
        this.PreviewKeyDown += MainWindow_PreviewKeyDown;
    }

    // ── Win8 Tile Context Menu (built programmatically) ───────────────

    private void Tile_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not AppEntry app)
            return;

        var vm = DataContext as MainViewModel;
        if (vm == null) return;

        var menu = new ContextMenu
        {
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 70)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(0, 4, 0, 4),
            Foreground = Brushes.White
        };

        // ── Resize submenu ──
        var resizeItem = new MenuItem
        {
            Header = "Resize",
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 48))
        };

        foreach (TileSize size in Enum.GetValues(typeof(TileSize)))
        {
            var sizeItem = new MenuItem
            {
                Header = size.ToString(),
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                IsCheckable = false,
                // Show a bullet for the currently active size
                Icon = app.TileSize == size
                    ? new System.Windows.Shapes.Ellipse
                    {
                        Width = 8, Height = 8,
                        Fill = (Brush)FindResource("MetroAccentBrush")
                    }
                    : null
            };

            var capturedSize = size;
            sizeItem.Click += (s, args) =>
            {
                app.TileSize = capturedSize;
                vm.SaveTileOrderAsync();
            };

            resizeItem.Items.Add(sizeItem);
        }
        menu.Items.Add(resizeItem);

        // ── Change color ──
        var colorItem = new MenuItem
        {
            Header = "Change color",
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 48))
        };
        colorItem.Click += (s, args) =>
        {
            vm.ChangeTileColorCommand.Execute(app);
        };
        menu.Items.Add(colorItem);

        fe.ContextMenu = menu;
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

            // Win8-style staggered entrance: fast 40ms stagger, crisp 350ms slide
            var delay = TimeSpan.FromMilliseconds(index * 40 + 80);

            var sb = new Storyboard();

            // Opacity Animation
            var fadeAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(350)),
                BeginTime = delay,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(fadeAnim, element);
            Storyboard.SetTargetProperty(fadeAnim, new PropertyPath("Opacity"));

            // Slide X Animation — smaller offset for subtle, fluid entrance
            var slideXAnim = new DoubleAnimation
            {
                From = 120,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(400)),
                BeginTime = delay,
                EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 5 }
            };
            Storyboard.SetTarget(slideXAnim, element);
            Storyboard.SetTargetProperty(slideXAnim, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

            sb.Children.Add(fadeAnim);
            sb.Children.Add(slideXAnim);
            sb.Begin();

            // Listen for TileSize changes to play resize animation
            app.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(AppEntry.TileSize))
                {
                    Dispatcher.BeginInvoke(new Action(() => PlayResizeAnimation(element)));
                }
            };
        }
    }

    private static void PlayResizeAnimation(FrameworkElement element)
    {
        // Find the tile Button inside the ContentControl, then its internal ScaleTransform
        var button = FindVisualChild<Button>(element);
        if (button == null) return;

        // The template has a Grid named "RootGrid" with a ScaleTransform
        // Animate opacity on the whole ContentControl for a clean crossfade
        var sb = new Storyboard();

        // Smooth opacity dip — brief fade out then back in
        var fadeOut = new DoubleAnimation
        {
            To = 0.0,
            Duration = new Duration(TimeSpan.FromMilliseconds(120)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(fadeOut, element);
        Storyboard.SetTargetProperty(fadeOut, new PropertyPath("Opacity"));

        sb.Children.Add(fadeOut);

        sb.Completed += (s, args) =>
        {
            // Size has already changed via binding; now fade + scale back in
            element.RenderTransformOrigin = new Point(0.5, 0.5);
            element.RenderTransform = new ScaleTransform(0.9, 0.9);

            var sb2 = new Storyboard();

            var fadeIn = new DoubleAnimation
            {
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(fadeIn, element);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath("Opacity"));

            var scaleXIn = new DoubleAnimation
            {
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(250)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(scaleXIn, element);
            Storyboard.SetTargetProperty(scaleXIn, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));

            var scaleYIn = new DoubleAnimation
            {
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(250)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(scaleYIn, element);
            Storyboard.SetTargetProperty(scaleYIn, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));

            sb2.Children.Add(fadeIn);
            sb2.Children.Add(scaleXIn);
            sb2.Children.Add(scaleYIn);

            sb2.Completed += (s2, args2) =>
            {
                // Restore original transform state cleanly
                element.RenderTransform = new TranslateTransform(0, 0);
            };

            sb2.Begin();
        };

        sb.Begin();
    }

    public void ShowPage(UserControl page)
    {
        PageHost.Content = page;
        PageHostContainer.Visibility = Visibility.Visible;

        // Win8-style crisp slide-in
        var sb = new Storyboard();
        var slideAnim = new DoubleAnimation
        {
            From = 60,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(250)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var fadeAnim = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
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
        // Win8-style crisp slide-out
        var sb = new Storyboard();
        var slideAnim = new DoubleAnimation
        {
            To = 60,
            Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        var fadeAnim = new DoubleAnimation
        {
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
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

    // ── Tile Drag-to-Reorder ──────────────────────────────────────────

    private WrapPanel? FindTilesPanel()
    {
        // Walk the visual tree from the ItemsControl to find its WrapPanel
        return FindVisualChild<WrapPanel>(TilesItemsControl);
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result) return result;
            var descendant = FindVisualChild<T>(child);
            if (descendant != null) return descendant;
        }
        return null;
    }

    private void Tile_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(this);
    }

    private void Tile_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _isDragInProgress)
            return;

        var pos = e.GetPosition(this);
        var diff = pos - _dragStartPoint;

        // Drag threshold to avoid accidental drags
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (sender is FrameworkElement fe && fe.DataContext is AppEntry app)
        {
            _isDragInProgress = true;
            _draggedApp = app;

            // Dim the source tile
            fe.Opacity = 0.3;

            // Create ghost window that follows cursor
            CreateDragGhost(fe);

            var data = new DataObject("TileApp", app);
            DragDrop.DoDragDrop(fe, data, DragDropEffects.Move);

            // Cleanup
            fe.Opacity = 1.0;
            DestroyDragGhost();
            RemoveDropIndicator();
            _isDragInProgress = false;
            _draggedApp = null;
        }
    }

    private void TileArea_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("TileApp"))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        // Move ghost window to follow cursor
        UpdateDragGhost();

        // Show drop indicator at target position
        var panel = FindTilesPanel();
        if (panel != null)
        {
            int idx = GetDropTargetIndex(e.GetPosition(panel), panel);
            ShowDropIndicator(panel, idx);
        }
    }

    private void TileArea_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("TileApp")) return;

        var droppedApp = e.Data.GetData("TileApp") as AppEntry;
        if (droppedApp == null) return;

        var vm = DataContext as MainViewModel;
        if (vm == null) return;

        int oldIndex = vm.InstalledApps.IndexOf(droppedApp);
        if (oldIndex < 0) return;

        // Find the target tile at the drop position
        var panel = FindTilesPanel();
        int newIndex = panel != null
            ? GetDropTargetIndex(e.GetPosition(panel), panel)
            : vm.InstalledApps.Count - 1;

        if (newIndex < 0) newIndex = vm.InstalledApps.Count - 1;
        if (newIndex >= vm.InstalledApps.Count) newIndex = vm.InstalledApps.Count - 1;

        vm.MoveApp(oldIndex, newIndex);
        e.Handled = true;
    }

    private void TileArea_GiveFeedback(object sender, GiveFeedbackEventArgs e)
    {
        e.UseDefaultCursors = false;
        Mouse.SetCursor(Cursors.Hand);
        e.Handled = true;
    }

    private static int GetDropTargetIndex(Point dropPos, WrapPanel panel)
    {
        for (int i = 0; i < panel.Children.Count; i++)
        {
            if (panel.Children[i] is not FrameworkElement child) continue;

            var topLeft = child.TranslatePoint(new Point(0, 0), panel);
            var bounds = new Rect(topLeft, new Point(topLeft.X + child.ActualWidth, topLeft.Y + child.ActualHeight));

            if (bounds.Contains(dropPos))
                return i;

            var centerX = bounds.Left + bounds.Width / 2;
            if (dropPos.Y >= bounds.Top && dropPos.Y < bounds.Bottom && dropPos.X < centerX)
                return i;
        }

        return panel.Children.Count > 0 ? panel.Children.Count - 1 : 0;
    }

    private void ShowDropIndicator(WrapPanel panel, int targetIndex)
    {
        double x = 0, y = 0, height = 150;

        if (panel.Children.Count > 0)
        {
            if (targetIndex >= 0 && targetIndex < panel.Children.Count)
            {
                var child = panel.Children[targetIndex] as FrameworkElement;
                if (child != null)
                {
                    var pos = child.TranslatePoint(new Point(0, 0), panel);
                    x = pos.X;
                    y = pos.Y;
                    height = child.ActualHeight;
                }
            }
            else
            {
                var last = panel.Children[panel.Children.Count - 1] as FrameworkElement;
                if (last != null)
                {
                    var pos = last.TranslatePoint(new Point(0, 0), panel);
                    x = pos.X + last.ActualWidth;
                    y = pos.Y;
                    height = last.ActualHeight;
                }
            }
        }

        var layer = AdornerLayer.GetAdornerLayer(panel);
        if (layer == null) return;

        if (_dropAdorner == null)
        {
            var accentBrush = (Brush)FindResource("MetroAccentBrush");
            _dropAdorner = new DropIndicatorAdorner(panel, accentBrush);
            layer.Add(_dropAdorner);
        }

        _dropAdorner.UpdatePosition(x, y, height);
    }

    private void RemoveDropIndicator()
    {
        if (_dropAdorner != null)
        {
            var panel = FindTilesPanel();
            if (panel != null)
            {
                var layer = AdornerLayer.GetAdornerLayer(panel);
                layer?.Remove(_dropAdorner);
            }
            _dropAdorner = null;
        }
    }

    private void CreateDragGhost(FrameworkElement source)
    {
        try
        {
            var w = (int)source.ActualWidth;
            var h = (int)source.ActualHeight;
            if (w <= 0 || h <= 0) return;

            var renderBitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            renderBitmap.Render(source);

            var image = new Image
            {
                Source = renderBitmap,
                Opacity = 0.8,
                Stretch = Stretch.None,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    ShadowDepth = 0,
                    BlurRadius = 12,
                    Opacity = 0.4
                }
            };

            _dragGhostWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false,
                Topmost = true,
                IsHitTestVisible = false,
                Width = w + 24,   // extra room for shadow
                Height = h + 24,
                Content = image
            };

            _dragGhostWindow.Show();
            UpdateDragGhost();
        }
        catch
        {
            // Drag still works without ghost visual
        }
    }

    private void UpdateDragGhost()
    {
        if (_dragGhostWindow == null) return;

        var screenPos = PointToScreen(Mouse.GetPosition(this));
        _dragGhostWindow.Left = screenPos.X - _dragGhostWindow.Width / 2;
        _dragGhostWindow.Top = screenPos.Y - _dragGhostWindow.Height / 2;
    }

    private void DestroyDragGhost()
    {
        _dragGhostWindow?.Close();
        _dragGhostWindow = null;
    }
}