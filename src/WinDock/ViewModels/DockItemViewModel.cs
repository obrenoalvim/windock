using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDock.Models;
using WinDock.Services;

namespace WinDock.ViewModels;

public sealed partial class DockItemViewModel : ObservableObject
{
    public DockItemViewModel(DockItem model, ImageSource? icon, Action launch, Action remove)
    {
        Name = model.Name;
        Icon = icon;
        Initial = model.Name.Length > 0 ? model.Name[..1].ToUpperInvariant() : "?";
        ProcessName = model.Process ?? ExeName(model.Target);
        LaunchCommand = new RelayCommand(launch);
        RemoveCommand = new RelayCommand(remove);
    }

    public string Name { get; }
    public ImageSource? Icon { get; }
    public bool HasIcon => Icon is not null;
    public string Initial { get; }
    public string? ProcessName { get; }
    public ICommand LaunchCommand { get; }
    public ICommand RemoveCommand { get; }

    [ObservableProperty] bool isRunning;

    static string? ExeName(string target)
    {
        var path = Target.ResolvePath(target);
        return path is not null && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(path)
            : null;
    }
}
