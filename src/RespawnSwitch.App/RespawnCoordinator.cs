using RespawnSwitch.Application.Douyin;
using RespawnSwitch.Application.Media;
using RespawnSwitch.Application.Monitoring;
using RespawnSwitch.Application.Windows;
using RespawnSwitch.App.Overlay;
using RespawnSwitch.Core.Clock;
using RespawnSwitch.Core.Respawn;
using RespawnSwitch.Riot.Http;
using RespawnSwitch.Riot.Polling;
using RespawnSwitch.Windows.Media;
using RespawnSwitch.Windows.DouyinDiscovery;
using RespawnSwitch.Windows.Windows;
using System.Net.Http;
using System.Collections.Concurrent;
using RespawnSwitch.Core.Game;

namespace RespawnSwitch.App;

public sealed class RespawnCoordinator : IAsyncDisposable
{
    private readonly RespawnOverlayWindow overlay; private readonly RespawnStateMachine machine; private readonly ILeagueGameProbe probe;
    private readonly ILeagueWindowController league; private readonly IDouyinWindowController douyin; private readonly TimeProvider time; private AppSettings settings; private readonly Action<string> status;
    private readonly DouyinDiscoveryController discovery; private readonly IDouyinWebFallbackLauncher webFallback; private readonly WebFallbackCycleGuard webGuard = new();
    private readonly ConcurrentDictionary<RespawnCycleId, byte> desktopCycles = new();
    private readonly RespawnCycleRunner cycleRunner = new();
    private readonly RespawnSwitch.Windows.Identity.IWindowSnapshotSource windowSource; private readonly RespawnSwitch.Windows.Identity.IDouyinProcessIdentityReader identityReader;
    private readonly CancellationTokenSource shutdown = new(); private Task? loop; private GameWindowTarget? game; private IDouyinMediaController? media;
    public RespawnCoordinator(
        RespawnOverlayWindow overlay,
        AppSettings settings,
        Action<string> status,
        DouyinDiscoveryController discovery,
        IDouyinWebFallbackLauncher? webFallback = null)
    {
        this.overlay = overlay; this.settings = settings; this.status = status; this.discovery = discovery;
        this.webFallback = webFallback ?? new DouyinWebFallbackLauncher(); time = TimeProvider.System;
        var client = new HttpClient(RiotHttpClientFactory.CreateHandler(new RiotTlsCertificateValidator())) { BaseAddress = RiotEndpoint.Origin };
        var api = new RiotLiveClientApi(client, new RiotRequestTimeouts(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));
        probe = new LeagueGameProbe(api, new RespawnTimerSemantics(TimerSemanticStatus.VerifiedForCurrentPatch, "mvp", 1.0, "mvp-seconds"), new LeaguePollingSchedule(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)), time);
        machine = new RespawnStateMachine(new RespawnStateMachineOptions(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), 2.0, time.TimestampFrequency));
        windowSource = new RespawnSwitch.Windows.Identity.NativeWindowSnapshotSource(); identityReader = new RespawnSwitch.Windows.Identity.DouyinProcessIdentityReader();
        league = new LeagueWindowLocator(windowSource, new RespawnSwitch.Windows.Identity.ToolhelpProcessSnapshot());
        douyin = new MvpDouyinWindowController(windowSource, new DouyinWindowLocator(windowSource, identityReader));
    }
    public void Start() => loop ??= Task.Run(RunAsync);
    public void UpdateSettings(AppSettings value) => settings = value;
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
            case DeathConfirmed x:
                game = await league.TryFindAsync(x.Sample.TimelineKey, shutdown.Token);
                if (game is not null)
                {
                    var seconds = ValidSeconds(x.Sample.RespawnTimerSeconds);
                    _ = overlay.Dispatcher.BeginInvoke(() => overlay.BeginCycle(game, x.Sample, RespawnSwitch.Core.Clock.LocalRespawnCountdown.Create(time, seconds)));
                }
                status(game is null ? "League 窗口未连接：请使用无边框模式" : $"已检测到死亡 · {x.Sample.ChampionName} · {x.Sample.Kills}/{x.Sample.Deaths}/{x.Sample.Assists}");
                break;
            case AttachmentRequested x when game is not null:
                var capturedGame = game;
                cycleRunner.Start(x.CycleId, token => AttachDouyinAsync(x, capturedGame, token));
                break;
            case DeadSampleUpdated:
                break;
            case RespawnConfirmed x:
                _ = overlay.Dispatcher.BeginInvoke(overlay.EndCycle);
                webGuard.Complete(x.CycleId);
                var capturedLeague = game;
                _ = CompleteRespawnAsync(x.CycleId, capturedLeague);
                status("已确认复活 · 正在暂停抖音并返回 League");
                break;
        }
    }

    private async Task AttachDouyinAsync(AttachmentRequested x, GameWindowTarget capturedGame, CancellationToken token)
    {
        try
        {
            var plan = RespawnDouyinActionPlanner.Plan(
                    discovery.CurrentResult,
                    new DouyinRuntimePreferences(settings.DiscoveryMode, settings.OpenWebFallback));
            if (plan.Mode == DouyinLaunchMode.Web)
            {
                if (webGuard.TryBegin(x.CycleId))
                {
                    status(await webFallback.OpenAsync(token)
                        ? "抖音网页版已打开 · 浏览器扩展连接待确认"
                        : "抖音网页问题：无法打开网页");
                }
                return;
            }

            if (plan.Mode == DouyinLaunchMode.Unavailable || plan.DesktopCandidate is null)
            {
                status("抖音问题：请提前打开桌面客户端或抖音网页");
                return;
            }

            var douyinPath = plan.DesktopCandidate.NormalizedPath;
                settings = settings with
                {
                    PreferredDouyinPath = douyinPath,
                    LastValidatedSignatureThumbprint = plan.DesktopCandidate.SignatureThumbprint
                };
                await AppSettingsStore.SaveAsync(settings);
                var windowClass = await FindWindowClassAsync(douyinPath, TimeSpan.FromSeconds(2), token);
                if (windowClass is null) { status("抖音桌面问题：请提前打开唯一的抖音主窗口"); return; }
                settings = settings with { DouyinWindowClass = windowClass }; await AppSettingsStore.SaveAsync(settings);
                var attached = await douyin.AttachAsync(new WindowAttachRequest(x.CycleId, capturedGame, capturedGame.Bounds, douyinPath, windowClass, false), token);
                if (!attached.PostconditionVerified) { status($"抖音窗口问题：{attached.FailureCode}"); return; }
                desktopCycles[x.CycleId] = 0;
                var candidates = await FindMediaAsync(TimeSpan.FromSeconds(4), token);
                if (candidates is null) { status("抖音媒体问题：请先打开并播放一次视频"); return; }
                settings = settings with { SourceAppUserModelId = candidates.SourceAppUserModelId, DiagnosticFingerprint = candidates.DiagnosticFingerprint }; await AppSettingsStore.SaveAsync(settings);
                media = new GsmtcDouyinMediaController(new GsmtcMediaProfile(candidates.SourceAppUserModelId, candidates.DiagnosticFingerprint));
                var result = await media.PlayAsync(token);
                status(result.StateVerified ? "抖音已连接 · 正在播放" : $"抖音媒体问题：{result.FailureCode}");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { status($"抖音连接问题：{ex.GetType().Name}"); }
    }

    private async Task CompleteRespawnAsync(RespawnCycleId cycleId, GameWindowTarget? capturedLeague)
    {
        try
        {
            await cycleRunner.CancelAsync(cycleId, TimeSpan.FromMilliseconds(750));
            if (desktopCycles.TryRemove(cycleId, out _))
            {
                if (media is not null) _ = await media.PauseAsync(shutdown.Token);
                _ = await douyin.RestoreAsync(cycleId, shutdown.Token);
            }
            if (capturedLeague is not null) _ = await league.TryRestoreFocusOnceAsync(capturedLeague, shutdown.Token);
            status("已复活 · League 已恢复");
        }
        catch (OperationCanceledException) { }
    }

    private static double ValidSeconds(double? value) => value is { } seconds && double.IsFinite(seconds) && seconds >= 0 ? seconds : 0;

    private async Task<string?> FindWindowClassAsync(string douyinPath, TimeSpan timeout, CancellationToken token)
    {
        var until = DateTime.UtcNow + timeout;
        do
        {
            var candidates = new List<DouyinWindowCandidate>();
            foreach (var w in windowSource.EnumerateTopLevelWindows())
            {
                var process = await identityReader.TryReadAsync(w.Identity.ProcessId, token);
                if (process is not null) candidates.Add(new(process.NormalizedExecutablePath, w.Identity.WindowClass, w.IsVisible, w.IsTopLevel, w.IsToolWindow, w.Identity.Handle));
            }
            var result = DouyinWindowCalibration.SelectUniqueDouyinWindowClass(candidates, douyinPath);
            if (result is not null) return result;
            await Task.Delay(250, token);
        } while (DateTime.UtcNow < until); return null;
    }
    private async Task<DouyinMediaDiscovery?> FindMediaAsync(TimeSpan timeout, CancellationToken token)
    {
        var until = DateTime.UtcNow + timeout;
        do { var matches = await DouyinGsmTcDiscovery.DiscoverAsync(token); if (matches.Count == 1) return matches[0]; await Task.Delay(250, token); } while (DateTime.UtcNow < until); return null;
    }
    public async ValueTask DisposeAsync() { await StopAsync(); await cycleRunner.DisposeAsync(); shutdown.Dispose(); }
}
