using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using eStarter.Models;

namespace eStarter.Converters
{
    public class TileSizeToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TileSize size)
            {
                return size switch
                {
                    TileSize.Small => 70.0,
                    TileSize.Medium => 150.0,
                    TileSize.Wide => 310.0,
                    TileSize.Large => 310.0,
                    _ => 150.0
                };
            }
            return 150.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class TileSizeToHeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TileSize size)
            {
                return size switch
                {
                    TileSize.Small => 70.0,
                    TileSize.Medium => 150.0,
                    TileSize.Wide => 150.0,
                    TileSize.Large => 310.0,
                    _ => 150.0
                };
            }
            return 150.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class TileSizeToFontSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TileSize size)
            {
                return size switch
                {
                    TileSize.Small => 10.0,
                    TileSize.Medium => 16.0,
                    TileSize.Wide => 16.0,
                    TileSize.Large => 20.0,
                    _ => 16.0
                };
            }
            return 16.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class TileSizeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TileSize size && parameter is string param)
            {
                // "small" means hide on small tiles
                if (param == "small")
                    return size == TileSize.Small ? Visibility.Collapsed : Visibility.Visible;
                
                // "description" means show only on wide/large
                if (param == "description")
                    return (size == TileSize.Wide || size == TileSize.Large) ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
