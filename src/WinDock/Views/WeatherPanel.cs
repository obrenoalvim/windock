using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using WinDock.Services;
using WinDock.ViewModels;

namespace WinDock.Views;

public sealed class WeatherPanel : Flyout
{
    static readonly FontFamily Symbols = new("Segoe UI Symbol");

    readonly Weather weather;
    readonly StackPanel body = new() { Width = 340 };

    public WeatherPanel(Weather weather, bool dark) : base(dark)
    {
        this.weather = weather;
        SetBody(body);
        Render();
        weather.Updated += Render;
        Closed += (_, _) => weather.Updated -= Render;
    }

    void Render()
    {
        body.Children.Clear();
        var report = weather.Last;
        body.Children.Add(Text(report?.Place ?? weather.Place ?? Loc.T("widget_weather"), 14, font: Display));

        if (report is not null)
        {
            var (glyph, key) = Weather.Describe(report.Code, report.Day);
            var icon = Symbol(glyph, 40);
            icon.Margin = new Thickness(0, 0, 14, 0);
            var temp = Text(Weather.Degrees(report.Temp), 30);
            temp.FontWeight = FontWeights.Bold;
            temp.Margin = new Thickness(0, 0, 16, 0);
            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(Text(Loc.T(key), 12));
            info.Children.Add(Spaced(Text(Loc.F("weatherFeels", Weather.Degrees(report.Feels)), 10, muted: true), 4));
            info.Children.Add(Spaced(Text($"↑ {Weather.Degrees(report.High)}   ↓ {Weather.Degrees(report.Low)}", 10, muted: true), 3));
            body.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 14, 0, 16),
                Children = { icon, temp, info }
            });

            var hours = new UniformGrid { Rows = 1, Margin = new Thickness(0, 0, 0, 14) };
            foreach (var hour in report.Hours.Take(6))
            {
                var column = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                column.Children.Add(Centered(Text(Hour(hour.Time), 10, muted: true)));
                var symbol = Symbol(Weather.Describe(hour.Code, hour.Day).Glyph, 18);
                symbol.Margin = new Thickness(0, 6, 0, 6);
                column.Children.Add(Centered(symbol));
                column.Children.Add(Centered(Text(Weather.Degrees(hour.Temp), 11)));
                hours.Children.Add(column);
            }
            body.Children.Add(hours);
        }

        if (weather.Status != WeatherStatus.Ok)
        {
            var key = weather.Status switch
            {
                WeatherStatus.NoPlace => "weatherNoPlaceLong",
                WeatherStatus.NotFound => "weatherNotFoundLong",
                _ => WeatherWidget.StatusKey(weather.Status)
            };
            var note = Text(Loc.F(key, weather.Place ?? ""), 11, muted: true);
            note.TextWrapping = TextWrapping.Wrap;
            note.Margin = new Thickness(0, 10, 0, 12);
            body.Children.Add(note);
        }

        var footer = new DockPanel();
        var refresh = Action(Loc.T("refresh"), () =>
        {
            if (!weather.Refresh(DateTime.Now)) Render();
        }, "");
        DockPanel.SetDock(refresh, Dock.Right);
        footer.Children.Add(refresh);
        footer.Children.Add(Text("Open-Meteo", 10, muted: true));
        body.Children.Add(footer);
    }

    TextBlock Symbol(string glyph, double size)
    {
        var symbol = Text(glyph, size);
        symbol.FontFamily = Symbols;
        return symbol;
    }

    static string Hour(DateTime time) =>
        Loc.Instance.Language == Loc.Portuguese ? $"{time:HH}h" : time.ToString("h tt", System.Globalization.CultureInfo.InvariantCulture);

    static TextBlock Centered(TextBlock text)
    {
        text.HorizontalAlignment = HorizontalAlignment.Center;
        return text;
    }

    static TextBlock Spaced(TextBlock text, double top)
    {
        text.Margin = new Thickness(0, top, 0, 0);
        return text;
    }
}
