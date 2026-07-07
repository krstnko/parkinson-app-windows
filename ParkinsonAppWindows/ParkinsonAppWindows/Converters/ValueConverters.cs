using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ParkinsonAppWindows.Converters
{
    public class BooleanToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isDark && isDark) return Brushes.Black;
            return Brushes.White;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BoolToPlayPauseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isPlaying && isPlaying) return "⏸ Pause";
            return "▶ Play";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isVisible && isVisible)
                return Visibility.Visible;
            return Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ModeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Темная тема -> Темно-серый, Светлая -> Почти белый
            if (value is bool isDark && isDark)
                return new SolidColorBrush(Color.FromRgb(30, 30, 30));
            return new SolidColorBrush(Color.FromRgb(245, 245, 245));
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ModeToToolbarColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isDark && isDark)
                return new SolidColorBrush(Color.FromRgb(45, 45, 48));
            return Brushes.White;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ModeToTextColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Темная тема -> Белый текст, Светлая -> Черный текст
            if (value is bool isDark && isDark)
                return Brushes.White;
            return Brushes.Black;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b) return Visibility.Collapsed;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // Инверсия булева значения: True -> False, False -> True
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return false;
        }
    }

    public class BooleanToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return new SolidColorBrush(Color.FromRgb(30, 30, 30)); // #1E1E1E

            return new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class StringToVisConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class CollectionToVisConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Collections.ICollection collection && collection.Count > 0)
                return Visibility.Visible;

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class PlayPauseTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isPlaying && isPlaying) return "Pause";
            return "Play";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ProbToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double prob && double.TryParse(parameter?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double maxWidth))
            {
                return (prob / 100.0) * maxWidth;
            }
            return 0.0;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BrushToTranslucentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush brush)
            {
                var c = brush.Color;
                var newColor = Color.FromArgb(35, c.R, c.G, c.B);
                return new SolidColorBrush(newColor);
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
