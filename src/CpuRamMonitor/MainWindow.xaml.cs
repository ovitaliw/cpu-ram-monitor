using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CpuRamMonitor.Metrics;

namespace CpuRamMonitor;

public partial class MainWindow : Window
{
    private const int TopN = 5;
    private const double HotThreshold = 85;
    private const double ScreenMargin = 16;
    private static readonly TimeSpan ConfirmWindow = TimeSpan.FromSeconds(3);

    private readonly AppSettings _settings;
    private readonly ExclusionList _exclusions;
    private readonly SystemMetrics _system = new();
    private readonly ProcessSampler _processes = new();
    private readonly ObservableCollection<ProcessRow> _cpuRows = [];
    private readonly ObservableCollection<ProcessRow> _ramRows = [];
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(1500) };
    private readonly DispatcherTimer _saveDebounce = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(5) };

    private SystemSnapshot _lastSystem;
    private IReadOnlyList<ProcessGroup> _lastGroups = [];
    private bool _sampling;
    private bool _menuOpen;
    private string? _pendingKill;
    private DateTime _pendingKillExpires;

    private bool IsExpanded => ExpandedView.Visibility == Visibility.Visible;

    public MainWindow(AppSettings settings, ExclusionList exclusions)
    {
        InitializeComponent();
        _settings = settings;
        _exclusions = exclusions;

        CpuList.ItemsSource = _cpuRows;
        RamList.ItemsSource = _ramRows;
        CpuRingSub.Text = $"{Environment.ProcessorCount} threads";

        Opacity = Math.Clamp(settings.Opacity, OpacitySlider.Minimum, 1);
        OpacitySlider.Value = Opacity;
        OpacitySlider.ValueChanged += (_, e) =>
        {
            Opacity = e.NewValue;
            _settings.Opacity = e.NewValue;
            ScheduleSave();
        };
        ShowView(settings.Mode == WidgetMode.Expanded);

        _exclusions.Changed += () => Dispatcher.BeginInvoke(() =>
        {
            RenderLists();
            ShowStatus("Lista de ocultos recarregada");
        });

        _tick.Tick += async (_, _) => await RefreshAsync();
        _saveDebounce.Tick += (_, _) => SaveNow();
        _statusTimer.Tick += (_, _) =>
        {
            _statusTimer.Stop();
            StatusText.Text = "";
        };

        SourceInitialized += (_, _) => PlaceInitially();
        Loaded += async (_, _) =>
        {
            await RefreshAsync();
            _tick.Start();
        };
        Closing += OnClosing;
    }

    // ---------- Sampling & rendering ----------

    private async Task RefreshAsync()
    {
        if (_sampling) return;
        _sampling = true;
        try
        {
            (_lastSystem, _lastGroups) = await Task.Run(() => (_system.Sample(), _processes.Sample(ProcessRules.IsUserProcess)));
            RenderSystem();
            if (IsExpanded && !_menuOpen) RenderLists();
        }
        catch (Exception ex)
        {
            ShowStatus($"Erro ao amostrar: {ex.Message}");
        }
        finally
        {
            _sampling = false;
        }
    }

    private void RenderSystem()
    {
        var s = _lastSystem;
        double usedGb = s.RamUsedBytes / 1073741824.0, totalGb = s.RamTotalBytes / 1073741824.0;

        SetBar(CpuTrack, CpuBar, s.CpuPercent, "CpuBrush");
        SetBar(RamTrack, RamBar, s.RamPercent, "RamBrush");
        CpuCompactText.Text = $"{s.CpuPercent:0}%";
        RamCompactText.Text = $"{s.RamPercent:0}%";
        CompactView.ToolTip = $"CPU {s.CpuPercent:0}%  ·  RAM {s.RamPercent:0}% ({usedGb:0.0} / {totalGb:0} GB)\nDuplo clique para expandir";

        CpuRing.Value = s.CpuPercent;
        CpuRing.Stroke = Brush(s.CpuPercent, "CpuBrush");
        CpuRingText.Text = $"{s.CpuPercent:0}%";
        RamRing.Value = s.RamPercent;
        RamRing.Stroke = Brush(s.RamPercent, "RamBrush");
        RamRingText.Text = $"{s.RamPercent:0}%";
        RamRingSub.Text = $"{usedGb:0.0} / {totalGb:0} GB";
    }

    private void SetBar(Border track, Border bar, double percent, string brushKey)
    {
        double inner = Math.Max(0, track.ActualHeight - track.BorderThickness.Top - track.BorderThickness.Bottom);
        bar.Height = inner * Math.Clamp(percent, 0, 100) / 100;
        bar.Background = Brush(percent, brushKey);
    }

    private Brush Brush(double percent, string normalKey) =>
        (Brush)FindResource(percent >= HotThreshold ? "HotBrush" : normalKey);

    private void RenderLists()
    {
        if (_pendingKill is not null && DateTime.UtcNow > _pendingKillExpires) _pendingKill = null;

        var filter = _exclusions.Filter;
        var visible = _lastGroups.Where(g => !filter.IsExcluded(g.Name)).ToList();
        double totalRam = Math.Max(1, _lastSystem.RamTotalBytes);

        Sync(_cpuRows, visible.OrderByDescending(g => g.CpuPercent).ThenByDescending(g => g.MemoryBytes).Take(TopN),
            g => $"{g.CpuPercent:0.0}%");
        Sync(_ramRows, visible.OrderByDescending(g => g.MemoryBytes).Take(TopN),
            g => $"{100.0 * g.MemoryBytes / totalRam:0.0}%");
    }

    private void Sync(ObservableCollection<ProcessRow> rows, IEnumerable<ProcessGroup> groups, Func<ProcessGroup, string> valueText)
    {
        int i = 0;
        foreach (var g in groups)
        {
            if (i == rows.Count) rows.Add(new ProcessRow());
            var tooltip = $"{g.Name} — {g.Count} processo(s)\nCPU {g.CpuPercent:0.0}%  ·  RAM {FormatBytes(g.MemoryBytes)}\nClique direito para ocultar";
            rows[i].Update(g, valueText(g), tooltip, string.Equals(g.Name, _pendingKill, StringComparison.OrdinalIgnoreCase));
            i++;
        }
        while (rows.Count > i) rows.RemoveAt(rows.Count - 1);
    }

    private static string FormatBytes(long bytes) =>
        bytes >= 1L << 30 ? $"{bytes / 1073741824.0:0.0} GB" : $"{bytes / 1048576.0:0} MB";

    private void ShowStatus(string text)
    {
        StatusText.Text = text;
        StatusText.ToolTip = text;
        _statusTimer.Stop();
        _statusTimer.Start();
    }

    // ---------- Kill / hide ----------

    private async void Kill_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ProcessRow row) return;
        var name = row.Name;

        bool confirmed = string.Equals(_pendingKill, name, StringComparison.OrdinalIgnoreCase) && DateTime.UtcNow <= _pendingKillExpires;
        if (!confirmed)
        {
            _pendingKill = name;
            _pendingKillExpires = DateTime.UtcNow + ConfirmWindow;
            RenderLists();
            return;
        }

        _pendingKill = null;
        RenderLists();
        ShowStatus($"Encerrando {name}…");
        ShowStatus(await Task.Run(() => ProcessKiller.KillByName(name)));
        await RefreshAsync();
    }

    private void HideProcess_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ProcessRow row) return;
        try
        {
            _exclusions.Add(row.Name);
            ShowStatus($"{row.Name} oculto — ⚙ para reexibir");
        }
        catch (Exception ex)
        {
            ShowStatus($"Não foi possível salvar: {ex.Message}");
        }
        RenderLists();
    }

    private void List_ContextMenuOpening(object sender, ContextMenuEventArgs e) => _menuOpen = true;

    private void List_ContextMenuClosing(object sender, ContextMenuEventArgs e)
    {
        _menuOpen = false;
        RenderLists();
    }

    private void EditExclusions_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(_exclusions.FilePath) { UseShellExecute = true });
        }
        catch (Exception)
        {
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{_exclusions.FilePath}\""));
        }
    }

    // ---------- Window behaviour ----------

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            SetMode(!IsExpanded);
            e.Handled = true;
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // Mouse was released before the drag started.
        }
        RememberPosition();
    }

    private void Compact_Click(object sender, RoutedEventArgs e) => SetMode(false);

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ShowView(bool expanded)
    {
        CompactView.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
        ExpandedView.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Switch size while keeping the corner nearest to the screen edge fixed.</summary>
    private void SetMode(bool expanded)
    {
        var old = new Rect(Left, Top, ActualWidth, ActualHeight);
        var work = ScreenUtil.WorkAreaNearest(this, new Point(old.X + old.Width / 2, old.Y + old.Height / 2));
        bool anchorRight = old.X + old.Width / 2 > work.X + work.Width / 2;
        bool anchorBottom = old.Y + old.Height / 2 > work.Y + work.Height / 2;

        ShowView(expanded);
        var target = expanded ? ExpandedView : CompactView;
        target.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = target.DesiredSize;

        double left = anchorRight ? old.Right - size.Width : old.Left;
        double top = anchorBottom ? old.Bottom - size.Height : old.Top;
        Left = Math.Clamp(left, work.Left, Math.Max(work.Left, work.Right - size.Width));
        Top = Math.Clamp(top, work.Top, Math.Max(work.Top, work.Bottom - size.Height));

        _settings.Mode = expanded ? WidgetMode.Expanded : WidgetMode.Compact;
        RememberPosition();
        if (expanded) RenderLists();
        else Dispatcher.BeginInvoke(RenderSystem, DispatcherPriority.Loaded); // bars need the track's laid-out height
    }

    private void PlaceInitially()
    {
        if (_settings.Left is double left && _settings.Top is double top && ScreenUtil.IsOnAnyMonitor(this, new Point(left + 20, top + 20)))
        {
            Left = left;
            Top = top;
            return;
        }

        // Default: top-right corner of the primary screen.
        var work = SystemParameters.WorkArea;
        var target = IsExpanded ? ExpandedView : CompactView;
        target.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Left = work.Right - target.DesiredSize.Width - ScreenMargin;
        Top = work.Top + ScreenMargin;
    }

    private void RememberPosition()
    {
        _settings.Left = Left;
        _settings.Top = Top;
        ScheduleSave();
    }

    private void ScheduleSave()
    {
        _saveDebounce.Stop();
        _saveDebounce.Start();
    }

    private void SaveNow()
    {
        _saveDebounce.Stop();
        SettingsStore.Save(_settings);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _tick.Stop();
        _settings.Left = Left;
        _settings.Top = Top;
        SaveNow();
        _exclusions.Dispose();
    }
}
