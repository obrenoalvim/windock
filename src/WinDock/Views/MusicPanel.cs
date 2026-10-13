using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinDock.Services;

namespace WinDock.Views;

public sealed class MusicPanel : Flyout
{
    readonly MediaSessions media;
    const double CardWidth = 280;
    readonly StackPanel body = new();

    public MusicPanel(MediaSessions media, bool dark) : base(dark)
    {
        this.media = media;
        SetBody(body);
        Render();
        media.Changed += Render;
        Closed += (_, _) => media.Changed -= Render;
    }

    void Render()
    {
        body.Children.Clear();
        var all = media.All;
        if (all.Count == 0)
        {
            body.Width = 300;
            body.Children.Add(Text(Loc.T("musicTitle"), 13, font: Display));
            var idle = Text(Loc.T("musicIdleLong"), 11, muted: true);
            idle.TextWrapping = TextWrapping.Wrap;
            idle.Margin = new Thickness(0, 10, 0, 2);
            body.Children.Add(idle);
            return;
        }

        body.Width = double.NaN;
        if (all.Count == 1)
        {
            body.Children.Add(Card(all[0]));
            return;
        }

        // Every player side by side, three per row.
        var row = new WrapPanel { MaxWidth = 3 * (CardWidth + 24 + 10), Margin = new Thickness(-5) };
        foreach (var now in all)
            row.Children.Add(new Border
            {
                Padding = new Thickness(12),
                Margin = new Thickness(5),
                CornerRadius = new CornerRadius(14),
                Background = Hover,
                Child = Card(now)
            });
        body.Children.Add(row);
    }

    StackPanel Card(MediaInfo now)
    {
        var card = new StackPanel { Width = CardWidth };
        var art = new Border
        {
            Width = 76,
            Height = 76,
            CornerRadius = new CornerRadius(12),
            Background = Track,
            Child = now.Cover is null ? Centered(Glyph("\uE8D6", 26)) : null
        };
        if (now.Cover is { } cover) art.Background = new ImageBrush(cover) { Stretch = Stretch.UniformToFill };

        var title = Text(now.Title, 13);
        title.FontWeight = FontWeights.Bold;
        title.TextWrapping = TextWrapping.Wrap;
        title.MaxHeight = 36;
        var info = new StackPanel { Margin = new Thickness(14, 2, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = { title } };
        if (now.Artist.Length > 0) info.Children.Add(Spaced(Text(now.Artist, 11, muted: true), 3));
        if (Source(now.AppId) is { } app) info.Children.Add(Spaced(Text(app, 10, muted: true), 6));

        var top = new DockPanel();
        DockPanel.SetDock(art, Dock.Left);
        top.Children.Add(art);
        top.Children.Add(info);
        card.Children.Add(top);

        var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 16, 0, 2) };
        controls.Children.Add(Round("\uE892", () => _ = media.Previous(now), now.CanPrevious, 38));
        controls.Children.Add(Round(now.Playing ? "\uE769" : "\uE768", () => _ = media.Toggle(now), true, 48));
        controls.Children.Add(Round("\uE893", () => _ = media.Next(now), now.CanNext, 38));
        card.Children.Add(controls);
        return card;
    }

    static UIElement Centered(TextBlock glyph)
    {
        glyph.HorizontalAlignment = HorizontalAlignment.Center;
        return glyph;
    }

    static TextBlock Spaced(TextBlock text, double top)
    {
        text.Margin = new Thickness(0, top, 0, 0);
        return text;
    }

    Border Round(string glyph, Action click, bool enabled, double size)
    {
        var icon = Glyph(glyph, size * 0.36);
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        var button = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2),
            Margin = new Thickness(8, 0, 8, 0),
            Background = Hover,
            Cursor = enabled ? Cursors.Hand : Cursors.Arrow,
            Opacity = enabled ? 1 : 0.35,
            VerticalAlignment = VerticalAlignment.Center,
            Child = icon
        };
        if (enabled)
        {
            button.MouseEnter += (_, _) => button.Background = Track;
            button.MouseLeave += (_, _) => button.Background = Hover;
            button.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                click();
            };
        }
        return button;
    }

    static string? Source(string? appId)
    {
        if (string.IsNullOrWhiteSpace(appId)) return null;
        if (appId.Contains('!')) return ShellIcons.NameForAppId(appId);
        var name = appId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? appId[..^4] : appId;
        return name.Length > 0 ? char.ToUpperInvariant(name[0]) + name[1..] : null;
    }
}
