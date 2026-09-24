using CpuRamMonitor.Metrics;

namespace CpuRamMonitor.Tests;

public class ProcessFilterTests
{
    [Theory]
    [InlineData("opera", "opera", true)]
    [InlineData("opera", "Opera", true)]
    [InlineData("OPERA.exe", "opera", true)]
    [InlineData("opera", "opera.exe", true)]
    [InlineData("opera", "opera_crashreporter", false)]
    [InlineData("docker*", "Docker Desktop", true)]
    [InlineData("docker*", "com.docker.backend", false)]
    [InlineData("*node*", "node", true)]
    [InlineData("a.b", "aXb", false)] // regex metacharacters are literal
    public void Matches_case_insensitively_with_wildcards(string pattern, string process, bool excluded)
    {
        Assert.Equal(excluded, new ProcessFilter([pattern]).IsExcluded(process));
    }

    [Fact]
    public void Blank_entries_exclude_nothing()
    {
        var filter = new ProcessFilter(["", "  ", null]);
        Assert.False(filter.IsExcluded("node"));
    }

    [Fact]
    public void Exclusion_file_skips_comments_and_blank_lines()
    {
        var lines = new[] { "# comment", "", "  opera  ", "   # indented comment", "Code" };
        Assert.Equal(["opera", "Code"], ExclusionList.Parse(lines));
    }
}
