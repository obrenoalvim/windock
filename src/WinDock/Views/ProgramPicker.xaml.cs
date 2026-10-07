using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using WinDock.Services;

namespace WinDock.Views;

public sealed class PickerEntry(StartApp app)
{
    ImageSource? icon;

    public string Name => app.Name;
    public string Path => app.Path;
    public ImageSource? Icon => icon ??= ShellIcons.ForPath(app.Path);
}

public partial class ProgramPicker : Window
{
    public ProgramPicker()
    {
        InitializeComponent();
        List.ItemsSource = StartMenu.Scan().Select(a => new PickerEntry(a)).ToList();
        Loaded += (_, _) => Search.Focus();
    }

    public List<string> Chosen { get; } = [];

    void OnDrag(object sender, MouseButtonEventArgs e) => DragMove();

    void OnSearch(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var text = Search.Text.Trim();
        var view = CollectionViewSource.GetDefaultView(List.ItemsSource);
        view.Filter = text.Length == 0
            ? null
            : o => ((PickerEntry)o).Name.Contains(text, StringComparison.CurrentCultureIgnoreCase);
    }

    void OnConfirm(object sender, RoutedEventArgs e)
    {
        Chosen.AddRange(List.SelectedItems.Cast<PickerEntry>().Select(p => p.Path));
        if (Chosen.Count > 0) DialogResult = true;
    }

    void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Escolha programas",
            Filter = "Programas e atalhos|*.exe;*.lnk",
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != true) return;
        Chosen.AddRange(dialog.FileNames);
        DialogResult = true;
    }
}
