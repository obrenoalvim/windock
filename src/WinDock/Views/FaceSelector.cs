using System.Windows;
using System.Windows.Controls;
using WinDock.Models;
using WinDock.ViewModels;

namespace WinDock.Views;

// Picks the square template for widgets set to square (or, unset, when square is the default); the rest keep their own.
public sealed class FaceSelector : DataTemplateSelector
{
    public DataTemplate? Compact { get; set; }
    public DataTemplate? MusicPause { get; set; }
    public DataTemplate? MusicFull { get; set; }
    public DataTemplate? MusicBackdrop { get; set; }
    public bool On { get; set; } = true;

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) =>
        item is WidgetTile w && w.Square(On) ? Compact
        : item is MusicWidget music ? music.Spec.Size switch
        {
            WidgetSpec.FullSize => MusicFull,
            WidgetSpec.BackdropSize => MusicBackdrop,
            _ => MusicPause
        }
        : null;
}
