using RespawnSwitch.Application.Media;
using RespawnSwitch.Application.Monitoring;
using RespawnSwitch.Application.Windows;
using RespawnSwitch.App.Overlay;
using RespawnSwitch.Core.Clock;
using RespawnSwitch.Core.Respawn;
using RespawnSwitch.Riot.Http;
using RespawnSwitch.Riot.Polling;
using RespawnSwitch.Windows.Media;
using RespawnSwitch.Windows.Windows;
using System.Net.Http;

namespace RespawnSwitch.App;

public sealed class RespawnCoordinator : IAsyncDisposable
{
    private readonly RespawnOverlayWindow overlay; private readonly RespawnStateMachine machine; private readonly ILeagueGameProbe probe;
    private readonly ILeagueWindowController league; private readonly IDouyinWindowController douyin; private readonly TimeProvider time; private AppSettings settings; private readonly Action<string> status;
    private readonly RespawnSwitch.Windows.Identity.IWindowSnapshotSource windowSource; private readonly RespawnSwitch.Windows.Identity.IDouyinProcessIdentityReader identityReader;
    private readonly CancellationTokenSource shutdown = new(); private Task? loop; private GameWindowTarget? game; private IDouyinMediaController? media;
    public RespawnCoordinator(RespawnOverlayWindow overlay, AppSettings settings, Action<string> status)
    {
        this.overlay = overlay; this.settings = settings; this.status = status; time = TimeProvider.System;
        var client = new HttpClient(RiotHttpClientFactory.CreateHandler(new RiotTlsCertificateValidator())) { BaseAddress = RiotEndpoint.Origin };
        var api = new RiotLiveClientApi(client, new RiotRequestTimeouts(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));
        probe = new LeagueGameProbe(api, new RespawnTimerSemantics(TimerSemanticStatus.VerifiedForCurrentPatch, "mvp", 1.0, "mvp-seconds"), new LeaguePollingSchedule(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)), time);
        machine = new RespawnStateMachine(new RespawnStateMachineOptions(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), 2.0, time.TimestampFrequency));
        windowSource = new RespawnSwitch.Windows.Identity.NativeWindowSnapshotSource(); identityReader = new RespawnSwitch.Windows.Identity.DouyinProcessIdentityReader();
        league = new LeagueWindowLocator(windowSource, new RespawnSwitch.Windows.Identity.ToolhelpProcessSnapshot());
        douyin = new MvpDouyinWindowController(windowSource, new DouyinWindowLocator(windowSource, identityReader));
    }
    public void Start() => loop ??= Task.Run(RunAsync);
    public async Task StopAsync() { shutdown.Cancel(); if (loop is not null) try { await loop; } catch (OperationCanceledException) { } overlay.Dispatcher.Invoke(overlay.Hide); }
    public void ShowTestOverlay() => overlay.Dispatcher.Invoke(overlay.Show);
    private async Task RunAsync()
    {
        status("正在监控 League Live Client…");
        await foreach (var observation in probe.WatchAsync(shutdown.Token))
        {
            var transition = observation switch { LeagueSampleObserved x => machine.Apply(new SuccessfulSampleInput(x.Sample)), LeagueProbeFailed x => machine.Apply(new ProbeFailureInput(x.Failure)), _ => throw new InvalidOperationException() };
            foreach (var item in transition.Events) await HandleAsync(item);
        }
    }
    private async Task HandleAsync(RespawnDomainEvent item)
    {
        switch (item)
        {
            case DeathConfirmed x: game = await league.TryFindAsync(x.Sample.TimelineKey, shutdown.Token); if (game is not null) overlay.Dispatcher.Invoke(() => overlay.ShowCountdown(game, (int)Math.Ceiling(Math.Max(0, x.Sample.RespawnTimerSeconds.GetValueOrDefault())))); status(game is null ? "未找到唯一无边框 League 窗口，已安全跳过" : "已检测到死亡"); break;
            case AttachmentRequested x when game is not null:
                overlay.Dispatcher.Invoke(() => overlay.ShowCountdown(game, (int)Math.Ceiling(Math.Max(0, x.Sample.RespawnTimerSeconds.GetValueOrDefault()))));
                var windowClass = await FindWindowClassAsync(TimeSpan.FromSeconds(1));
                if (windowClass is null) { _ = await douyin.AttachAsync(new WindowAttachRequest(x.CycleId, game, game.Bounds, settings.DouyinPath, "", true), shutdown.Token); windowClass = await FindWindowClassAsync(TimeSpan.FromSeconds(5)); }
                if (windowClass is null) { status("未找到唯一抖音窗口，已安全跳过"); break; }
                settings = settings with { DouyinWindowClass = windowClass }; await AppSettingsStore.SaveAsync(settings);
                var attached = await douyin.AttachAsync(new WindowAttachRequest(x.CycleId, game, game.Bounds, settings.DouyinPath, windowClass, false), shutdown.Token);
                if (!attached.PostconditionVerified) { status("抖音窗口未唯一确认，倒计时继续显示"); break; }
                var candidates = await FindMediaAsync(TimeSpan.FromSeconds(4));
                if (candidates is null) { status("未找到唯一抖音媒体会话，倒计时继续显示"); break; }
                settings = settings with { SourceAppUserModelId = candidates.SourceAppUserModelId, DiagnosticFingerprint = candidates.DiagnosticFingerprint }; await AppSettingsStore.SaveAsync(settings);
                media = new GsmtcDouyinMediaController(new GsmtcMediaProfile(candidates.SourceAppUserModelId, candidates.DiagnosticFingerprint));
                status((await media.PlayAsync(shutdown.Token)).StateVerified ? "抖音已启动并显示倒计时" : "抖音播放未确认，倒计时继续显示"); break;
            case DeadSampleUpdated x when game is not null: overlay.Dispatcher.Invoke(() => overlay.ShowCountdown(game, (int)Math.Ceiling(Math.Max(0, x.Sample.RespawnTimerSeconds.GetValueOrDefault())))); break;
            case RespawnConfirmed x:
                if (media is not null) _ = await media.PauseAsync(shutdown.Token); overlay.Dispatcher.Invoke(overlay.Hide); _ = await douyin.RestoreAsync(x.CycleId, shutdown.Token);
                if (game is not null) _ = await league.TryRestoreFocusOnceAsync(game, shutdown.Token); status("已复活，已恢复 League"); break;
        }
    }
    private async Task<string?> FindWindowClassAsync(TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        do
        {
            var candidates = new List<DouyinWindowCandidate>();
            foreach (var w in windowSource.EnumerateTopLevelWindows())
            {
                var process = await identityReader.TryReadAsync(w.Identity.ProcessId, shutdown.Token);
                if (process is not null) candidates.Add(new(process.NormalizedExecutablePath, w.Identity.WindowClass, w.IsVisible, w.IsTopLevel, w.IsToolWindow, w.Identity.Handle));
            }
            var result = DouyinWindowCalibration.SelectUniqueDouyinWindowClass(candidates, settings.DouyinPath);
            if (result is not null) return result;
            await Task.Delay(250, shutdown.Token);
        } while (DateTime.UtcNow < until); return null;
    }
    private async Task<DouyinMediaDiscovery?> FindMediaAsync(TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        do { var matches = await DouyinGsmTcDiscovery.DiscoverAsync(shutdown.Token); if (matches.Count == 1) return matches[0]; await Task.Delay(250, shutdown.Token); } while (DateTime.UtcNow < until); return null;
    }
    public async ValueTask DisposeAsync() { await StopAsync(); shutdown.Dispose(); }
}
