using System.IO;
using CpuRamMonitor.Metrics;

namespace CpuRamMonitor;

/// <summary>
/// Hand-editable exclusions.txt: one process name per line, "#" comments, "*" wildcard.
/// The app only ever appends to it, so user comments and ordering survive.
/// Edits on disk are picked up live.
/// </summary>
public sealed class ExclusionList : IDisposable
{
    private const string DefaultContent =
        """
        # Processos ocultos das listas do CPU/RAM Monitor.
        # Um nome por linha, sem diferenciar maiúsculas; ".exe" é opcional; "*" funciona como curinga.
        # Salve o arquivo e o widget recarrega sozinho.
        #
        # Exemplos:
        #   Code
        #   docker*

        # Navegador padrão (Opera GX)
        opera

        """;

    private readonly string _path;
    private readonly FileSystemWatcher _watcher;
    private readonly Timer _debounce;

    public ProcessFilter Filter { get; private set; }

    /// <summary>Raised on a thread-pool thread after the file changed on disk.</summary>
    public event Action? Changed;

    public ExclusionList(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path)) File.WriteAllText(path, DefaultContent);
        Filter = new ProcessFilter(ReadLines());

        _debounce = new Timer(_ => Reload());
        _watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        _watcher.Changed += (_, _) => _debounce.Change(250, Timeout.Infinite);
        _watcher.Created += (_, _) => _debounce.Change(250, Timeout.Infinite);
        _watcher.Renamed += (_, _) => _debounce.Change(250, Timeout.Infinite);
        _watcher.EnableRaisingEvents = true;
    }

    public string FilePath => _path;

    public void Add(string processName)
    {
        File.AppendAllText(_path, Environment.NewLine + processName + Environment.NewLine);
        Filter = new ProcessFilter(ReadLines());
    }

    internal static IEnumerable<string> Parse(IEnumerable<string> lines) =>
        lines.Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#'));

    private void Reload()
    {
        Filter = new ProcessFilter(ReadLines());
        Changed?.Invoke();
    }

    private List<string> ReadLines()
    {
        // Editors often hold the file briefly while saving.
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return Parse(File.ReadAllLines(_path)).ToList();
            }
            catch (FileNotFoundException)
            {
                return [];
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(50);
            }
            catch (IOException)
            {
                return [];
            }
        }
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _debounce.Dispose();
    }
}
