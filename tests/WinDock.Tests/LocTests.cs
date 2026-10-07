using System.Text.RegularExpressions;
using WinDock.Services;

namespace WinDock.Tests;

public class LocTests
{
    [Fact]
    public void English_is_the_default_language()
    {
        Assert.Equal(Loc.English, Loc.Instance.Language);
        Assert.Equal("Apply", Loc.T("apply"));
    }

    [Fact]
    public void Switching_language_changes_the_text_and_notifies_bindings()
    {
        var notified = 0;
        void Handler(object? s, System.ComponentModel.PropertyChangedEventArgs e) => notified++;
        Loc.Instance.PropertyChanged += Handler;
        try
        {
            Loc.Instance.Set(Loc.Portuguese);
            Assert.Equal("Aplicar", Loc.T("apply"));
            Assert.Equal("pt-BR", Loc.Instance.Culture.Name);
            Assert.Equal(1, notified);

            Loc.Instance.Set(Loc.Portuguese);
            Assert.Equal(1, notified);
        }
        finally
        {
            Loc.Instance.PropertyChanged -= Handler;
            Loc.Instance.Set(Loc.English);
        }
    }

    [Fact]
    public void Every_key_has_both_languages_with_the_same_placeholders()
    {
        foreach (var key in Loc.Keys)
        {
            Loc.Instance.Set(Loc.English);
            var en = Loc.T(key);
            Loc.Instance.Set(Loc.Portuguese);
            var pt = Loc.T(key);
            Loc.Instance.Set(Loc.English);

            Assert.False(string.IsNullOrWhiteSpace(en), key);
            Assert.False(string.IsNullOrWhiteSpace(pt), key);
            Assert.Equal(Placeholders(en), Placeholders(pt));
        }
    }

    [Fact]
    public void Unknown_key_falls_back_to_the_key_itself()
    {
        Assert.Equal("missing.key", Loc.T("missing.key"));
    }

    static string Placeholders(string text) =>
        string.Join(',', Regex.Matches(text, @"\{\d+\}").Select(m => m.Value).Order());
}
