using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Viewer;

public partial class App : Application
{
}

public sealed class BoolToHighlightConverter : IValueConverter
{
    private static readonly Brush Highlight = new SolidColorBrush(Color.FromRgb(0x4A, 0x9E, 0xFF));
    private static readonly Brush None = Brushes.Transparent;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Highlight : None;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
