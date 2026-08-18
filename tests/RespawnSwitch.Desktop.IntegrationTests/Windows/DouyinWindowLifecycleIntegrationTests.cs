using System.Diagnostics;
using System.Text.Json;
using RespawnSwitch.Application.Windows;
using RespawnSwitch.Core.Respawn;
using RespawnSwitch.Windows.Identity;
using RespawnSwitch.Windows.Windows;

namespace RespawnSwitch.Desktop.IntegrationTests.Windows;

[Collection("Desktop window tests")]
public sealed class DouyinWindowLifecycleIntegrationTests
{
    [Theory]
    [InlineData("normal", false)]
    [InlineData("minimized", true)]
    public async Task Attach_and_restore_are_verified_for_borderless_game_flow(string mode, bool initiallyMinimized)
    {
        var readyFile = Path.Combine(Path.GetTempPath(), $"respawnswitch-douyin-{Guid.NewGuid():N}.json");
        using var host = StartHost(readyFile, mode);
        try
        {
            var ready = await ReadReadyAsync(readyFile);
            var source = new NativeWindowSnapshotSource();
            var handle = new NativeWindowHandle((nint)ready.Hwnd);
            var initial = source.TryGetWindow(handle);
            Assert.NotNull(initial);
            Assert.Equal(initiallyMinimized, initial.IsMinimized);
            var identity = new ProcessIdentity(ready.Pid, DateTimeOffset.UtcNow, "C:\\Apps\\Douyin.exe", "ByteDance", "ABC");
            var controller = new MvpDouyinWindowController(source,
                new DouyinWindowLocator(source, new StubIdentityReader(identity)));
            var cycle = new RespawnCycleId(Guid.NewGuid());
            var game = new GameWindowTarget(new(new(99), 99, "League"), new(0, 0, 1280, 720), "League of Legends.exe", "game", true);

            var attached = await controller.AttachAsync(new(cycle, game, game.Bounds, identity.NormalizedExecutablePath, initial.Identity.WindowClass, false), CancellationToken.None);

            var active = source.TryGetWindow(handle);
            Assert.NotNull(active);
            var sourceBounds = initiallyMinimized ? initial.RestoreBounds : initial.Bounds;
            var desired = DouyinWindowPlacement.PlaceOnRight(game.Bounds, sourceBounds);
            Assert.True(attached.PostconditionVerified, $"{attached.FailureCode}; desired={desired}; actual={active.ExtendedFrameBounds}; ex={active.ExtendedStyle}; minimized={active.IsMinimized}");
            Assert.True(DouyinWindowPostcondition.IsAttached(active, desired));

            var restored = await controller.RestoreAsync(cycle, CancellationToken.None);

            Assert.True(restored.PostconditionVerified, restored.FailureCode);
            await Task.Delay(100);
            var final = source.TryGetWindow(handle);
            Assert.NotNull(final);
            Assert.Equal(initiallyMinimized, final.IsMinimized);
            Assert.Equal(0, final.ExtendedStyle & 0x00000008);
        }
        finally
        {
            if (!host.HasExited) { _ = host.CloseMainWindow(); if (!host.WaitForExit(3000)) host.Kill(entireProcessTree: true); }
            File.Delete(readyFile);
        }
    }

    private static Process StartHost(string readyFile, string mode)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "RespawnSwitch.TestWindowHost.dll"));
        start.ArgumentList.Add("--mode"); start.ArgumentList.Add(mode);
        start.ArgumentList.Add("--ready-file"); start.ArgumentList.Add(readyFile);
        return Process.Start(start) ?? throw new InvalidOperationException("Unable to start TestWindowHost.");
    }

    private static async Task<ReadyWindow> ReadReadyAsync(string path)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (File.Exists(path))
            {
                var value = JsonSerializer.Deserialize<ReadyWindow>(await File.ReadAllTextAsync(path));
                if (value is { Ready: true }) return value;
            }
            await Task.Delay(50);
        }
        throw new TimeoutException("TestWindowHost did not become ready.");
    }

    private sealed record ReadyWindow(int Pid, long Hwnd, bool Ready);
    private sealed class StubIdentityReader(ProcessIdentity identity) : IDouyinProcessIdentityReader
    {
        public ValueTask<ProcessIdentity?> TryReadAsync(int processId, CancellationToken cancellationToken) =>
            ValueTask.FromResult<ProcessIdentity?>(processId == identity.ProcessId ? identity : null);
    }
}
