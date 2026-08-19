// Author: Stress Monster
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
using RespawnSwitch.App.Browser;
using RespawnSwitch.Application.Audio;
using RespawnSwitch.Windows.Audio;
using RespawnSwitch.App.Cycles;

namespace RespawnSwitch.App;

public sealed class RespawnCoordinator : IAsyncDisposable
{
    private readonly RespawnOverlayWindow overlay; private readonly RespawnStateMachine machine; private readonly ILeagueGameProbe probe;
    private readonly ILeagueWindowController league; private readonly IDouyinWindowController douyin; private readonly TimeProvider time; private AppSettings settings; private readonly Action<string> status;
    private readonly DouyinDiscoveryController discovery; private readonly IDouyinWebFallbackLauncher webFallback; private readonly WebFallbackCycleGuard webGuard = new();
    private readonly ConcurrentDictionary<RespawnCycleId, RespawnCycleRuntime> cycles = new();
    private readonly BrowserBridgeState? browserState;
    private readonly RespawnAudioMuteCoordinator leagueAudio = new(new WindowsProcessAudioMuteController());
    private readonly RespawnSwitch.Windows.Identity.IWindowSnapshotSource windowSource; private readonly RespawnSwitch.Windows.Identity.IDouyinProcessIdentityReader identityReader;
    private readonly CancellationTokenSource shutdown = new(); private Task? loop; private GameWindowTarget? game;
    public RespawnCoordinator(
        RespawnOverlayWindow overlay,
        AppSettings settings,
        Action<string> status,
        DouyinDiscoveryController discovery,
        IDouyinWebFallbackLauncher? webFallback = null,
        BrowserBridgeState? browserState = null)
    {
        this.overlay = overlay; this.settings = settings; this.status = status; this.discovery = discovery;
        this.webFallback = webFallback ?? new DouyinWebFallbackLauncher(); this.browserState = browserState; time = TimeProvider.System;
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
    public async Task StopAsync() { shutdown.Cancel(); if (loop is not null) try { await loop; } catch (OperationCanceledException) { } await leagueAudio.DisposeAsync(); overlay.Dispatcher.Invoke(overlay.Hide); }
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
            case LifeStateSynchronized x:
                status(x.State == LifeState.Alive
                    ? "League 对局数据已连接 · 当前存活"
                    : "League 对局数据已连接 · 状态已同步");
                break;
            case ConnectionBecameStale:
                _ = overlay.Dispatcher.BeginInvoke(overlay.MarkConnectionUnstable);
                status("League 数据连接问题 · 对局接口暂时无响应");
                break;
            case ConnectionRestored:
                status(machine.State.LifeState == LifeState.Dead
                    ? "League 对局数据已恢复 · 当前阵亡"
                    : "League 对局数据已恢复 · 当前存活");
                break;
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
                var runtime = new RespawnCycleRuntime(x.CycleId, shutdown.Token);
                if (cycles.TryAdd(x.CycleId, runtime))
                    runtime.StartEnter((cycle, token) => AttachDouyinAsync(x, capturedGame, cycle, token));
                else
                    await runtime.DisposeAsync();
                break;
            case DeadSampleUpdated:
                break;
            case RespawnConfirmed x:
                _ = overlay.Dispatcher.BeginInvoke(overlay.EndCycle);
                webGuard.Complete(x.CycleId);
                var capturedLeague = game;
                status("已确认复活 · 正在暂停抖音并返回 League");
                await CompleteRespawnAsync(x.CycleId, capturedLeague);
                break;
        }
    }

    private async Task AttachDouyinAsync(AttachmentRequested x, GameWindowTarget capturedGame, RespawnCycleRuntime runtime, CancellationToken token)
    {
        var switchedToDouyin = false;
        try
        {
            await leagueAudio.BeginAsync(x.CycleId, capturedGame.Identity.ProcessId, token);
            var plan = RespawnDouyinActionPlanner.Plan(
                    discovery.CurrentResult,
                    new DouyinRuntimePreferences(settings.DiscoveryMode, settings.OpenWebFallback));
            if (plan.Mode == DouyinLaunchMode.Web)
            {
                if (!webGuard.TryBegin(x.CycleId)) return;
                if (browserState is null) { status("抖音网页问题：本地浏览器桥接未运行"); return; }
                if (!runtime.TryReserve(c => c.WebCommandIssued = true)) return;
                var webResult = await browserState.IssueAsync(x.CycleId.Value, "play", TimeSpan.FromSeconds(3), token);
                if (webResult.Ok && runtime.TryCommit(_ => { })) { switchedToDouyin = true; status($"抖音网页已连接 · {webResult.Browser} 正在播放"); }
                else status($"抖音网页问题：{webResult.ErrorCode}");
                return;
            }

            if (plan.Mode == DouyinLaunchMode.Unavailable || plan.DesktopCandidate is null)
            {
                status("抖音问题：请提前打开桌面客户端或抖音网页");
                return;
            }

            var douyinPath = plan.DesktopCandidate.NormalizedPath;
            var windowClass = settings.DouyinWindowClass;
            var usedCachedWindowClass = !string.IsNullOrWhiteSpace(windowClass);
            if (string.IsNullOrWhiteSpace(windowClass))
            {
                windowClass = await FindWindowClassAsync(douyinPath, TimeSpan.FromSeconds(1), token);
                if (windowClass is null) { status("抖音桌面问题：请提前打开唯一的抖音主窗口"); return; }
            }

            IDouyinMediaController? cycleMedia = null;
            Task<MediaCommandResult>? playTask = null;
            if (!string.IsNullOrWhiteSpace(settings.SourceAppUserModelId) && !string.IsNullOrWhiteSpace(settings.DiagnosticFingerprint))
            {
                cycleMedia = new GsmtcDouyinMediaController(new GsmtcMediaProfile(settings.SourceAppUserModelId, settings.DiagnosticFingerprint));
                if (!runtime.TryReserve(c => c.Media = cycleMedia)) return;
                playTask = cycleMedia.PlayAsync(token).AsTask();
            }

            var attached = await douyin.AttachAsync(new WindowAttachRequest(x.CycleId, capturedGame, capturedGame.Bounds, douyinPath, windowClass, false), token);
            if (!attached.PostconditionVerified && usedCachedWindowClass)
            {
                var recalibrated = await FindWindowClassAsync(douyinPath, TimeSpan.FromSeconds(1), token);
                if (!string.IsNullOrWhiteSpace(recalibrated))
                {
                    windowClass = recalibrated;
                    attached = await douyin.AttachAsync(new WindowAttachRequest(x.CycleId, capturedGame, capturedGame.Bounds, douyinPath, windowClass, false), token);
                }
            }
            if (!attached.PostconditionVerified)
            {
                if (cycleMedia is not null) _ = await cycleMedia.PauseAsync(token);
                status($"抖音窗口问题：{attached.FailureCode}");
                return;
            }
            if (!runtime.TryReserve(c => c.DesktopAttached = true))
            {
                _ = await douyin.RestoreAsync(x.CycleId, CancellationToken.None);
                return;
            }

            MediaCommandResult result;
            DouyinMediaDiscovery? discovered = null;
            if (playTask is not null)
            {
                result = await playTask;
            }
            else
            {
                discovered = await FindMediaAsync(TimeSpan.FromSeconds(1), token);
                if (discovered is null) { status("抖音媒体问题：请先打开并播放一次视频"); return; }
                cycleMedia = new GsmtcDouyinMediaController(new GsmtcMediaProfile(discovered.SourceAppUserModelId, discovered.DiagnosticFingerprint));
                if (!runtime.TryReserve(c => c.Media = cycleMedia)) return;
                result = await cycleMedia.PlayAsync(token);
            }
            if (!result.StateVerified || !runtime.TryCommit(_ => { }))
            {
                status($"抖音媒体问题：{result.FailureCode}");
                return;
            }
            switchedToDouyin = true;
            settings = settings with
            {
                PreferredDouyinPath = douyinPath,
                LastValidatedSignatureThumbprint = plan.DesktopCandidate.SignatureThumbprint,
                DouyinWindowClass = windowClass,
                SourceAppUserModelId = discovered?.SourceAppUserModelId ?? settings.SourceAppUserModelId,
                DiagnosticFingerprint = discovered?.DiagnosticFingerprint ?? settings.DiagnosticFingerprint
            };
            await AppSettingsStore.SaveAsync(settings);
            status("抖音已连接 · 已置顶并播放");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { status($"抖音连接问题：{ex.GetType().Name}"); }
        finally
        {
            if (!switchedToDouyin && runtime.Stage is not RespawnCycleStage.ReturningToLeague and not RespawnCycleStage.Completed)
                await leagueAudio.CompleteAsync(x.CycleId);
        }
    }

    private async Task CompleteRespawnAsync(RespawnCycleId cycleId, GameWindowTarget? capturedLeague)
    {
        try
        {
            if (cycles.TryGetValue(cycleId, out var runtime))
            {
                await runtime.ReturnOnceAsync(async cycle =>
                {
                    if (cycle.Media is not null)
                    {
                        var paused = await cycle.Media.PauseAsync(shutdown.Token);
                        if (!paused.StateVerified) status($"抖音暂停问题：{paused.FailureCode}");
                    }
                    if (cycle.WebCommandIssued && browserState is not null)
                    {
                        var paused = await browserState.IssueAsync(cycleId.Value, "pause", TimeSpan.FromSeconds(2), shutdown.Token);
                        if (!paused.Ok) status($"抖音网页暂停问题：{paused.ErrorCode}");
                    }
                    if (cycle.DesktopAttached) _ = await douyin.RestoreAsync(cycleId, shutdown.Token);
                    if (capturedLeague is not null) _ = await league.TryRestoreFocusOnceAsync(capturedLeague, shutdown.Token);
                    await leagueAudio.CompleteAsync(cycleId);
                });
                if (cycles.TryRemove(cycleId, out var completed)) await completed.DisposeAsync();
            }
            else
            {
                if (capturedLeague is not null) _ = await league.TryRestoreFocusOnceAsync(capturedLeague, shutdown.Token);
                await leagueAudio.CompleteAsync(cycleId);
            }
            status("已复活 · League 已恢复");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { status($"返回 League 时遇到问题：{ex.GetType().Name}"); await leagueAudio.CompleteAsync(cycleId); }
    }

    private static double ValidSeconds(double? value) => value is { } seconds && double.IsFinite(seconds) && seconds >= 0 ? seconds : 0;

    private async Task<string?> FindWindowClassAsync(string douyinPath, TimeSpan timeout, CancellationToken token)
    {
        var until = DateTime.UtcNow + timeout;
        do
        {
            var candidates = new List<DouyinWindowCandidate>();
            var eligible = windowSource.EnumerateTopLevelWindows()
                .Where(w => w.IsTopLevel && w.IsVisible && !w.IsToolWindow && w.Bounds.Width > 0 && w.Bounds.Height > 0)
                .ToArray();
            foreach (var processWindows in eligible.GroupBy(w => w.Identity.ProcessId))
            {
                var process = await identityReader.TryReadAsync(processWindows.Key, token);
                if (process is null) continue;
                foreach (var w in processWindows)
                    candidates.Add(new(process.NormalizedExecutablePath, w.Identity.WindowClass, w.IsVisible, w.IsTopLevel, w.IsToolWindow, w.Identity.Handle));
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
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        foreach (var runtime in cycles.Values) await runtime.DisposeAsync();
        cycles.Clear();
        shutdown.Dispose();
    }
}
