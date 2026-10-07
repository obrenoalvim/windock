using CommunityToolkit.Mvvm.ComponentModel;
using WinDock.Models;

namespace WinDock.ViewModels;

public sealed partial class PresetViewModel(Preset model, int index) : ObservableObject
{
    public Preset Model { get; } = model;
    public int Index { get; } = index;
    public string Id => Model.Id;
    public string Glyph => Model.Glyph;
    public string Label => Model.Name.ToUpperInvariant();

    [ObservableProperty] bool isActive;
}
