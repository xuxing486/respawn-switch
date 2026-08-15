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
    private readonly ILeagueWindowController league; private readonly IDouyinWindowController douyin; private readonly TimeProvider time; private readonly AppSettings settings; private readonly Action<string> status;
    private readonly CancellationTokenSource shutdown = new(); private Task? loop; private GameWindowTarget? game; private IDouyinMediaController? media;
    public RespawnCoordinator(RespawnOverlayWindow overlay, AppSettings settings, Action<string> status)
    {
        this.overlay = overlay; this.settings = settings; this.status = status; time = TimeProvider.System;
        var client = new HttpClient(RiotHttpClientFactory.CreateHandler(new RiotTlsCertificateValidator())) { BaseAddress = RiotEndpoint.Origin };
        var api = new RiotLiveClientApi(client, new RiotRequestTimeouts(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));
        probe = new LeagueGameProbe(api, new RespawnTimerSemantics(TimerSemanticStatus.VerifiedForCurrentPatch, "mvp", 1.0, "mvp-seconds"), new LeaguePollingSchedule(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)), time);
        machine = new RespawnStateMachine(new RespawnStateMachineOptions(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), 0, time.TimestampFrequency));
        var windows = new RespawnSwitch.Windows.Identity.NativeWindowSnapshotSource();
        league = new LeagueWindowLocator(windows, new RespawnSwitch.Windows.Identity.ToolhelpProcessSnapshot());
        douyin = new MvpDouyinWindowController(windows, new DouyinWindowLocator(windows, new RespawnSwitch.Windows.Identity.DouyinProcessIdentityReader()));
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
            case DeathConfirmed x: game = await league.TryFindAsync(x.Sample.TimelineKey, shutdown.Token); status(game is null ? "未找到唯一无边框 League 窗口，已安全跳过" : "已检测到死亡"); break;
            case AttachmentRequested x when game is not null:
                var candidates = (await DouyinGsmTcDiscovery.DiscoverAsync(shutdown.Token)).Where(d => string.IsNullOrEmpty(settings.SourceAppUserModelId) || (d.SourceAppUserModelId == settings.SourceAppUserModelId && d.DiagnosticFingerprint == settings.DiagnosticFingerprint)).ToArray();
                if (candidates.Length != 1) { status("未找到唯一抖音媒体会话，已安全跳过"); break; }
                media = new GsmtcDouyinMediaController(new GsmtcMediaProfile(candidates[0].SourceAppUserModelId, candidates[0].DiagnosticFingerprint));
                var attached = await douyin.AttachAsync(new WindowAttachRequest(x.CycleId, game, game.Bounds, settings.DouyinPath, settings.DouyinWindowClass ?? string.Empty, true), shutdown.Token);
                if (!attached.PostconditionVerified) { status("抖音窗口未唯一确认，未执行媒体控制"); break; }
                status((await media.PlayAsync(shutdown.Token)).StateVerified ? "抖音已启动并显示倒计时" : "抖音播放未确认，已安全跳过"); break;
            case DeadSampleUpdated x when game is not null: overlay.Dispatcher.Invoke(() => overlay.ShowCountdown(game, (int)Math.Ceiling(Math.Max(0, x.Sample.RespawnTimerSeconds.GetValueOrDefault())))); break;
            case RespawnConfirmed x:
                if (media is not null) _ = await media.PauseAsync(shutdown.Token); overlay.Dispatcher.Invoke(overlay.Hide); _ = await douyin.RestoreAsync(x.CycleId, shutdown.Token);
                if (game is not null) _ = await league.TryRestoreFocusOnceAsync(game, shutdown.Token); status("已复活，已恢复 League"); break;
        }
    }
    public async ValueTask DisposeAsync() { await StopAsync(); shutdown.Dispose(); }
}
