using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDock.Models;

namespace WinDock.ViewModels;

public sealed partial class DayFlag(string label, DayOfWeek day, bool isOn, Action changed) : ObservableObject
{
    public string Label { get; } = label;
    public DayOfWeek Day { get; } = day;

    [ObservableProperty] bool isOn = isOn;

    partial void OnIsOnChanged(bool value) => changed();
}

public sealed class SlotViewModel : ObservableObject
{
    static readonly (string Label, DayOfWeek Day)[] Week =
    [
        ("S", DayOfWeek.Monday), ("T", DayOfWeek.Tuesday), ("Q", DayOfWeek.Wednesday), ("Q", DayOfWeek.Thursday),
        ("S", DayOfWeek.Friday), ("S", DayOfWeek.Saturday), ("D", DayOfWeek.Sunday)
    ];

    readonly Slot model;
    readonly Action changed;

    public SlotViewModel(Slot model, SolidColorBrush accent, Action changed, Action<SlotViewModel> remove)
    {
        this.model = model;
        this.changed = changed;
        Accent = accent;
        var on = DaySet.Parse(model.Days);
        Days = new ObservableCollection<DayFlag>(Week.Select(w => new DayFlag(w.Label, w.Day, on.Contains(w.Day), Rewrite)));
        RemoveCommand = new RelayCommand(() => remove(this));
    }

    public SolidColorBrush Accent { get; }
    public ObservableCollection<DayFlag> Days { get; }
    public ICommand RemoveCommand { get; }
    public Slot Model => model;

    public string Start
    {
        get => model.Start;
        set { Assign(value, v => model.Start = v); OnPropertyChanged(); }
    }

    public string End
    {
        get => model.End;
        set { Assign(value, v => model.End = v); OnPropertyChanged(); }
    }

    void Assign(string text, Action<string> set)
    {
        if (!Slot.TryNormalize(text, out var normalized)) return;
        set(normalized);
        changed();
    }

    void Rewrite()
    {
        model.Days = DaySet.Format(Days.Where(d => d.IsOn).Select(d => d.Day));
        changed();
    }
}
