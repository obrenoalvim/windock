using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDock.Models;

namespace WinDock.ViewModels;

public sealed partial class PresetViewModel : ObservableObject
{
    readonly Action<PresetViewModel> changed;

    public PresetViewModel(Preset model, int index, Action<PresetViewModel> changed)
    {
        Model = model;
        Index = index;
        this.changed = changed;
        Accent = new SolidColorBrush((Color)ColorConverter.ConvertFromString(model.Accent));
        Accent.Freeze();
        Slots = new ObservableCollection<SlotViewModel>(model.Schedule.Select(Wrap));
        AddSlotCommand = new RelayCommand(AddSlot);
    }

    public Preset Model { get; }
    public int Index { get; }
    public string Id => Model.Id;
    public string Glyph => Model.Glyph;
    public SolidColorBrush Accent { get; }
    public ObservableCollection<SlotViewModel> Slots { get; }
    public ICommand AddSlotCommand { get; }
    public bool HasSnapshot => Model.Snapshot is not null;

    public string Name
    {
        get => Model.Name;
        set
        {
            if (string.IsNullOrWhiteSpace(value)) { OnPropertyChanged(); return; }
            Model.Name = value.Trim();
            OnPropertyChanged();
            changed(this);
        }
    }

    public string Summary
    {
        get
        {
            if (Model.Snapshot is not { } s) return "Sem snapshot. Arrume a taskbar do jeito que quer e clique em Capturar.";
            var names = s.LinkNames.ToList();
            var shown = string.Join(", ", names.Take(5)) + (names.Count > 5 ? $" +{names.Count - 5}" : "");
            return $"Capturado em {s.CapturedAt:dd/MM HH:mm}" + (names.Count > 0 ? $"  ·  {shown}" : "");
        }
    }

    [ObservableProperty] bool isActive;

    public void RefreshSnapshot()
    {
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(HasSnapshot));
    }

    SlotViewModel Wrap(Slot slot) =>
        new(slot, Accent, () => changed(this), RemoveSlot);

    void AddSlot()
    {
        var slot = new Slot { Days = "Mon-Fri", Start = "09:00", End = "18:00" };
        Model.Schedule.Add(slot);
        Slots.Add(Wrap(slot));
        changed(this);
    }

    void RemoveSlot(SlotViewModel slot)
    {
        Model.Schedule.Remove(slot.Model);
        Slots.Remove(slot);
        changed(this);
    }
}
