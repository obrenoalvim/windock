using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using WinDock.Models;
using WinDock.Services;

namespace WinDock.Views;

public sealed class PickerEntry(CatalogEntry entry)
{
    ImageSource? icon;

    public CatalogEntry Entry => entry;
    public string Name => entry.Name;
    public string Category => Loc.T("cat" + entry.Category).ToUpperInvariant();
    public ImageSource? Icon => icon ??= entry.IconSource is { } source ? ShellIcons.ForPath(source) : null;
}

public partial class ProgramPicker : Window
{
    const string All = "All";

    string category = All;

    public ProgramPicker()
    {
        InitializeComponent();
        var entries = Catalog.Load().Select(e => new PickerEntry(e)).ToList();
        List.ItemsSource = entries;

        var names = new[] { All, Catalog.Programs, Catalog.Store, Catalog.Steam, Catalog.Epic };
        foreach (var name in names.Where(n => n == All || entries.Any(e => e.Entry.Category == n)))
        {
            var tab = new ToggleButton { Content = Loc.T(name == All ? "tabAll" : "cat" + name), Style = (Style)FindResource("ChipToggle"), IsChecked = name == All, Tag = name };
            tab.Click += OnTab;
            Tabs.Children.Add(tab);
        }

        Loaded += (_, _) => Search.Focus();
    }

    public List<PinItem> Chosen { get; } = [];

    void OnDrag(object sender, MouseButtonEventArgs e) => DragMove();

    void OnTab(object sender, RoutedEventArgs e)
    {
        category = (string)((ToggleButton)sender).Tag;
        foreach (ToggleButton tab in Tabs.Children) tab.IsChecked = (string)tab.Tag == category;
        Refresh();
    }

    void OnSearch(object sender, TextChangedEventArgs e) => Refresh();

    void Refresh()
    {
        var text = Search.Text.Trim();
        var view = CollectionViewSource.GetDefaultView(List.ItemsSource);
        view.Filter = o =>
        {
            var entry = (PickerEntry)o;
            return (category == All || entry.Entry.Category == category)
                && (text.Length == 0 || entry.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase));
        };
    }

    void OnConfirm(object sender, RoutedEventArgs e)
    {
        Chosen.AddRange(List.SelectedItems.Cast<PickerEntry>().Select(p => p.Entry.Make()));
        if (Chosen.Count > 0) DialogResult = true;
    }

    void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.T("browseTitle"),
            Filter = Loc.T("browseFilter"),
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != true) return;
        Chosen.AddRange(dialog.FileNames.Select(f => new PinItem { Name = Path.GetFileNameWithoutExtension(f), Target = f }));
        DialogResult = true;
    }
}
