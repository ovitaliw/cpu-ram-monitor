using System.Diagnostics;
using System.Windows;

namespace CpuRamMonitor;

public partial class App : Application
{
    private Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstance = new Mutex(true, @"Local\CpuRamMonitor.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // Stay responsive exactly when the machine is choking.
        try
        {
            using var self = Process.GetCurrentProcess();
            self.PriorityClass = ProcessPriorityClass.AboveNormal;
        }
        catch (Exception)
        {
            // Not critical.
        }

        MainWindow = new MainWindow(SettingsStore.Load(), new ExclusionList(AppPaths.Exclusions));
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
