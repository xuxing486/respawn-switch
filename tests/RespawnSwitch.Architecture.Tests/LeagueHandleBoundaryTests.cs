namespace RespawnSwitch.Architecture.Tests;

public sealed class LeagueHandleBoundaryTests
{
    [Fact]
    public void League_locator_source_does_not_contain_forbidden_process_handle_apis()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "RespawnSwitch.Windows", "Windows", "LeagueWindowLocator.cs"));

        foreach (var forbidden in new[] { "OpenProcess", "Process.GetProcessById", "MainModule", "QueryFullProcessImageName", "ManagementObjectSearcher", "PROCESS_" })
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RespawnSwitch.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("RespawnSwitch repository root was not found.");
    }
}
