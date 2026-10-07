using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using WinDock.Models;
using WinDock.Services;

namespace WinDock.ViewModels;

public sealed class PinViewModel
{
    public PinViewModel(PinItem model, Action<PinViewModel> remove, Action<PinViewModel, int> move)
    {
        Model = model;
        Icon = model.IsRaw
            ? (model.AppId is { Length: > 0 } id ? ShellIcons.ForAppId(id) : null)
            : model.ResolvedTarget is { } path ? ShellIcons.ForPath(path) : null;
        Initial = model.Name.Length > 0 ? model.Name[..1].ToUpperInvariant() : "?";
        RemoveCommand = new RelayCommand(() => remove(this));
        MoveLeftCommand = new RelayCommand(() => move(this, -1));
        MoveRightCommand = new RelayCommand(() => move(this, 1));
    }

    public PinItem Model { get; }
    public string Name => Model.Name;
    public ImageSource? Icon { get; }
    public bool HasIcon => Icon is not null;
    public string Initial { get; }
    public double Fade => Model.Exists ? 1 : 0.35;
    public string Tip => Model.Exists ? Model.Name : $"{Model.Name} (não encontrado)";
    public ICommand RemoveCommand { get; }
    public ICommand MoveLeftCommand { get; }
    public ICommand MoveRightCommand { get; }
}
