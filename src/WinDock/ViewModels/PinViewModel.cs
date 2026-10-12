using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using WinDock.Models;
using WinDock.Services;

namespace WinDock.ViewModels;

public sealed partial class PinViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public PinViewModel(PinItem model, Action<PinViewModel> remove, Action<PinViewModel, int> move)
    {
        Model = model;
        Icon = ShellIcons.ForPin(model);
        Initial = model.Name.Length > 0 ? model.Name[..1].ToUpperInvariant() : "?";
        RemoveCommand = new RelayCommand(() => remove(this));
        MoveLeftCommand = new RelayCommand(() => move(this, -1));
        MoveRightCommand = new RelayCommand(() => move(this, 1));
    }

    public void RefreshText() => OnPropertyChanged(nameof(Tip));

    public PinItem Model { get; }
    public string Name => Model.Name;
    public ImageSource? Icon { get; }
    public bool HasIcon => Icon is not null;
    public string Initial { get; }
    public double Fade => Model.Exists ? 1 : 0.35;
    public string Tip => Model.Exists ? Model.Name : Loc.F("notFound", Model.Name);
    public ICommand RemoveCommand { get; }
    public ICommand MoveLeftCommand { get; }
    public ICommand MoveRightCommand { get; }
}
