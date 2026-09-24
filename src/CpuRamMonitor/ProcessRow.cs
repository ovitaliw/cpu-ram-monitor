using System.ComponentModel;
using CpuRamMonitor.Metrics;

namespace CpuRamMonitor;

/// <summary>One line in a heavy-users list. Updated in place so buttons and menus survive refreshes.</summary>
public sealed class ProcessRow : INotifyPropertyChanged
{
    private string _name = "", _label = "", _valueText = "", _tooltip = "";
    private bool _isConfirming;

    public string Name { get => _name; private set => Set(ref _name, value, nameof(Name), nameof(HideLabel)); }
    public string Label { get => _label; private set => Set(ref _label, value, nameof(Label)); }
    public string ValueText { get => _valueText; private set => Set(ref _valueText, value, nameof(ValueText)); }
    public string Tooltip { get => _tooltip; private set => Set(ref _tooltip, value, nameof(Tooltip)); }
    public bool IsConfirming { get => _isConfirming; private set => Set(ref _isConfirming, value, nameof(IsConfirming)); }
    public string HideLabel => $"Ocultar \"{Name}\" da lista";

    public void Update(ProcessGroup group, string valueText, string tooltip, bool isConfirming)
    {
        Name = group.Name;
        Label = group.Count > 1 ? $"{group.Name} ({group.Count})" : group.Name;
        ValueText = valueText;
        Tooltip = tooltip;
        IsConfirming = isConfirming;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, params string[] names)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        foreach (var n in names) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
