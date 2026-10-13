using System.Windows;
using System.Windows.Controls;
using WinDock.ViewModels;

namespace WinDock.Views;

// Picks the compact square template for widgets when compact widgets are on; anything else keeps its own template.
public sealed class FaceSelector : DataTemplateSelector
{
    public DataTemplate? Compact { get; set; }
    public bool On { get; set; } = true;

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) =>
        On && item is WidgetTile and not NoteWidget ? Compact : null;
}
