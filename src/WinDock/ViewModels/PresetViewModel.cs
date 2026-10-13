using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDock.Models;
using WinDock.Services;

namespace WinDock.ViewModels;

public sealed partial class WidgetChoice(string kind, bool isOn, Action<WidgetChoice> toggled) : ObservableObject
{
    public string Kind { get; } = kind;
    public string Label => Loc.T("widget_" + Kind);

    [ObservableProperty] bool isOn = isOn;

    partial void OnIsOnChanged(bool value) => toggled(this);

    public void RefreshText() => OnPropertyChanged(nameof(Label));
}

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
        Pins = new ObservableCollection<PinViewModel>(model.Items.Select(WrapPin));
        var shown = model.ShownWidgets.Select(w => w.Kind).ToHashSet();
        Widgets = new ObservableCollection<WidgetChoice>(WidgetSpec.Kinds.Select(k => new WidgetChoice(k, shown.Contains(k), ToggleWidget)));
        AddSlotCommand = new RelayCommand(AddSlot);
    }

    public Preset Model { get; }
    public int Index { get; }
    public string Id => Model.Id;
    public string Glyph => Model.Glyph;
    public SolidColorBrush Accent { get; }
    public ObservableCollection<SlotViewModel> Slots { get; }
    public ObservableCollection<PinViewModel> Pins { get; }
    public ObservableCollection<WidgetChoice> Widgets { get; }
    public ICommand AddSlotCommand { get; }
    public bool HasItems => Model.Items.Count > 0;
    public string Count => Model.Items.Count == 1 ? Loc.T("countOne") : Loc.F("countMany", Model.Items.Count);

    WidgetSpec? CountdownSpec => Model.Widgets?.FirstOrDefault(w => w.Kind == WidgetSpec.Countdown);

    public bool HasCountdown => CountdownSpec is not null;

    public string CountdownDate
    {
        get => CountdownSpec?.Date ?? "";
        set
        {
            if (CountdownSpec is not { } spec) return;
            spec.Date = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            OnPropertyChanged();
            changed(this);
        }
    }

    public string CountdownLabel
    {
        get => CountdownSpec?.Label ?? "";
        set
        {
            if (CountdownSpec is not { } spec) return;
            spec.Label = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            OnPropertyChanged();
            changed(this);
        }
    }

    WidgetSpec? WeatherSpec => Model.Widgets?.FirstOrDefault(w => w.Kind == WidgetSpec.Weather);

    public bool HasWeather => WeatherSpec is not null;

    public string WeatherCity
    {
        get => WeatherSpec?.Label ?? "";
        set
        {
            if (WeatherSpec is not { } spec) return;
            spec.Label = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            OnPropertyChanged();
            changed(this);
        }
    }

    void ToggleWidget(WidgetChoice choice)
    {
        var current = Model.ShownWidgets.ToDictionary(w => w.Kind);
        Model.Widgets = Widgets.Where(w => w.IsOn)
            .Select(w => current.TryGetValue(w.Kind, out var spec) ? spec : new WidgetSpec { Kind = w.Kind })
            .ToList();
        OnPropertyChanged(nameof(HasCountdown));
        OnPropertyChanged(nameof(CountdownDate));
        OnPropertyChanged(nameof(CountdownLabel));
        OnPropertyChanged(nameof(HasWeather));
        OnPropertyChanged(nameof(WeatherCity));
        changed(this);
    }

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

    [ObservableProperty] bool isActive;

    public void AddItems(IEnumerable<PinItem> items)
    {
        foreach (var item in items)
        {
            Model.Items.Add(item);
            Pins.Add(WrapPin(item));
        }
        ItemsChanged();
    }

    public void ReplaceItems(IEnumerable<PinItem> items)
    {
        Model.Items.Clear();
        Pins.Clear();
        AddItems(items);
    }

    public void RefreshText()
    {
        OnPropertyChanged(nameof(Count));
        foreach (var pin in Pins) pin.RefreshText();
        foreach (var slot in Slots) slot.RefreshText();
        foreach (var widget in Widgets) widget.RefreshText();
    }

    public void RemoveDuplicates()
    {
        var seen = new HashSet<string>();
        var duplicates = Pins.Where(p => !PinBuilder.TryAdd(seen, p.Model)).ToList();
        foreach (var d in duplicates)
        {
            Model.Items.Remove(d.Model);
            Pins.Remove(d);
        }
        if (duplicates.Count > 0) ItemsChanged();
    }

    PinViewModel WrapPin(PinItem item) => new(item, RemovePin, MovePin);

    void RemovePin(PinViewModel pin)
    {
        Model.Items.Remove(pin.Model);
        Pins.Remove(pin);
        ItemsChanged();
    }

    void MovePin(PinViewModel pin, int delta)
    {
        var from = Pins.IndexOf(pin);
        var to = from + delta;
        if (from < 0 || to < 0 || to >= Pins.Count) return;
        Pins.Move(from, to);
        Model.Items.RemoveAt(from);
        Model.Items.Insert(to, pin.Model);
        ItemsChanged();
    }

    void ItemsChanged()
    {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(Count));
        changed(this);
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
