using System.Text.RegularExpressions;

namespace CpuRamMonitor.Metrics;

/// <summary>User-configured names hidden from the lists. Case-insensitive, ".exe" optional, "*" wildcard.</summary>
public sealed class ProcessFilter
{
    private readonly Regex[] _patterns;

    public ProcessFilter(IEnumerable<string?> patterns)
    {
        _patterns = patterns
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => ProcessRules.NormalizeName(p!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(p => new Regex("^" + Regex.Escape(p).Replace(@"\*", ".*") + "$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            .ToArray();
    }

    public bool IsExcluded(string processName)
    {
        var name = ProcessRules.NormalizeName(processName);
        return _patterns.Any(r => r.IsMatch(name));
    }
}
