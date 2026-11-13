using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using WinDock.Services;

namespace WinDock.Views;

public static class Surface
{
    public static readonly DependencyProperty BrushProperty = DependencyProperty.RegisterAttached(
        "Brush", typeof(Brush), typeof(Surface), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

    public static Brush? GetBrush(DependencyObject element) => (Brush?)element.GetValue(BrushProperty);

    public static void SetBrush(DependencyObject element, Brush? value) => element.SetValue(BrushProperty, value);
}

public sealed class InkConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => WidgetLooks.Role(value as Brush, parameter as string);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
