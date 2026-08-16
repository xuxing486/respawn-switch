using System.ComponentModel;
using System.Diagnostics;
using RespawnSwitch.Application.Douyin;

namespace RespawnSwitch.Windows.DouyinDiscovery;

internal interface IExternalProcessLauncher
{
    bool Start(string target, bool useShellExecute);
}

internal sealed class ExternalProcessLauncher : IExternalProcessLauncher
{
    public bool Start(string target, bool useShellExecute) =>
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = useShellExecute }) is not null;
}

public sealed class DouyinWebFallbackLauncher : IDouyinWebFallbackLauncher
{
    private const string OfficialWebUrl = "https://www.douyin.com/";
    private readonly IExternalProcessLauncher processLauncher;

    public DouyinWebFallbackLauncher()
        : this(new ExternalProcessLauncher())
    {
    }

    internal DouyinWebFallbackLauncher(IExternalProcessLauncher processLauncher)
    {
        this.processLauncher = processLauncher;
    }

    public Task<bool> OpenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return Task.FromResult(processLauncher.Start(OfficialWebUrl, useShellExecute: true));
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return Task.FromResult(false);
        }
    }
}
