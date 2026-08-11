using System.Diagnostics;
using System.Text.Json;
using RespawnSwitch.Application.Windows;
using RespawnSwitch.Windows.Identity;
using RespawnSwitch.Windows.Windows;

namespace RespawnSwitch.Desktop.IntegrationTests.Windows;

[Collection("Desktop window tests")]
public sealed class WindowLocatorIntegrationTests
{
    [Fact]
    public async Task TryFindAsync_discovers_only_the_RespawnSwitch_owned_test_window()
    {
        var readyFile = Path.Combine(Path.GetTempPath(), $"respawnswitch-host-{Guid.NewGuid():N}.json");
        using var host = StartHost(readyFile);
        try
        {
            var ready = await ReadReadyAsync(readyFile);
            var source = new NativeWindowSnapshotSource();
            var snapshot = source.TryGetWindow(new NativeWindowHandle((nint)ready.Hwnd));
            Assert.NotNull(snapshot);
            Assert.Equal(0, snapshot.Style & 0x00C00000);
            var locator = new LeagueWindowLocator(source, new SingleProcessSnapshot(ready.Pid));

            var target = await locator.TryFindAsync("owned-test-window", CancellationToken.None);

            Assert.NotNull(target);
            Assert.Equal((nint)ready.Hwnd, target.Identity.Handle.Value);
            Assert.True(target.IsBorderless);
        }
        finally
        {
            if (!host.HasExited)
            {
                _ = host.CloseMainWindow();
                if (!host.WaitForExit(3000)) host.Kill(entireProcessTree: true);
            }
            File.Delete(readyFile);
        }
    }

    private static Process StartHost(string readyFile)
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "RespawnSwitch.TestWindowHost.dll");
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
        start.ArgumentList.Add(hostAssembly);
        start.ArgumentList.Add("--mode");
        start.ArgumentList.Add("normal");
        start.ArgumentList.Add("--ready-file");
        start.ArgumentList.Add(readyFile);
        return Process.Start(start) ?? throw new InvalidOperationException("Unable to start TestWindowHost.");
    }

    private static async Task<ReadyWindow> ReadReadyAsync(string readyFile)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (File.Exists(readyFile))
            {
                var result = JsonSerializer.Deserialize<ReadyWindow>(await File.ReadAllTextAsync(readyFile));
                if (result is not null && result.Ready) return result;
            }
            await Task.Delay(50);
        }
        throw new TimeoutException("TestWindowHost did not write a ready record.");
    }

    private sealed record ReadyWindow(int Pid, long Hwnd, bool Ready);
    private sealed class SingleProcessSnapshot(int processId) : IToolhelpProcessSnapshot
    {
        public IReadOnlyList<ToolhelpProcessEntry> EnumerateProcesses() => [new ToolhelpProcessEntry(processId, "League of Legends.exe")];
    }
}

[CollectionDefinition("Desktop window tests", DisableParallelization = true)]
public sealed class DesktopWindowTestCollection;
