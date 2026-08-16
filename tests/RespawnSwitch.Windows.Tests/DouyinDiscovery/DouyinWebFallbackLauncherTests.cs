using RespawnSwitch.Windows.DouyinDiscovery;

namespace RespawnSwitch.Windows.Tests.DouyinDiscovery;

public sealed class DouyinWebFallbackLauncherTests
{
    [Fact]
    public async Task OpenAsync_AlwaysUsesFixedOfficialHttpsUrl()
    {
        var process = new RecordingProcessLauncher();
        var launcher = new DouyinWebFallbackLauncher(process);

        var opened = await launcher.OpenAsync(CancellationToken.None);

        Assert.True(opened);
        Assert.Equal("https://www.douyin.com/", process.Target);
        Assert.True(process.UseShellExecute);
    }

    private sealed class RecordingProcessLauncher : IExternalProcessLauncher
    {
        public string? Target { get; private set; }
        public bool UseShellExecute { get; private set; }

        public bool Start(string target, bool useShellExecute)
        {
            Target = target;
            UseShellExecute = useShellExecute;
            return true;
        }
    }
}
