using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinDock.Services;
using WinDock.ViewModels;

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
            body.Children.Add(all[0].Cover is null ? Card(all[0]) : Framed(all[0], new Thickness(-16, -14, -16, -14), new Thickness(16, 14, 16, 14), 17));
            return;
        }

        var row = new WrapPanel { MaxWidth = 3 * (CardWidth + 24 + 10), Margin = new Thickness(-5) };
        foreach (var now in all) row.Children.Add(Framed(now, new Thickness(5), new Thickness(12), 14));
        body.Children.Add(row);
    }

    Grid Framed(MediaInfo now, Thickness margin, Thickness padding, double radius)
    {
        var corner = new CornerRadius(radius);
        var frame = new Grid { Margin = margin };
        if (MusicWidget.Smear(now.Cover) is { } smear)
        {
            frame.Children.Add(new Border { CornerRadius = corner, Background = new ImageBrush(smear) { Stretch = Stretch.Fill } });
            frame.Children.Add(new Border { CornerRadius = corner, Background = Shade });
        }
        else frame.Children.Add(new Border { CornerRadius = corner, Background = Hover });
        frame.Children.Add(new Border { Padding = padding, Child = Card(now) });
        return frame;
    }

    static readonly Brush Shade = Paint("#66000000");
    static readonly Brush CoverInk = Paint("#F4EFE7");
    static readonly Brush CoverMuted = Paint("#CCF4EFE7");
    static readonly Brush CoverHover = Paint("#2EFFFFFF");
    static readonly Brush CoverTrack = Paint("#4DFFFFFF");

    StackPanel Card(MediaInfo now)
    {
        var lit = now.Cover is not null;
        TextBlock Lit(TextBlock text, bool muted = false)
        {
            if (lit) text.Foreground = muted ? CoverMuted : CoverInk;
            return text;
        }

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

        var title = Lit(Text(now.Title, 13));
        title.FontWeight = FontWeights.Bold;
        title.TextWrapping = TextWrapping.Wrap;
        title.MaxHeight = 36;
        var info = new StackPanel { Margin = new Thickness(14, 2, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = { title } };
        if (now.Artist.Length > 0) info.Children.Add(Spaced(Lit(Text(now.Artist, 11, muted: true), true), 3));
        if (Source(now.AppId) is { } app) info.Children.Add(Spaced(Lit(Text(app, 10, muted: true), true), 6));

        var top = new DockPanel();
        DockPanel.SetDock(art, Dock.Left);
        top.Children.Add(art);
        top.Children.Add(info);
        card.Children.Add(top);

        var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 16, 0, 2) };
        controls.Children.Add(Round("\uE892", () => _ = media.Previous(now), now.CanPrevious, 38, lit));
        controls.Children.Add(Round(now.Playing ? "\uE769" : "\uE768", () => _ = media.Toggle(now), true, 48, lit));
        controls.Children.Add(Round("\uE893", () => _ = media.Next(now), now.CanNext, 38, lit));
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

    Border Round(string glyph, Action click, bool enabled, double size, bool lit)
    {
        var icon = Glyph(glyph, size * 0.36);
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        var (rest, hover) = lit ? (CoverHover, CoverTrack) : (Hover, Track);
        if (lit) icon.Foreground = CoverInk;
        var button = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2),
            Margin = new Thickness(8, 0, 8, 0),
            Background = rest,
            Cursor = enabled ? Cursors.Hand : Cursors.Arrow,
            Opacity = enabled ? 1 : 0.35,
            VerticalAlignment = VerticalAlignment.Center,
            Child = icon
        };
        if (enabled)
        {
            button.MouseEnter += (_, _) => button.Background = hover;
            button.MouseLeave += (_, _) => button.Background = rest;
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
