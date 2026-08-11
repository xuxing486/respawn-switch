# RespawnSwitch Windows Automation and App Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the local Windows x64 RespawnSwitch prototype after the Riot semantic gate by adding targeted Douyin media control, verified transactional window automation, a no-activate overlay, cycle-safe coordination, settings/diagnostics, recovery, and a self-contained release validated through ten real death/respawn cycles.

**Architecture:** `RespawnSwitch.Application` owns immutable ports and orchestration; `RespawnSwitch.Windows` owns WinRT, Win32, UI Automation, window identity, overlay-host services, focus, hotkeys, and session guards; `RespawnSwitch.Infrastructure` owns atomic JSON persistence and redacted JSONL logs; `RespawnSwitch.App` owns WPF views, tray behavior, composition, and lifecycle. Every external mutation is verified, tied to one `RespawnCycleId`, persisted before it begins, and restored only with identity checks plus per-property compare-and-restore.

**Tech Stack:** C# 12, .NET SDK 8.0.423, .NET 8, WPF, Windows Forms `NotifyIcon`, Win32 P/Invoke, WinRT GSMTC through the versioned Windows TFM, Windows UI Automation, xUnit 2.5.3, self-contained Windows x64 directory publishing.

## Global Constraints

- Prerequisite: Part 1 Gate A in `docs/superpowers/plans/2026-08-10-respawnswitch-core-and-risk-probes.md` is committed with `docs/acceptance/riot-semantic-probe.md` containing `Passed: true`. Stop immediately if that evidence is absent, stale for the installed League patch, or internally inconsistent.
- The approved product contract remains `docs/superpowers/specs/2026-08-10-lol-douyin-respawn-assistant-design.md`.
- Run commands from `C:\Users\1\Documents\Codex\2026-08-10\yeah-2`. Before `dotnet` commands, set `DOTNET_CLI_HOME`, `NUGET_PACKAGES`, and telemetry opt-out exactly as in Part 1. Before Git commands in this desktop sandbox, set `GIT_DIR=work\git-metadata` and `GIT_WORK_TREE` to the workspace.
- Continue RED → GREEN → focused regression → commit. Real-environment gates supplement automated tests; they never replace them.
- Windows production projects target `net8.0-windows10.0.26100.0`, set `SupportedOSPlatformVersion=10.0.17763.0`, and target x64.
- Do not add `Microsoft.Windows.SDK.Contracts`, `Microsoft.Windows.CsWinRT`, `Microsoft.WindowsAppSDK`, an IoC container, or a third-party UI Automation package. Use the SDK's WinRT projection, BCL/WPF/WinForms, and manually declared narrow P/Invoke surfaces.
- The application is ordinary unpackaged `asInvoker`. Never request administrator privileges or `uiAccess`. `globalMediaControl` is not placed in an unpackaged manifest; actual GSMTC access must pass both Debug and published smoke gates.
- UI Automation runs only on a long-lived dedicated MTA worker. Never call an external provider from the WPF Dispatcher thread, never use `Thread.Abort`, and never allow more than one quarantined hung provider worker.
- Never send global media keys, `SendInput`, an emulated spacebar, coordinate clicks, Alt simulation, or game input. Never call `AttachThreadInput`.
- League discovery may use `EnumWindows`, `GetWindowThreadProcessId`, and Toolhelp snapshots only. No code path may call `OpenProcess`, `Process.GetProcessById`, `Process.MainModule`, `QueryFullProcessImageName`, WMI process queries, or any API that creates a handle to the League PID.
- Douyin mutations are fail-closed: zero or multiple identities, invalid/reused HWND, unverifiable results, or profile drift prevents the action.
- Any claim that a window or media operation succeeded requires an observed postcondition. An accepted or posted Windows request is not itself success.
- Persist a recovery intent atomically before every visibility, bounds, show-state, or Topmost mutation. Restore only properties whose current value still equals the last value verified as applied by RespawnSwitch.
- Store no Riot tokens, command lines, full API payloads, Douyin cookies, credentials, or full Riot ID. Diagnostics are local and redacted.
- Every `docs/acceptance/*.md` evidence file starts with `BuildCommit:`, `EvidenceSha256:`, and `Passed:` fields; task-specific fields follow. `Passed` is false until the named real checks finish.
- Public or closed distribution remains blocked until Task 21's live Riot policy review and any required product registration/review are complete. Do not perform an external registration submission without the user's confirmation at that boundary.
---

## Part 2 File Map

### Application contracts and coordination

- `src/RespawnSwitch.Application/Media/MediaContracts.cs`
- `src/RespawnSwitch.Application/Windows/WindowContracts.cs`
- `src/RespawnSwitch.Application/Recovery/WindowRecoveryContracts.cs`
- `src/RespawnSwitch.Application/Overlay/OverlayContracts.cs`
- `src/RespawnSwitch.Application/Overlay/OverlayTextFormatter.cs`
- `src/RespawnSwitch.Application/Settings/AppSettings.cs`
- `src/RespawnSwitch.Application/Logging/DiagnosticContracts.cs`
- `src/RespawnSwitch.Application/Coordination/CycleEffects.cs`
- `src/RespawnSwitch.Application/Coordination/RespawnCoordinator.cs`
- `src/RespawnSwitch.Application/Coordination/IRespawnCoordinator.cs`
- `src/RespawnSwitch.Application/UserInterface/FirstRunWorkflow.cs`
- `src/RespawnSwitch.Application/UserInterface/TrayMenuState.cs`
- `src/RespawnSwitch.Application/UserInterface/SettingsValidator.cs`
- `src/RespawnSwitch.Application/Lifecycle/AppLifetime.cs`
- `src/RespawnSwitch.Application/Monitoring/LeagueMonitorService.cs`
- `src/RespawnSwitch.Application/Recovery/StartupRecoveryService.cs`
- `src/RespawnSwitch.Application/Diagnostics/SelfTestCommand.cs`

### Infrastructure

- `src/RespawnSwitch.Infrastructure/Files/IAtomicFileWriter.cs`
- `src/RespawnSwitch.Infrastructure/Files/AtomicFileWriter.cs`
- `src/RespawnSwitch.Infrastructure/Settings/JsonSettingsStore.cs`
- `src/RespawnSwitch.Infrastructure/Recovery/JsonWindowRecoveryJournal.cs`
- `src/RespawnSwitch.Infrastructure/Logging/JsonLinesDiagnosticLog.cs`
- `src/RespawnSwitch.Infrastructure/Logging/DiagnosticRedactor.cs`
- `src/RespawnSwitch.Infrastructure/Logging/LogRetentionPolicy.cs`

### Windows implementation

- `src/RespawnSwitch.Windows/Interop/User32.cs`
- `src/RespawnSwitch.Windows/Interop/Kernel32Toolhelp.cs`
- `src/RespawnSwitch.Windows/Interop/DwmApi.cs`
- `src/RespawnSwitch.Windows/Interop/Shell32.cs`
- `src/RespawnSwitch.Windows/Interop/WtsApi32.cs`
- `src/RespawnSwitch.Windows/Toolhelp/ToolhelpProcessSnapshot.cs`
- `src/RespawnSwitch.Windows/Identity/WindowIdentityVerifier.cs`
- `src/RespawnSwitch.Windows/Identity/DouyinProcessIdentityReader.cs`
- `src/RespawnSwitch.Windows/Windows/LeagueWindowLocator.cs`
- `src/RespawnSwitch.Windows/Windows/DouyinWindowLocator.cs`
- `src/RespawnSwitch.Windows/Windows/DouyinWindowController.cs`
- `src/RespawnSwitch.Windows/Windows/LeagueWindowController.cs`
- `src/RespawnSwitch.Windows/Windows/WindowStateReader.cs`
- `src/RespawnSwitch.Windows/Windows/WindowMutationVerifier.cs`
- `src/RespawnSwitch.Windows/Windows/CompareAndRestorePlanner.cs`
- `src/RespawnSwitch.Windows/Media/GsmtcSessionCatalog.cs`
- `src/RespawnSwitch.Windows/Media/WinRtGsmtcGateway.cs`
- `src/RespawnSwitch.Windows/Media/GsmtcDouyinMediaController.cs`
- `src/RespawnSwitch.Windows/Media/UiaAutomationWorker.cs`
- `src/RespawnSwitch.Windows/Media/UiaDouyinMediaController.cs`
- `src/RespawnSwitch.Windows/Media/UiaCalibrationService.cs`
- `src/RespawnSwitch.Windows/Displays/MonitorLayoutService.cs`
- `src/RespawnSwitch.Windows/Hotkeys/GlobalHotkeyService.cs`
- `src/RespawnSwitch.Windows/Session/DesktopAutomationGuard.cs`

### WPF application and tools

- `src/RespawnSwitch.App/App.xaml`
- `src/RespawnSwitch.App/App.xaml.cs`
- `src/RespawnSwitch.App/app.manifest`
- `src/RespawnSwitch.App/Bootstrapper.cs`
- `src/RespawnSwitch.App/Lifecycle/SingleInstanceGuard.cs`
- `src/RespawnSwitch.App/Lifecycle/WpfExceptionBridge.cs`
- `src/RespawnSwitch.App/Overlay/RespawnOverlayWindow.xaml`
- `src/RespawnSwitch.App/Overlay/RespawnOverlayWindow.xaml.cs`
- `src/RespawnSwitch.App/Overlay/WpfRespawnOverlay.cs`
- `src/RespawnSwitch.App/Tray/TrayIconController.cs`
- `src/RespawnSwitch.App/FirstRun/FirstRunWindow.xaml`
- `src/RespawnSwitch.App/FirstRun/FirstRunViewModel.cs`
- `src/RespawnSwitch.App/Settings/SettingsWindow.xaml`
- `src/RespawnSwitch.App/Settings/SettingsViewModel.cs`
- `src/RespawnSwitch.App/Diagnostics/DiagnosticsWindow.xaml`
- `src/RespawnSwitch.App/Diagnostics/DiagnosticsViewModel.cs`
- `tools/RespawnSwitch.MediaSmoke/RespawnSwitch.MediaSmoke.csproj`
- `tools/RespawnSwitch.MediaSmoke/Program.cs`
- `tools/RespawnSwitch.TestWindowHost/*`
- `build/Publish-RespawnSwitch.ps1`
- `build/Verify-RespawnSwitchPublish.ps1`
- `docs/acceptance/*`

### Test projects

- Pure behavior: `tests/RespawnSwitch.Application.Tests`, `tests/RespawnSwitch.Infrastructure.Tests`, and `tests/RespawnSwitch.Windows.Tests`.
- Real HWND/WPF/WinRT behavior: `tests/RespawnSwitch.Desktop.IntegrationTests`; mark the collection non-parallel and skip with an explicit reason outside Windows interactive sessions.
- Dependency and forbidden-API rules: `tests/RespawnSwitch.Architecture.Tests`.

## Locked Part 2 Public Contracts

Keep these names and signatures stable across Tasks 8–21.

```csharp
namespace RespawnSwitch.Application.Media;

public enum PlaybackState { Unknown, Playing, Paused, Stopped }

public enum MediaFailureKind
{
    None,
    NotConfigured,
    NoMatch,
    AmbiguousMatch,
    PermissionDenied,
    Unsupported,
    TimedOut,
    ProviderHung,
    CommandRejected,
    StateUnverified,
    TargetChanged,
    Cancelled,
    Unexpected
}

public abstract record MediaControlProfile(string ControllerName);

public sealed record GsmtcMediaProfile(
    string SourceAppUserModelId,
    string DiagnosticFingerprint)
    : MediaControlProfile("GSMTC");

public sealed record UiaMediaProfile(
    string NormalizedExecutablePath,
    string WindowClass,
    string SelectorVersion,
    string StateProperty,
    string PlaySelector,
    string PauseSelector)
    : MediaControlProfile("UIA");

public sealed record MediaProbeResult(
    bool IsUsable,
    PlaybackState State,
    MediaFailureKind FailureKind,
    string FailureCode,
    string ControllerName,
    IReadOnlyList<string> DiagnosticFingerprints);

public sealed record PlaybackStateResult(
    PlaybackState State,
    bool IsVerified,
    MediaFailureKind FailureKind,
    string FailureCode,
    string ControllerName);

public sealed record MediaCommandResult(
    bool CommandSent,
    bool TargetAccepted,
    bool StateVerified,
    PlaybackState FinalState,
    MediaFailureKind FailureKind,
    string FailureCode,
    string ControllerName);

public interface IDouyinMediaController
{
    string Name { get; }
    ValueTask<MediaProbeResult> ProbeAsync(CancellationToken cancellationToken);
    ValueTask<MediaCommandResult> PlayAsync(CancellationToken cancellationToken);
    ValueTask<MediaCommandResult> PauseAsync(CancellationToken cancellationToken);
    ValueTask<PlaybackStateResult> GetPlaybackStateAsync(CancellationToken cancellationToken);
}

public interface IMediaControllerFactory
{
    ValueTask<IDouyinMediaController?> CreateAsync(
        IReadOnlyList<MediaControlProfile> profilesInPriorityOrder,
        CancellationToken cancellationToken);
}
```

```csharp
using RespawnSwitch.Core.Game;
using RespawnSwitch.Core.Respawn;

namespace RespawnSwitch.Application.Windows;

public readonly record struct NativeWindowHandle(nint Value)
{
    public bool IsNull => Value == 0;
}

public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

public sealed record ProcessIdentity(
    int ProcessId,
    DateTimeOffset StartedAtUtc,
    string NormalizedExecutablePath,
    string SignatureSubject,
    string SignatureThumbprint);

public sealed record WindowIdentity(
    NativeWindowHandle Handle,
    int ProcessId,
    string WindowClass);

public sealed record GameWindowTarget(
    WindowIdentity Identity,
    PixelRect Bounds,
    string ProcessName,
    string TimelineKey,
    bool IsBorderless);

public sealed record DouyinWindowTarget(
    WindowIdentity Identity,
    ProcessIdentity Process,
    PixelRect Bounds);

public interface IGamePresenceProbe
{
    ValueTask<GamePresenceSnapshot> ProbeAsync(CancellationToken cancellationToken);
}

public interface ILeagueWindowController
{
    ValueTask<GameWindowTarget?> TryFindAsync(
        string timelineKey,
        CancellationToken cancellationToken);

    ValueTask<bool> TryRestoreFocusOnceAsync(
        GameWindowTarget target,
        CancellationToken cancellationToken);

    ValueTask FlashTaskbarAsync(
        GameWindowTarget target,
        CancellationToken cancellationToken);
}

public sealed record WindowAttachRequest(
    RespawnCycleId CycleId,
    GameWindowTarget GameWindow,
    PixelRect TargetWorkArea,
    string CalibratedExecutablePath,
    string CalibratedWindowClass,
    bool AllowLaunch);

public sealed record WindowOperationResult(
    bool RequestIssued,
    bool PostconditionVerified,
    NativeWindowHandle? Window,
    string FailureCode);

public interface IDouyinWindowController
{
    ValueTask<WindowOperationResult> AttachAsync(
        WindowAttachRequest request,
        CancellationToken cancellationToken);

    ValueTask<WindowOperationResult> RestoreAsync(
        RespawnCycleId cycleId,
        CancellationToken cancellationToken);
}
```

```csharp
using RespawnSwitch.Application.Windows;
using RespawnSwitch.Core.Respawn;

namespace RespawnSwitch.Application.Recovery;

[Flags]
public enum WindowMutationFields
{
    None = 0,
    NormalBounds = 1,
    ShowState = 2,
    Visibility = 4,
    Topmost = 8
}

public enum WindowShowState { Hidden, Normal, Minimized, Maximized }

public sealed record VerifiedWindowState(
    WindowIdentity Identity,
    PixelRect NormalBounds,
    WindowShowState ShowState,
    bool IsVisible,
    bool IsTopmost);

public sealed record WindowMutationIntent(
    RespawnCycleId CycleId,
    ProcessIdentity Process,
    VerifiedWindowState Original,
    VerifiedWindowState Target,
    WindowMutationFields IntendedFields,
    bool MutationStarted,
    long WrittenAtTimestamp);

public sealed record VerifiedWindowMutation(
    WindowMutationIntent Intent,
    WindowMutationFields AppliedFields,
    VerifiedWindowState LastVerifiedState);

public sealed record WindowRecoveryRecord(
    int SchemaVersion,
    VerifiedWindowMutation Mutation);

public interface IWindowRecoveryJournal
{
    ValueTask WriteIntentAsync(
        WindowMutationIntent intent,
        CancellationToken cancellationToken);

    ValueTask WriteVerifiedMutationAsync(
        VerifiedWindowMutation mutation,
        CancellationToken cancellationToken);

    ValueTask<WindowRecoveryRecord?> TryReadActiveAsync(
        CancellationToken cancellationToken);

    ValueTask DeleteAsync(
        RespawnCycleId cycleId,
        CancellationToken cancellationToken);
}

public sealed record WindowRestorePlan(
    bool IdentityVerified,
    WindowMutationFields FieldsToRestore,
    VerifiedWindowState DesiredState,
    string FailureCode);

public interface ICompareAndRestorePlanner
{
    WindowRestorePlan Plan(
        WindowRecoveryRecord record,
        WindowIdentity currentIdentity,
        VerifiedWindowState currentState);
}
```

```csharp
using RespawnSwitch.Application.Windows;
using RespawnSwitch.Core.Respawn;

namespace RespawnSwitch.Application.Overlay;

public enum OverlayMessageKind
{
    Countdown,
    ReadingTimer,
    ConnectionUnstable,
    RespawnedHotkey,
    MediaFailed,
    ManualReturnRequired
}

public sealed record OverlayViewState(
    OverlayMessageKind Kind,
    int? DisplaySeconds,
    string Text,
    double Scale,
    double Opacity,
    bool ShowBackground);

public sealed record OverlayAnchor(
    NativeWindowHandle GameWindow,
    PixelRect GameBounds,
    NativeWindowHandle? DouyinWindow);

public sealed record OverlayOperationResult(
    bool RequestIssued,
    bool PostconditionVerified,
    string FailureCode);

public interface IRespawnOverlay
{
    ValueTask<OverlayOperationResult> ShowAsync(
        RespawnCycleId cycleId,
        OverlayViewState state,
        OverlayAnchor anchor,
        CancellationToken cancellationToken);

    ValueTask<OverlayOperationResult> UpdateAsync(
        RespawnCycleId cycleId,
        OverlayViewState state,
        CancellationToken cancellationToken);

    ValueTask<OverlayOperationResult> PlaceAboveAsync(
        RespawnCycleId cycleId,
        NativeWindowHandle otherTopmostWindow,
        CancellationToken cancellationToken);

    ValueTask<OverlayOperationResult> HideAsync(
        RespawnCycleId cycleId,
        CancellationToken cancellationToken);
}
```

```csharp
using RespawnSwitch.Core.Respawn;

namespace RespawnSwitch.Application.Coordination;

public enum CleanupReason
{
    RespawnConfirmed,
    UserPaused,
    GameExited,
    AbandonedUnknown,
    TimelineReset,
    ApplicationExit,
    RecoverableFailure
}

public interface IRespawnCoordinator
{
    ValueTask HandleAsync(
        RespawnTransition transition,
        CancellationToken cancellationToken);

    ValueTask PauseAutomationAsync(CancellationToken cancellationToken);
    ValueTask ResumeAutomationAsync(CancellationToken cancellationToken);
    ValueTask ShutdownAsync(CancellationToken cancellationToken);
}
```

The media profile types are configuration data, not permission grants. `ProcessIdentity` is valid only for Douyin and RespawnSwitch-owned test processes. `GameWindowTarget` deliberately contains no executable path, start time, or process handle.

---

### Task 8: Add media contracts, targeted GSMTC control, and Gate B

**Files:**

- Create: `src/RespawnSwitch.Application/Media/MediaContracts.cs`
- Create: `src/RespawnSwitch.Windows/Media/IGsmtcGateway.cs`
- Create: `src/RespawnSwitch.Windows/Media/GsmtcSessionDescriptor.cs`
- Create: `src/RespawnSwitch.Windows/Media/GsmtcSessionCatalog.cs`
- Create: `src/RespawnSwitch.Windows/Media/WinRtGsmtcGateway.cs`
- Create: `src/RespawnSwitch.Windows/Media/GsmtcDouyinMediaController.cs`
- Create: `src/RespawnSwitch.Windows/Media/MediaControllerFactory.cs`
- Create: `tools/RespawnSwitch.MediaSmoke/RespawnSwitch.MediaSmoke.csproj`
- Create: `tools/RespawnSwitch.MediaSmoke/Program.cs`
- Test: `tests/RespawnSwitch.Windows.Tests/Media/GsmtcSessionCatalogTests.cs`
- Test: `tests/RespawnSwitch.Windows.Tests/Media/GsmtcDouyinMediaControllerTests.cs`
- Create after real run: `docs/acceptance/gsm-tc-smoke.md`

**Interfaces:**

- Consumes: the locked `MediaControlProfile` and media result contracts.
- Produces: `GsmtcDouyinMediaController : IDouyinMediaController`, `MediaControllerFactory`, and real evidence that the unpackaged Debug and self-contained builds can access the target session.

- [ ] **Step 1: Enforce the Part 1 prerequisite before adding production behavior**

  ```powershell
  $report = Get-Content docs\acceptance\riot-semantic-probe.md -Raw
  if ($report -notmatch '(?m)^Passed:\s*true\s*$') {
    throw 'Gate A has not passed. Stop before Part 2.'
  }
  ```

  Confirm that the report's League patch matches the installed client. If it does not, rerun Part 1 Task 7 rather than editing the old result.

  Add the smoke project explicitly:

  ```powershell
  dotnet new console -n RespawnSwitch.MediaSmoke -o tools\RespawnSwitch.MediaSmoke -f net8.0 --no-restore
  dotnet sln RespawnSwitch.sln add tools\RespawnSwitch.MediaSmoke\RespawnSwitch.MediaSmoke.csproj
  dotnet add tools\RespawnSwitch.MediaSmoke reference src\RespawnSwitch.Application src\RespawnSwitch.Windows
  ```

  Retarget its project to `net8.0-windows10.0.26100.0`, set `SupportedOSPlatformVersion=10.0.17763.0`, `PlatformTarget=x64`, `RuntimeIdentifier=win-x64`, `SelfContained=true`, `PublishSingleFile=false`, and `PublishTrimmed=false`.

- [ ] **Step 2: Write failing catalog and command tests**

  Cover these exact cases:

  - zero exact AUMID matches → `NoMatch`;
  - one exact AUMID plus calibrated fingerprint → selected;
  - two matches, even if one is the system's current session → `AmbiguousMatch`;
  - media title changes → selection remains based on AUMID/fingerprint, never title text;
  - `PlayAsync` when already Playing → no toggle, final state verified Playing;
  - `PlayAsync` when Paused → calls `TryPlayAsync` exactly once and re-reads state;
  - `PauseAsync` mirrors those two cases with `TryPauseAsync`;
  - API returns true but state remains wrong → `StateUnverified`;
  - API returns false → `CommandRejected`;
  - target set changes between enumeration and command → `TargetChanged`;
  - cancellation maps to `Cancelled`, not `Unexpected`.

  The fake gateway records calls so tests assert that `TryTogglePlayPauseAsync` does not exist in the gateway contract and is never invoked.

- [ ] **Step 3: Run RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Windows.Tests -c Debug --filter FullyQualifiedName~Gsmtc --no-restore
  ```

- [ ] **Step 4: Implement the narrow WinRT gateway and fail-closed selector**

  ```csharp
  internal interface IGsmtcGateway
  {
      ValueTask<IReadOnlyList<GsmtcSessionDescriptor>> EnumerateAsync(
          CancellationToken cancellationToken);

      ValueTask<bool> TryPlayAsync(
          string sessionToken,
          CancellationToken cancellationToken);

      ValueTask<bool> TryPauseAsync(
          string sessionToken,
          CancellationToken cancellationToken);

      ValueTask<PlaybackState> ReadStateAsync(
          string sessionToken,
          CancellationToken cancellationToken);
  }

  internal sealed record GsmtcSessionDescriptor(
      string SessionToken,
      string SourceAppUserModelId,
      string DiagnosticFingerprint,
      PlaybackState PlaybackState,
      bool CanPlay,
      bool CanPause);
  ```

  `WinRtGsmtcGateway` calls `GlobalSystemMediaTransportControlsSessionManager.RequestAsync()`, enumerates fresh sessions for every public controller operation, and maps WinRT status values explicitly. The fingerprint is a versioned SHA-256 of the exact AUMID plus package-family/publisher identity when Windows exposes it; for an unpackaged desktop source it uses the exact AUMID plus the fingerprint schema version. Playback state, enabled-control flags, and titles are excluded because they can change between Play and Pause. Titles may appear redacted in diagnostics but are not identity. Dispose event subscriptions and do not retain a session past the operation that selected it.

  `GsmtcSessionCatalog.Select` filters with ordinal exact comparisons for both configured AUMID and fingerprint. It succeeds only for a single match. Every command performs select → read current state → explicit Play/Pause if needed → re-enumerate and reselect → read final state.

- [ ] **Step 5: Run GREEN and Windows unit regression**

  ```powershell
  dotnet test tests\RespawnSwitch.Windows.Tests -c Debug --no-restore
  dotnet test tests\RespawnSwitch.Architecture.Tests -c Debug --no-restore
  ```

- [ ] **Step 6: Add a deterministic smoke CLI**

  `RespawnSwitch.MediaSmoke` targets the same Windows TFM and x64 runtime as the app. Commands are:

  ```text
  list
  probe --aumid AUMID --fingerprint SHA256_HEX
  play --aumid AUMID --fingerprint SHA256_HEX
  pause --aumid AUMID --fingerprint SHA256_HEX
  ```

  It prints JSON containing controller, match count, pre-state, command acceptance, and verified post-state; it never prints media titles unredacted. Exit codes are `0=verified`, `2=no match`, `3=ambiguous`, `4=permission/unsupported`, `5=command or state verification failed`, and `6=invalid arguments`.

- [ ] **Step 7: Execute Gate B in both build forms**

  User action required: open the installed Douyin desktop app at a playable video and identify the intended session from `list`.

  ```powershell
  dotnet run --project tools\RespawnSwitch.MediaSmoke -c Debug -- list
  $confirmedAumid = Read-Host 'Paste the exact confirmed Douyin SourceAppUserModelId'
  $confirmedFingerprint = Read-Host 'Paste the exact confirmed diagnostic fingerprint'
  dotnet run --project tools\RespawnSwitch.MediaSmoke -c Debug -- probe --aumid $confirmedAumid --fingerprint $confirmedFingerprint
  dotnet run --project tools\RespawnSwitch.MediaSmoke -c Debug -- play --aumid $confirmedAumid --fingerprint $confirmedFingerprint
  dotnet run --project tools\RespawnSwitch.MediaSmoke -c Debug -- pause --aumid $confirmedAumid --fingerprint $confirmedFingerprint

  dotnet publish tools\RespawnSwitch.MediaSmoke -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o artifacts\media-smoke\published
  & artifacts\media-smoke\published\RespawnSwitch.MediaSmoke.exe probe --aumid $confirmedAumid --fingerprint $confirmedFingerprint
  & artifacts\media-smoke\published\RespawnSwitch.MediaSmoke.exe play --aumid $confirmedAumid --fingerprint $confirmedFingerprint
  & artifacts\media-smoke\published\RespawnSwitch.MediaSmoke.exe pause --aumid $confirmedAumid --fingerprint $confirmedFingerprint
  ```

  `docs/acceptance/gsm-tc-smoke.md` records Windows build, Douyin version and signature identity, AUMID/fingerprint, Debug results, published results, concurrent Spotify/browser-session test, evidence hashes, and `Passed`. If either build returns access denied or cannot uniquely target Douyin, record the exact HRESULT and keep GSMTC unavailable; do not add a manifest capability to the unpackaged app. Task 14's UIA route then becomes mandatory.

- [ ] **Step 8: Commit**

  ```powershell
  git add src\RespawnSwitch.Application\Media src\RespawnSwitch.Windows\Media tools\RespawnSwitch.MediaSmoke tests\RespawnSwitch.Windows.Tests\Media docs\acceptance\gsm-tc-smoke.md RespawnSwitch.sln
  git commit -m "feat(windows): add targeted GSMTC media control"
  ```

---

### Task 9: Add Win32 boundaries, safe window identities, locators, and the test host

**Files:**

- Create: `src/RespawnSwitch.Application/Windows/WindowContracts.cs`
- Create: `src/RespawnSwitch.Windows/Interop/User32.cs`
- Create: `src/RespawnSwitch.Windows/Interop/Kernel32Toolhelp.cs`
- Create: `src/RespawnSwitch.Windows/Interop/DwmApi.cs`
- Create: `src/RespawnSwitch.Windows/Interop/Shell32.cs`
- Create: `src/RespawnSwitch.Windows/Toolhelp/ToolhelpProcessSnapshot.cs`
- Create: `src/RespawnSwitch.Windows/Identity/DouyinProcessIdentityReader.cs`
- Create: `src/RespawnSwitch.Windows/Identity/WindowIdentityVerifier.cs`
- Create: `src/RespawnSwitch.Windows/Windows/LeagueWindowLocator.cs`
- Create: `src/RespawnSwitch.Windows/Windows/DouyinWindowLocator.cs`
- Create: `src/RespawnSwitch.Windows/Windows/WindowStateReader.cs`
- Replace: `tools/RespawnSwitch.TestWindowHost/App.xaml*`
- Create: `tools/RespawnSwitch.TestWindowHost/HostOptions.cs`
- Create: `tools/RespawnSwitch.TestWindowHost/TestWindow.xaml*`
- Test: `tests/RespawnSwitch.Windows.Tests/Identity/*.cs`
- Test: `tests/RespawnSwitch.Windows.Tests/Windows/*LocatorTests.cs`
- Test: `tests/RespawnSwitch.Desktop.IntegrationTests/Windows/WindowLocatorIntegrationTests.cs`
- Test: `tests/RespawnSwitch.Architecture.Tests/LeagueHandleBoundaryTests.cs`

**Interfaces:**

- Consumes: locked window contracts and Part 1's `GamePresenceSnapshot`.
- Produces: verified League/Douyin targets without ever opening the League process.

- [ ] **Step 1: Write failing pure locator and identity tests**

  Use injected snapshots, not live desktop state, to cover visible top-level windows, child/tool/zero-area exclusion, calibrated class scoring, exact normalized Douyin path, PID/start-time/path/class mismatch, signature mismatch, destroyed/reused HWND, multiple equal candidates, Riot Client/lobby exclusion, and a unique `League of Legends.exe` game target. Assert borderless detection from native style/client/frame/monitor evidence; an ordinary decorated window returns `IsBorderless=false` and disables automated overlay/window attachment with a clear diagnostic. Fullscreen-exclusive mode remains unsupported rather than guessed from resolution alone.

  Add an architecture test that scans `src/RespawnSwitch.Windows` source and compiled method references. It fails if a type whose name contains `League` references `OpenProcess`, `Process.GetProcessById`, `MainModule`, `QueryFullProcessImageName`, `ManagementObjectSearcher`, or `PROCESS_` access constants.

- [ ] **Step 2: Run RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Windows.Tests -c Debug --filter "FullyQualifiedName~Identity|FullyQualifiedName~Locator" --no-restore
  dotnet test tests\RespawnSwitch.Architecture.Tests -c Debug --filter FullyQualifiedName~LeagueHandleBoundary --no-restore
  ```

- [ ] **Step 3: Implement narrow interop and snapshot adapters**

  Declare only needed signatures with `LibraryImport` or `DllImport`, `SetLastError=true` where documented, exact `StructLayout`, and `SafeHandle` for the Toolhelp snapshot. Include `EnumWindows`, `GetWindow`, `GetWindowThreadProcessId`, `GetClassName`, `IsWindow`, `IsWindowVisible`, `GetWindowRect`, `GetWindowLongPtr`, `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)`, and Toolhelp `CreateToolhelp32Snapshot`/`Process32FirstW`/`Process32NextW`.

  `LeagueWindowLocator` joins an `EnumWindows` snapshot to a Toolhelp process-name snapshot by PID. It never resolves a League path or start time. `DouyinWindowLocator` may ask `DouyinProcessIdentityReader` for path, start time, and Authenticode identity because the safety restriction is specific to League.

- [ ] **Step 4: Implement the deterministic test window host**

  The host accepts one mode: `normal`, `hidden`, `minimized`, `maximized`, `topmost`, `recreate`, `topmost-peer`, `focus-target`, or `hung-uia`. It writes one JSON line with PID, HWND, class, bounds, mode, and ready status to a caller-specified file. `recreate` destroys and recreates the HWND after a named event; `hung-uia` exposes a provider action that blocks on a named event.

- [ ] **Step 5: Run GREEN plus real HWND integration tests**

  ```powershell
  dotnet test tests\RespawnSwitch.Windows.Tests -c Debug --no-restore
  dotnet test tests\RespawnSwitch.Architecture.Tests -c Debug --no-restore
  dotnet test tests\RespawnSwitch.Desktop.IntegrationTests -c Debug --filter FullyQualifiedName~WindowLocator --no-restore
  ```

  Integration tests start only the RespawnSwitch-owned host, wait for its ready file, exercise HWND discovery, and close it gracefully. They never inspect or mutate the running game or Douyin.

- [ ] **Step 6: Commit**

  ```powershell
  git add src\RespawnSwitch.Application\Windows src\RespawnSwitch.Windows\Interop src\RespawnSwitch.Windows\Toolhelp src\RespawnSwitch.Windows\Identity src\RespawnSwitch.Windows\Windows tools\RespawnSwitch.TestWindowHost tests\RespawnSwitch.Windows.Tests tests\RespawnSwitch.Desktop.IntegrationTests tests\RespawnSwitch.Architecture.Tests
  git commit -m "feat(windows): add safe window discovery and identities"
  ```

---

### Task 10: Add atomic recovery persistence and compare-and-restore planning

**Files:**

- Create: `src/RespawnSwitch.Application/Recovery/WindowRecoveryContracts.cs`
- Create: `src/RespawnSwitch.Infrastructure/Files/IAtomicFileWriter.cs`
- Create: `src/RespawnSwitch.Infrastructure/Files/AtomicFileWriter.cs`
- Create: `src/RespawnSwitch.Infrastructure/Recovery/JsonWindowRecoveryJournal.cs`
- Create: `src/RespawnSwitch.Windows/Windows/CompareAndRestorePlanner.cs`
- Test: `tests/RespawnSwitch.Infrastructure.Tests/Files/AtomicFileWriterTests.cs`
- Test: `tests/RespawnSwitch.Infrastructure.Tests/Recovery/JsonWindowRecoveryJournalTests.cs`
- Test: `tests/RespawnSwitch.Windows.Tests/Windows/CompareAndRestorePlannerTests.cs`

**Interfaces:**

- Consumes: locked recovery contracts, `ProcessIdentity`, and `VerifiedWindowState`.
- Produces: crash-tolerant intent/result journaling and a pure plan that never overwrites user changes.

- [ ] **Step 1: Write failing atomicity, schema, and restore-matrix tests**

  Test first write, replacement write, flush-before-replace, cancellation before commit, stale temporary-file cleanup, truncated JSON, unknown schema version, mismatched Cycle ID deletion, and concurrent writers serialized by a process-local semaphore.

  The restore matrix must assert each field independently:

  | Applied by app | Current equals last applied | Restore original |
  |---|---|---|
  | no | either | no |
  | yes | no | no |
  | yes | yes | yes |

  Add identity failures for HWND, PID, start time, normalized path, signature identity, and window class. Any identity failure yields `FieldsToRestore=None`.

- [ ] **Step 2: Run RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Infrastructure.Tests -c Debug --filter "FullyQualifiedName~AtomicFileWriter|FullyQualifiedName~WindowRecovery" --no-restore
  dotnet test tests\RespawnSwitch.Windows.Tests -c Debug --filter FullyQualifiedName~CompareAndRestore --no-restore
  ```

- [ ] **Step 3: Implement atomic replacement and schema-versioned journal**

  Write UTF-8 JSON to a sibling whose name is a leading dot, the destination filename, a 32-hex GUID, and `.tmp` (for example `.settings.json.0123456789abcdef0123456789abcdef.tmp`), flush the stream with `Flush(flushToDisk: true)`, then use `File.Replace` when the destination exists or same-volume `File.Move` when it does not. Never expose the temp path through public contracts. On startup delete only temp files matching this application's exact prefix in its own LocalApplicationData directory.

  The active file is `%LOCALAPPDATA%\RespawnSwitch\window-recovery.json`; it contains exactly one active cycle. `WriteIntentAsync` requires `MutationStarted=false`, writes it, then the controller writes the same intent with `MutationStarted=true` immediately before the first native request. Each verified property update rewrites `VerifiedWindowMutation`. `DeleteAsync` re-reads and deletes only if Cycle IDs match.

- [ ] **Step 4: Implement pure compare-and-restore**

  Implement `CompareAndRestorePlanner : ICompareAndRestorePlanner`. Compare identity first. For each bit in `AppliedFields`, compare the corresponding current property to `LastVerifiedState`; include the bit in `FieldsToRestore` only on equality. `DesiredState` uses the original value for included fields and the current value for excluded fields, so a caller cannot accidentally rewrite an unowned property. Preserve an originally Topmost window by restoring `true`; never blindly clear Topmost. Equality for rectangles is exact in physical pixels because values were read from the same native API.

- [ ] **Step 5: Run GREEN and fault-injection regression**

  ```powershell
  dotnet test tests\RespawnSwitch.Infrastructure.Tests -c Debug --no-restore
  dotnet test tests\RespawnSwitch.Windows.Tests -c Debug --filter FullyQualifiedName~CompareAndRestore --no-restore
  ```

- [ ] **Step 6: Commit**

  ```powershell
  git add src\RespawnSwitch.Application\Recovery src\RespawnSwitch.Infrastructure\Files src\RespawnSwitch.Infrastructure\Recovery src\RespawnSwitch.Windows\Windows\CompareAndRestorePlanner.cs tests\RespawnSwitch.Infrastructure.Tests tests\RespawnSwitch.Windows.Tests\Windows\CompareAndRestorePlannerTests.cs
  git commit -m "feat(recovery): add transactional window journal"
  ```

---

### Task 11: Implement transactional Douyin attach/restore and Gate C

**Files:**

- Create: `src/RespawnSwitch.Windows/Windows/DouyinWindowController.cs`
- Create: `src/RespawnSwitch.Windows/Windows/WindowMutationVerifier.cs`
- Create: `src/RespawnSwitch.Windows/Windows/WindowMutationTimeouts.cs`
- Test: `tests/RespawnSwitch.Windows.Tests/Windows/DouyinWindowControllerTests.cs`
- Test: `tests/RespawnSwitch.Desktop.IntegrationTests/Windows/DouyinWindowMutationIntegrationTests.cs`
- Create after real run: `docs/acceptance/douyin-window-automation.md`

**Interfaces:**

- Consumes: `IDouyinWindowController`, locators/identity checks from Task 9, and the journal/planner from Task 10.
- Produces: one verified attach and one idempotent compare-and-restore per cycle.

- [ ] **Step 1: Write failing orchestration tests with a fake native gateway**

  Cover these exact sequences:

  - unique existing window → read original → journal intent → mark mutation started → request changed fields only → verify → persist applied bits;
  - absent process → start calibrated executable once → resolve one unique window → attach;
  - cancellation/respawn before launch resolves → do not mutate a late window;
  - no valid/unique window → no journal mutation and no native request;
  - identity changes before or after any request → stop and retain journal for manual recovery;
  - `ShowWindow` previous-visibility return is never treated as a success flag;
  - an accepted `SWP_ASYNCWINDOWPOS` request that fails postcondition returns unverified;
  - window rebuilt once → resolve and validate once, then continue; a second rebuild fails closed;
  - Restore called repeatedly → at most one write per still-owned property and safe no-op thereafter;
  - user changes each field during the cycle → that field is preserved;
  - originally Topmost → remains Topmost;
  - started by this cycle → restore target is non-activated Minimized/taskbar, never process termination and never a false “tray” claim.

- [ ] **Step 2: Run RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Windows.Tests -c Debug --filter FullyQualifiedName~DouyinWindowController --no-restore
  ```

- [ ] **Step 3: Implement verified non-activating attach**

  When no valid Douyin target exists and `WindowAttachRequest.AllowLaunch` is true, launch the calibrated executable at most once and record internally that this cycle started it. Use a bounded launch wait (8 seconds total, 100 ms probe interval). An attach target is Normal, visible, temporarily Topmost, and sized to `WindowAttachRequest.TargetWorkArea`. Before each property group:

  1. revalidate HWND/PID/start/path/signature/class;
  2. atomically persist intent/current applied bits;
  3. issue `SetWindowPlacement`, `ShowWindowAsync(SW_SHOWNOACTIVATE)` or `SetWindowPos(HWND_TOPMOST, ..., SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_ASYNCWINDOWPOS)` as appropriate;
  4. poll identity plus the exact postcondition every 20 ms for at most 500 ms;
  5. persist only the bits actually observed.

  Do not call activating `SW_SHOW`, `SW_RESTORE`, or `SW_SHOWMAXIMIZED` in the preferred path. If a minimized real Douyin build cannot be restored without activation, return `douyin.window.nonactivate-restore-unsupported`; the diagnostic gate may explicitly allow focus loss, but the program must stop claiming focus preservation.

- [ ] **Step 4: Implement per-field restore**

  Re-resolve and verify identity, call the injected `ICompareAndRestorePlanner.Plan`, persist the restore intent, and apply only `FieldsToRestore`. Verify every write. Delete the journal only after all planned writes verify or there are no owned fields remaining. If identity cannot be proven, leave the journal and return a manual-recovery diagnostic without touching the candidate window.

- [ ] **Step 5: Run GREEN and TestWindowHost integration matrix**

  ```powershell
  dotnet test tests\RespawnSwitch.Windows.Tests -c Debug --no-restore
  dotnet test tests\RespawnSwitch.Desktop.IntegrationTests -c Debug --filter FullyQualifiedName~DouyinWindowMutation --no-restore
  ```

  Run host modes `hidden`, `minimized`, `maximized`, `normal`, `topmost`, and `recreate`; include user movement, resizing, minimizing, maximizing, and Topmost changes between attach and restore.

- [ ] **Step 6: Execute Gate C against the installed Douyin client**

  User action required: close private content, keep a harmless playable page open, and approve each diagnostic start state. Test original hidden/absent, minimized, maximized, normal, and already-Topmost states; test one user move/resize and one user focus change during attachment. Record actual foreground HWND before/after, extended frame bounds, visibility, show state, Topmost, start/rebuild behavior, restoration result, and whether non-activating minimized recovery is supported.

  `docs/acceptance/douyin-window-automation.md` must identify the exact Douyin executable hash/signature/version and say `Passed: true` only if identity ambiguity fails closed, all owned fields restore, and user-changed fields survive.

- [ ] **Step 7: Commit**

  ```powershell
  git add src\RespawnSwitch.Windows\Windows tests\RespawnSwitch.Windows.Tests\Windows tests\RespawnSwitch.Desktop.IntegrationTests\Windows docs\acceptance\douyin-window-automation.md
  git commit -m "feat(windows): add transactional Douyin window control"
  ```

---

### Task 12: Implement the no-activate WPF overlay, monitor placement, and Gate D

**Files:**

- Create: `src/RespawnSwitch.Application/Overlay/OverlayContracts.cs`
- Create: `src/RespawnSwitch.Application/Overlay/OverlayTextFormatter.cs`
- Create: `src/RespawnSwitch.Windows/Displays/IMonitorLayoutGateway.cs`
- Create: `src/RespawnSwitch.Windows/Displays/MonitorLayoutService.cs`
- Create: `src/RespawnSwitch.App/Overlay/RespawnOverlayWindow.xaml`
- Create: `src/RespawnSwitch.App/Overlay/RespawnOverlayWindow.xaml.cs`
- Create: `src/RespawnSwitch.App/Overlay/WpfRespawnOverlay.cs`
- Test: `tests/RespawnSwitch.Windows.Tests/Displays/MonitorLayoutServiceTests.cs`
- Test: `tests/RespawnSwitch.Application.Tests/Overlay/OverlayTextTests.cs`
- Test: `tests/RespawnSwitch.Desktop.IntegrationTests/Overlay/RespawnOverlayIntegrationTests.cs`
- Create after real run: `docs/acceptance/overlay-zorder-dpi.md`

**Interfaces:**

- Consumes: `IRespawnOverlay`, game/Douyin HWNDs, physical-pixel monitor data, and user overlay settings.
- Produces: a 360×72-DIP top-center overlay that remains mouse-transparent, non-activating, absent from Alt+Tab, and verified above Douyin in the Topmost band.

- [ ] **Step 1: Write failing text, placement, style, and Z-order tests**

  Assert the only generated Chinese state strings are:

  ```text
  复活还有 N 秒
  正在读取复活时间
  连接不稳定
  已复活，按 Ctrl+Alt+F9 返回游戏
  抖音播放失败，倒计时仍在运行
  请点击游戏任务栏按钮或手动 Alt+Tab
  ```

  Pure placement tests cover 100%, 125%, and 150% DPI; a negative-coordinate secondary monitor; differing primary/secondary DPI; and movement of the game to another display. Integration tests assert `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST`, unchanged foreground HWND, no Alt+Tab entry, hit-test pass-through, and real Z-order above the `topmost-peer` host.

- [ ] **Step 2: Run RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Application.Tests -c Debug --filter FullyQualifiedName~Overlay --no-restore
  dotnet test tests\RespawnSwitch.Windows.Tests -c Debug --filter FullyQualifiedName~MonitorLayout --no-restore
  dotnet test tests\RespawnSwitch.Desktop.IntegrationTests -c Debug --filter FullyQualifiedName~RespawnOverlay --no-restore
  ```

- [ ] **Step 3: Implement WPF presentation and native styles**

  XAML sets `WindowStyle="None"`, `AllowsTransparency="True"`, `ShowInTaskbar="False"`, `ShowActivated="False"`, transparent background, 360×72 DIP default content, rounded semi-transparent dark panel, and white countdown text. It contains no focusable or interactive element.

  In `SourceInitialized`, obtain the HWND, add all required extended styles with `SetWindowLongPtr`, and read them back. `WM_NCHITTEST` returns `HTTRANSPARENT`; `WM_MOUSEACTIVATE` returns `MA_NOACTIVATE`. All public overlay methods marshal only local WPF work to the Dispatcher; no UI Automation/provider call occurs there.

- [ ] **Step 4: Implement Per-Monitor placement and relative Topmost ordering**

  Read the game monitor's work area and DPI, convert the requested DIP size to physical pixels, and place it top-center. On `WM_DPICHANGED`, accept the recommended physical rect and re-evaluate the anchor. `PlaceAboveAsync` must not pass the Douyin HWND directly as `hWndInsertAfter`, because that handle would precede the positioned overlay in Z-order. Reassert the overlay with `HWND_TOPMOST`, inspect the Topmost chain with `GetWindow(GW_HWNDPREV/GW_HWNDNEXT)`, and verify that the overlay precedes Douyin. If not, compute the HWND immediately preceding Douyin and insert after that predecessor, or use `HWND_TOP` when Douyin is first; then verify again. Re-run this after every Douyin show, rebuild, move, or Topmost mutation and return an unverified result if relative ordering cannot be proved.

- [ ] **Step 5: Run GREEN**

  ```powershell
  dotnet test tests\RespawnSwitch.Application.Tests -c Debug --no-restore
  dotnet test tests\RespawnSwitch.Windows.Tests -c Debug --no-restore
  dotnet test tests\RespawnSwitch.Desktop.IntegrationTests -c Debug --filter FullyQualifiedName~RespawnOverlay --no-restore
  ```

- [ ] **Step 6: Execute Gate D on the target displays**

  Place real League in borderless mode, cover it with real Douyin, and verify the overlay is visually above both at every available 100%/125%/150% DPI arrangement, including negative coordinates and mixed DPI. Record HDR, Windows fullscreen-optimization, GPU overlay, and monitor topology. If a combination hides the overlay, mark that combination unsupported and verify the sound/taskbar diagnostic fallback; do not label it successful.

- [ ] **Step 7: Commit**

  ```powershell
  git add src\RespawnSwitch.Application\Overlay src\RespawnSwitch.Windows\Displays src\RespawnSwitch.App\Overlay tests\RespawnSwitch.Application.Tests\Overlay tests\RespawnSwitch.Windows.Tests\Displays tests\RespawnSwitch.Desktop.IntegrationTests\Overlay docs\acceptance\overlay-zorder-dpi.md
  git commit -m "feat(app): add no-activate respawn overlay"
  ```

---

### Task 13: Implement the cycle-safe coordinator and side-effect ledger

**Files:**

- Create: `src/RespawnSwitch.Application/Coordination/CycleEffects.cs`
- Create: `src/RespawnSwitch.Application/Coordination/IRespawnCoordinator.cs`
- Create: `src/RespawnSwitch.Application/Coordination/RespawnCoordinator.cs`
- Create: `src/RespawnSwitch.Application/Coordination/CoordinatorOptions.cs`
- Create: `src/RespawnSwitch.Application/Coordination/CoordinatorTelemetry.cs`
- Test: `tests/RespawnSwitch.Application.Tests/Coordination/RespawnCoordinatorTests.cs`
- Test helper: `tests/RespawnSwitch.Application.Tests/Coordination/ControllableFakes.cs`

**Interfaces:**

- Consumes: state-machine transitions, clock frames, media/window/overlay interfaces, League window controller, and monotonic time.
- Produces: `IRespawnCoordinator` and exactly-once, cancellable external side effects.

- [ ] **Step 1: Write failing deterministic coordinator tests**

  Use task-completion-source fakes to cover:

  - first Dead cycle shows overlay once and updates it without replaying attach;
  - a game target with `IsBorderless=false` records unsupported mode and performs no Douyin/overlay attachment;
  - missing/invalid timer shows ReadingTimer and performs no media/window action;
  - verified timer `<2` seconds stays countdown-only;
  - a later value `>=2` seconds attaches exactly once;
  - window attach verifies → overlay is placed above returned Douyin HWND → Play is attempted;
  - window failure does not suppress countdown or invent media success;
  - media failure changes overlay to MediaFailed but countdown continues;
  - user manually pauses media → coordinator never continuously forces Play;
  - Respawn cancels late launch/window/play tasks before cleanup;
  - a stale completion from Cycle A cannot mutate Cycle B;
  - PauseAutomation, GameExited, TimelineReset, and Shutdown are idempotent cleanup reasons;
  - AbandonedUnknown pauses/hides/restores owned effects but does not publish or log Alive;
  - a short cycle with no Play/window mutation sends no unowned Pause/restore;
  - cleanup pauses media when this cycle sent Play or verified the selected target was Playing after `PlayAsync`; an already-Playing idempotent no-op is therefore paused on respawn, while a zero-match/ambiguous controller is not;
  - cleanup failures are isolated and all remaining cleanup steps still execute;
  - event-to-first-overlay/window-call dispatch is recorded from monotonic timestamps and remains below 50 ms in the fake scheduler test.

- [ ] **Step 2: Run RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Application.Tests -c Debug --filter FullyQualifiedName~RespawnCoordinator --no-restore
  ```

- [ ] **Step 3: Implement the immutable effect ledger**

  ```csharp
  internal sealed record CycleEffects(
      RespawnCycleId CycleId,
      bool OverlayShown,
      bool WindowAttachRequested,
      bool WindowAttachVerified,
      NativeWindowHandle? DouyinWindow,
      bool PlayCommandSent,
      bool PlayAccepted,
      bool PlayVerifiedPlaying,
      bool CleanupStarted,
      bool CleanupCompleted);
  ```

  Serialize ledger changes with one `SemaphoreSlim`; do not hold it while awaiting external adapters. Each cycle owns a linked `CancellationTokenSource`. Capture Cycle ID before every await, then reacquire the lock and discard a result if the active ID changed. A discarded verified mutation is immediately routed through idempotent recovery for its original cycle.

- [ ] **Step 4: Map domain events to one-shot operations**

  The coordinator alone invokes more than one external adapter. It starts overlay display/update immediately, then executes an attachment branch only on the state machine's attachment event. After a verified Douyin window result it calls `PlaceAboveAsync`, then explicit Play. It never brings Douyin forward again in response to ordinary timer updates.

  Map `DeathConfirmed` to cycle creation, `DeadSampleUpdated` to clock re-anchor/overlay update, `AttachmentRequested` to the one-shot attachment branch, `RespawnConfirmed` to `CleanupReason.RespawnConfirmed`, `AbandonCycleDueToUnknown` to `CleanupReason.AbandonedUnknown`, `NoGameConfirmed` to `CleanupReason.GameExited`, and `TimelineResetRequested` to `CleanupReason.TimelineReset`. `LifeStateSynchronized` and connection-only events update status text but cannot manufacture death or respawn cleanup.

  Cleanup first cancels pending cycle work, then attempts explicit Pause when owned, Hide, window Restore, and optional one-shot game focus. Use independent try/catch blocks and structured results so one failure does not skip later steps. `PauseAutomationAsync` disables new cycles before cleanup; `ResumeAutomationAsync` re-synchronizes from the next valid Riot sample rather than replaying old state.

- [ ] **Step 5: Run GREEN and all pure tests**

  ```powershell
  dotnet test tests\RespawnSwitch.Application.Tests -c Debug --no-restore
  dotnet test tests\RespawnSwitch.Core.Tests -c Debug --no-restore
  ```

- [ ] **Step 6: Commit**

  ```powershell
  git add src\RespawnSwitch.Application\Coordination tests\RespawnSwitch.Application.Tests\Coordination
  git commit -m "feat(app): coordinate cancellable respawn cycles"
  ```

---

### Task 14: Implement the calibrated UI Automation fallback and Gate E

**Files:**

- Create: `src/RespawnSwitch.Windows/Media/IUiaGateway.cs`
- Create: `src/RespawnSwitch.Windows/Media/UiaSelector.cs`
- Create: `src/RespawnSwitch.Windows/Media/UiaAutomationWorker.cs`
- Create: `src/RespawnSwitch.Windows/Media/UiaCalibrationService.cs`
- Create: `src/RespawnSwitch.Windows/Media/UiaDouyinMediaController.cs`
- Test: `tests/RespawnSwitch.Windows.Tests/Media/UiaCalibrationServiceTests.cs`
- Test: `tests/RespawnSwitch.Windows.Tests/Media/UiaDouyinMediaControllerTests.cs`
- Test: `tests/RespawnSwitch.Desktop.IntegrationTests/Media/UiaWorkerIsolationIntegrationTests.cs`
- Create after evaluation: `docs/acceptance/uia-media-fallback.md`

**Interfaces:**

- Consumes: the verified Douyin identity, `UiaMediaProfile`, and locked media result types.
- Produces: a process-scoped, calibrated, idempotent fallback that cannot hang the coordinator or WPF Dispatcher.

- [ ] **Step 1: Write failing calibration, state, and timeout tests**

  Cover all four calibration quadrants:

  | Starting state | Command | Expected final state |
  |---|---|---|
  | Playing | Play | Playing without Toggle |
  | Paused | Play | Playing |
  | Paused | Pause | Paused without Toggle |
  | Playing | Pause | Paused |

  Reject a single unknown-state Toggle, localized visible text as the only selector, controls outside the verified Douyin process/window subtree, selector ambiguity, missing Invoke/Toggle pattern, unknown pre-state, wrong post-state, target rebuild, and selector-version drift. Assert timeout returns within the configured bound, the WPF Dispatcher probe still runs, a hung worker is quarantined, no second provider task is accepted, and no `Thread.Abort` call exists.

- [ ] **Step 2: Run RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Windows.Tests -c Debug --filter FullyQualifiedName~Uia --no-restore
  dotnet test tests\RespawnSwitch.Desktop.IntegrationTests -c Debug --filter FullyQualifiedName~UiaWorkerIsolation --no-restore
  ```

- [ ] **Step 3: Implement a single long-lived MTA worker**

  Set `<UseWPF>true</UseWPF>` in `RespawnSwitch.Windows.csproj` so it references the inbox Windows Desktop UI Automation assemblies without adding a package. Create one background `Thread`, call `SetApartmentState(ApartmentState.MTA)` before start, and consume a private work queue. UIA object references never escape that thread. The caller waits with a bounded timeout (default 750 ms) without blocking a WPF Dispatcher. If the provider call hangs, atomically mark the worker quarantined and permanently disable that UIA controller instance; do not abort the thread or create an unbounded replacement chain. At process exit do not wait indefinitely for a quarantined thread.

- [ ] **Step 4: Implement structural selection and idempotent commands**

  Search only beneath the already verified Douyin top-level HWND. Score exact AutomationId, ControlType, parent/ancestor structure, and supported patterns; visible text is diagnostic only. Enable a profile only when a reliable state property exists or distinct Play/Pause elements can prove state. Before any Toggle read state; if already at target return verified no-op, and if state is Unknown fail closed.

  `MediaControllerFactory` chooses the configured GSMTC profile first only if its probe is usable; otherwise it may choose an already calibrated UIA profile. It never silently calibrates during gameplay.

- [ ] **Step 5: Run GREEN and the hung-provider integration test**

  ```powershell
  dotnet test tests\RespawnSwitch.Windows.Tests -c Debug --no-restore
  dotnet test tests\RespawnSwitch.Desktop.IntegrationTests -c Debug --filter FullyQualifiedName~UiaWorkerIsolation --no-restore
  ```

- [ ] **Step 6: Execute Gate E when Gate B did not pass**

  If Gate B passed, write `docs/acceptance/uia-media-fallback.md` with `Required: false`, the Gate B evidence ID, and the automated UIA-test result. If Gate B did not pass, user action is required: open Douyin at a harmless playable page and perform the four starting-state/command combinations. Record the exact Douyin version/signature, structural selectors with sensitive text removed, pre/post state evidence, timeout behavior, and `Passed`. If neither Gate B nor Gate E passes, automatic Douyin Play/Pause remains disabled and the overall full-feature acceptance in Task 20 must fail clearly; never introduce global-key or coordinate fallbacks.

- [ ] **Step 7: Commit**

  ```powershell
  git add src\RespawnSwitch.Windows\Media tests\RespawnSwitch.Windows.Tests\Media tests\RespawnSwitch.Desktop.IntegrationTests\Media docs\acceptance\uia-media-fallback.md
  git commit -m "feat(windows): add calibrated UIA media fallback"
  ```

---

### Task 15: Add atomic settings, structured diagnostics, redaction, and retention

**Files:**

- Create: `src/RespawnSwitch.Application/Settings/AppSettings.cs`
- Create: `src/RespawnSwitch.Application/Settings/ISettingsStore.cs`
- Create: `src/RespawnSwitch.Application/Logging/DiagnosticContracts.cs`
- Create: `src/RespawnSwitch.Infrastructure/Settings/JsonSettingsStore.cs`
- Create: `src/RespawnSwitch.Infrastructure/Logging/JsonLinesDiagnosticLog.cs`
- Create: `src/RespawnSwitch.Infrastructure/Logging/DiagnosticRedactor.cs`
- Create: `src/RespawnSwitch.Infrastructure/Logging/LogRetentionPolicy.cs`
- Test: `tests/RespawnSwitch.Infrastructure.Tests/Settings/JsonSettingsStoreTests.cs`
- Test: `tests/RespawnSwitch.Infrastructure.Tests/Logging/DiagnosticRedactorTests.cs`
- Test: `tests/RespawnSwitch.Infrastructure.Tests/Logging/LogRetentionPolicyTests.cs`

**Interfaces:**

- Consumes: Task 10's atomic writer and the media/window profiles.
- Produces: versioned local settings plus bounded, redacted JSONL diagnostics used by UI and acceptance.

- [ ] **Step 1: Write failing round-trip, corruption, secret, and retention tests**

  `AppSettings` schema version 1 contains only: automation enabled, calibrated Douyin executable/signature/window class, optional GSMTC profile, optional UIA profile, overlay position/scale/opacity/background, hotkey modifiers/key, first-run completion, and evidence IDs. Test valid round-trip, default migration, unsupported future schema, truncated JSON recovery to a sibling suffixed `.corrupt-YYYYMMDDTHHMMSSfffZ`, atomic replacement, and validation ranges.

  Feed the redactor a full Riot ID, Riot JSON, bearer/basic tokens, cookies, command-line switches, user profile path, media title, and normal diagnostic codes. Assert sensitive values never appear in serialized output. Retention tests delete oldest complete log files until both constraints hold: age at most 7 days and aggregate size at most 20 MiB.

- [ ] **Step 2: Run RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Infrastructure.Tests -c Debug --filter "FullyQualifiedName~Settings|FullyQualifiedName~Logging" --no-restore
  ```

- [ ] **Step 3: Implement exact local paths and atomic settings**

  Use `%LOCALAPPDATA%\RespawnSwitch\settings.json`, `%LOCALAPPDATA%\RespawnSwitch\window-recovery.json`, and `%LOCALAPPDATA%\RespawnSwitch\logs\respawnswitch-YYYYMMDD.jsonl`. Validate opacity `[0.2,1]`, scale `[0.5,2]`, known hotkey modifiers, non-empty calibrated identity fields, and mutual availability of controller profiles. Invalid values produce typed validation failures; they are not silently clamped except when loading an older documented schema.

- [ ] **Step 4: Implement one-event-per-line diagnostics**

  ```csharp
  public sealed record DiagnosticEvent(
      DateTimeOffset TimestampUtc,
      long MonotonicTimestamp,
      string Component,
      string EventCode,
      string Severity,
      string? CycleId,
      double? DurationMilliseconds,
      IReadOnlyDictionary<string, string> Properties);
  ```

  Redact before serialization. A full Riot ID becomes a stable session-only mask such as `P***#***`; executable paths and titles become a SHA-256 prefix plus safe category. Never accept raw response bodies, cookies, credentials, or command lines as diagnostic properties. Logging failure is isolated from automation and reported in-memory to the tray status.

- [ ] **Step 5: Run GREEN**

  ```powershell
  dotnet test tests\RespawnSwitch.Infrastructure.Tests -c Debug --no-restore
  ```

- [ ] **Step 6: Commit**

  ```powershell
  git add src\RespawnSwitch.Application\Settings src\RespawnSwitch.Application\Logging src\RespawnSwitch.Infrastructure\Settings src\RespawnSwitch.Infrastructure\Logging tests\RespawnSwitch.Infrastructure.Tests
  git commit -m "feat(infrastructure): persist settings and redacted diagnostics"
  ```

---

### Task 16: Add one-shot focus recovery, hotkey handling, and desktop-session guards

**Files:**

- Create: `src/RespawnSwitch.Windows/Interop/WtsApi32.cs`
- Create: `src/RespawnSwitch.Windows/Windows/LeagueWindowController.cs`
- Create: `src/RespawnSwitch.Windows/Hotkeys/GlobalHotkeyService.cs`
- Create: `src/RespawnSwitch.Windows/Hotkeys/HotkeyGesture.cs`
- Create: `src/RespawnSwitch.Windows/Session/DesktopAutomationGuard.cs`
- Create: `src/RespawnSwitch.Windows/Session/DesktopSessionState.cs`
- Test: `tests/RespawnSwitch.Windows.Tests/Windows/LeagueWindowControllerTests.cs`
- Test: `tests/RespawnSwitch.Windows.Tests/Hotkeys/GlobalHotkeyServiceTests.cs`
- Test: `tests/RespawnSwitch.Windows.Tests/Session/DesktopAutomationGuardTests.cs`
- Test: `tests/RespawnSwitch.Desktop.IntegrationTests/Windows/FocusAndHotkeyIntegrationTests.cs`

**Interfaces:**

- Consumes: `ILeagueWindowController`, verified `GameWindowTarget`, tray/overlay prompts, and session events.
- Produces: one automatic focus attempt, one user-requested hotkey attempt, and fail-closed suspension on non-interactive desktops.

- [ ] **Step 1: Write failing focus, collision, and suspension tests**

  Assert:

  - already foreground → no native mutation;
  - otherwise `ShowWindow(SW_RESTORE)` once, `SetForegroundWindow` once, then `GetForegroundWindow` verification;
  - rejection → `FlashWindowEx` once and prompt, no retry loop;
  - hotkey receipt → one additional verified attempt only;
  - second failure → ManualReturnRequired text;
  - no call to `AttachThreadInput`, `SendInput`, Alt simulation, or game input exists;
  - default `Ctrl+Alt+F9` registration success and collision failure are surfaced;
  - changing gesture unregisters the prior ID before registering the new one;
  - lock, logoff, inaccessible input desktop, UAC secure desktop, or abnormal remote-session state suspends automation and cleans the active cycle;
  - return to the normal interactive desktop re-synchronizes from fresh Riot data rather than replaying a cycle.

- [ ] **Step 2: Run RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Windows.Tests -c Debug --filter "FullyQualifiedName~LeagueWindowController|FullyQualifiedName~Hotkey|FullyQualifiedName~DesktopAutomationGuard" --no-restore
  ```

- [ ] **Step 3: Implement foreground behavior exactly once**

  `TryRestoreFocusOnceAsync` first revalidates the game HWND using the Toolhelp/window-only identity route. It performs the documented sequence once and returns true only if `GetForegroundWindow` equals the game HWND. On false, call `FlashWindowEx` and show the 5-second hotkey prompt through the coordinator. Do not keep a timer that retries focus.

- [ ] **Step 4: Implement hotkey and session messages**

  Use `RegisterHotKey`/`UnregisterHotKey` against a RespawnSwitch-owned message HWND. Use `WTSRegisterSessionNotification` plus `WM_WTSSESSION_CHANGE`; supplement with an input-desktop accessibility probe before every window/UIA mutation. The guard exposes only `Interactive`, `Locked`, `Disconnected`, or `SecureOrUnknown`. Any state other than Interactive cancels external automation.

- [ ] **Step 5: Run GREEN and interactive integration**

  ```powershell
  dotnet test tests\RespawnSwitch.Windows.Tests -c Debug --no-restore
  dotnet test tests\RespawnSwitch.Desktop.IntegrationTests -c Debug --filter FullyQualifiedName~FocusAndHotkey --no-restore
  ```

  Foreground restrictions can legitimately deny the automatic request. The integration test passes only when it observes either verified focus success or the complete flash/hotkey/manual-prompt fallback; a bare `SetForegroundWindow` return value is insufficient.

- [ ] **Step 6: Commit**

  ```powershell
  git add src\RespawnSwitch.Windows\Interop\WtsApi32.cs src\RespawnSwitch.Windows\Windows\LeagueWindowController.cs src\RespawnSwitch.Windows\Hotkeys src\RespawnSwitch.Windows\Session tests\RespawnSwitch.Windows.Tests tests\RespawnSwitch.Desktop.IntegrationTests\Windows
  git commit -m "feat(windows): add safe focus and session controls"
  ```

---

### Task 17: Build first-run calibration, tray, settings, and diagnostics UI

**Files:**

- Create: `src/RespawnSwitch.Application/UserInterface/FirstRunWorkflow.cs`
- Create: `src/RespawnSwitch.Application/UserInterface/TrayMenuState.cs`
- Create: `src/RespawnSwitch.Application/UserInterface/SettingsValidator.cs`
- Create: `src/RespawnSwitch.App/Commands/AsyncCommand.cs`
- Create: `src/RespawnSwitch.App/FirstRun/FirstRunWindow.xaml`
- Create: `src/RespawnSwitch.App/FirstRun/FirstRunWindow.xaml.cs`
- Create: `src/RespawnSwitch.App/FirstRun/FirstRunViewModel.cs`
- Create: `src/RespawnSwitch.App/Settings/SettingsWindow.xaml`
- Create: `src/RespawnSwitch.App/Settings/SettingsWindow.xaml.cs`
- Create: `src/RespawnSwitch.App/Settings/SettingsViewModel.cs`
- Create: `src/RespawnSwitch.App/Diagnostics/DiagnosticsWindow.xaml`
- Create: `src/RespawnSwitch.App/Diagnostics/DiagnosticsWindow.xaml.cs`
- Create: `src/RespawnSwitch.App/Diagnostics/DiagnosticsViewModel.cs`
- Create: `src/RespawnSwitch.App/Tray/TrayIconController.cs`
- Create: `src/RespawnSwitch.App/Tray/TrayMenuState.cs`
- Test: `tests/RespawnSwitch.Application.Tests/Ui/FirstRunWorkflowTests.cs`
- Test: `tests/RespawnSwitch.Application.Tests/Ui/TrayMenuStateTests.cs`
- Test: `tests/RespawnSwitch.Application.Tests/Ui/SettingsValidationTests.cs`

**Interfaces:**

- Consumes: controller probes/calibration, settings, coordinator, overlay preview, hotkey service, and diagnostics.
- Produces: a Chinese-language user path that never enables Douyin window/media automation until target identity, one media method, overlay preview, and hotkey have verified successfully; monitor-only preview mode remains available when media calibration fails.

- [ ] **Step 1: Write failing view-model workflow tests**

  Model these ordered first-run stages: resolve/confirm Douyin executable; ask user to open a harmless video; enumerate and explicitly choose a unique GSMTC session; run four idempotence checks; choose/confirm one main Douyin window; if GSMTC fails, run the four UIA checks; preview overlay; register hotkey; save and minimize to tray. Users can move backward. `EnableDouyinAutomation` stays disabled until all target/media/window/hotkey stages verify; `CompleteInMonitorOnlyMode` remains available after overlay preview and hotkey registration, persists automation disabled, and explains that countdown preview/manual diagnostics still work.

  Add tests for no media method, ambiguous session/window, elevated-process mismatch, blocked login/update dialog, hotkey collision, pause/resume, manual simulated death/respawn, and all tray states: 未在对局, 存活, 阵亡, 连接异常, 自动化已暂停.

- [ ] **Step 2: Run RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Application.Tests -c Debug --filter "FullyQualifiedName~FirstRun|FullyQualifiedName~TrayMenu|FullyQualifiedName~SettingsValidation" --no-restore
  ```

- [ ] **Step 3: Implement the first-run and settings views**

  Keep code-behind limited to window ownership/activation and disposal; state and commands live in view models. Settings expose overlay position/scale/opacity/background, hotkey, calibrated controller diagnostics, and one-click pause. Do not include credentials, Douyin login automation, video selection, global media-key toggles, or coordinate capture.

  Diagnostics provides explicit buttons for: list media sessions, verify Play, verify Pause, resolve Douyin window, preview overlay, simulate local Dead/Alive events, copy redacted diagnostic summary, and open the log directory. Destructive or state-changing tests require a visible confirmation inside the diagnostic window and are never run on startup.

- [ ] **Step 4: Implement the tray on the WPF UI thread**

  Use `System.Windows.Forms.NotifyIcon`, create/dispose it on the Dispatcher thread, and set `Application.ShutdownMode="OnExplicitShutdown"`. Menu items are Status, Enable/Pause, Settings, Test Play/Pause, Preview Countdown, Simulate Death/Respawn, Open Logs, and Exit. Closing settings/diagnostics hides those windows; only Exit invokes application shutdown.

- [ ] **Step 5: Run GREEN and visual smoke checks**

  ```powershell
  dotnet test tests\RespawnSwitch.Application.Tests -c Debug --no-restore
  dotnet build src\RespawnSwitch.App -c Debug --no-restore
  dotnet run --project src\RespawnSwitch.App -c Debug -- --diagnostics-only
  ```

  Verify keyboard navigation within settings/diagnostics, clear Chinese error text, correct display at 100%/125%/150% DPI, and that the overlay itself remains non-focusable.

- [ ] **Step 6: Commit**

  ```powershell
  git add src\RespawnSwitch.Application\UserInterface src\RespawnSwitch.App\Commands src\RespawnSwitch.App\FirstRun src\RespawnSwitch.App\Settings src\RespawnSwitch.App\Diagnostics src\RespawnSwitch.App\Tray tests\RespawnSwitch.Application.Tests\Ui
  git commit -m "feat(app): add calibration tray and diagnostics UI"
  ```

---

### Task 18: Compose monitoring, startup recovery, and application lifecycle

**Files:**

- Modify: `src/RespawnSwitch.App/App.xaml`
- Replace: `src/RespawnSwitch.App/App.xaml.cs`
- Create: `src/RespawnSwitch.App/app.manifest`
- Create: `src/RespawnSwitch.App/Bootstrapper.cs`
- Create: `src/RespawnSwitch.App/Lifecycle/SingleInstanceGuard.cs`
- Create: `src/RespawnSwitch.App/Lifecycle/WpfExceptionBridge.cs`
- Create: `src/RespawnSwitch.Application/Lifecycle/AppLifetime.cs`
- Create: `src/RespawnSwitch.Application/Monitoring/LeagueMonitorService.cs`
- Create: `src/RespawnSwitch.Application/Recovery/StartupRecoveryService.cs`
- Create from Gate A evidence: `src/RespawnSwitch.App/Resources/verified-riot-semantics.json`
- Test: `tests/RespawnSwitch.Application.Tests/Lifecycle/AppLifetimeTests.cs`
- Test: `tests/RespawnSwitch.Application.Tests/Monitoring/LeagueMonitorServiceTests.cs`
- Test: `tests/RespawnSwitch.Application.Tests/Recovery/StartupRecoveryServiceTests.cs`
- Test: `tests/RespawnSwitch.Architecture.Tests/ProjectDependencyTests.cs`

**Interfaces:**

- Consumes: all prior ports/adapters and the Gate A evidence artifact.
- Produces: one ordinary-user tray process with explicit startup, monitoring, recovery, and bounded shutdown.

- [ ] **Step 1: Write failing composition and lifecycle tests**

  Cover: second instance exits without a second tray/monitor; absent/incomplete first-run settings open the wizard and keep automation disabled; valid settings start monitoring; a Riot sample flows through state machine to coordinator; pause stops new automation and cleans the active cycle; shutdown cancels monitor then cleans coordinator then disposes tray/hotkey/media/UIA resources; startup journal recovery runs before monitoring; unprovable identity gives manual instructions only; recoverable Dispatcher exception triggers bounded cleanup; forced process death is not falsely claimed recoverable.

  Architecture tests enforce this reference DAG: Core→none; Application→Core; Riot→Core+Application; Infrastructure→Core+Application; Windows→Core+Application; App→all. They also reject League process-handle APIs, global input APIs, `Thread.Abort`, and any production `DangerousAcceptAnyServerCertificateValidator`.

- [ ] **Step 2: Run RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Application.Tests -c Debug --filter "FullyQualifiedName~Lifecycle|FullyQualifiedName~LeagueMonitor|FullyQualifiedName~StartupRecovery" --no-restore
  dotnet test tests\RespawnSwitch.Architecture.Tests -c Debug --no-restore
  ```

- [ ] **Step 3: Add exact process and DPI manifest**

  ```xml
  <?xml version="1.0" encoding="utf-8"?>
  <assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
    <trustInfo xmlns="urn:schemas-microsoft-com:asm.v3">
      <security>
        <requestedPrivileges>
          <requestedExecutionLevel level="asInvoker" uiAccess="false" />
        </requestedPrivileges>
      </security>
    </trustInfo>
    <application xmlns="urn:schemas-microsoft-com:asm.v3">
      <windowsSettings>
        <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/PM</dpiAware>
        <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2,PerMonitor</dpiAwareness>
      </windowsSettings>
    </application>
  </assembly>
  ```

  Set `<ApplicationManifest>app.manifest</ApplicationManifest>`. Do not add package capabilities, requested administrator execution, or `uiAccess`.

- [ ] **Step 4: Generate and validate the verified semantics resource**

  Convert the committed Gate A report into immutable JSON with schema version, League patch, seconds-per-raw-unit, evidence ID, evidence SHA-256, and build commit. Fail the Release build if it is absent or `Passed` was not true. This is evidence for the validated build, not a claim that future patches remain compatible; diagnostics must show the embedded patch and require the semantic probe to be rerun before rebuilding for a new patch.

- [ ] **Step 5: Implement startup order and monitoring**

  Exact order:

  1. acquire per-user named mutex;
  2. initialize bounded diagnostics and settings;
  3. run startup recovery against any active journal;
  4. create tray/hotkey/session guard on the Dispatcher;
  5. run first-use calibration if needed;
  6. construct `LeagueGameProbe`, state machine, monotonic clock, and coordinator;
  7. start the monitor only when automation is enabled and the desktop is Interactive.

  `LeagueMonitorService` consumes `ILeagueGameProbe.WatchAsync`, feeds observations and presence checks to the state machine, and immediately sends every transition to the coordinator. Presence checks use Task 9's no-handle game probe. No adapter directly accesses a WPF view model or another adapter's internal state.

- [ ] **Step 6: Implement bounded cleanup**

  Normal Exit and user Pause await full idempotent cleanup. Recoverable Dispatcher exceptions stop new automation and attempt the same cleanup before showing a redacted error. Process termination, power loss, fail-fast, and uncatchable faults are documented as startup-recovery cases, not guaranteed synchronous cleanup. Dispose the tray icon and unregister the hotkey on every orderly exit.

- [ ] **Step 7: Run GREEN and full automated regression**

  ```powershell
  dotnet build RespawnSwitch.sln -c Debug --no-restore
  dotnet test tests\RespawnSwitch.Core.Tests -c Debug --no-build
  dotnet test tests\RespawnSwitch.Application.Tests -c Debug --no-build
  dotnet test tests\RespawnSwitch.Riot.Tests -c Debug --no-build
  dotnet test tests\RespawnSwitch.Infrastructure.Tests -c Debug --no-build
  dotnet test tests\RespawnSwitch.Windows.Tests -c Debug --no-build
  dotnet test tests\RespawnSwitch.Architecture.Tests -c Debug --no-build
  dotnet test tests\RespawnSwitch.Desktop.IntegrationTests -c Debug --no-build
  ```

- [ ] **Step 8: Commit**

  ```powershell
  git add src\RespawnSwitch.Application\Lifecycle src\RespawnSwitch.Application\Monitoring src\RespawnSwitch.Application\Recovery src\RespawnSwitch.App tests\RespawnSwitch.Application.Tests tests\RespawnSwitch.Architecture.Tests
  git commit -m "feat(app): compose monitoring recovery and lifecycle"
  ```

---

### Task 19: Produce and verify a self-contained Windows x64 directory release

**Files:**

- Modify: `src/RespawnSwitch.App/RespawnSwitch.App.csproj`
- Create: `src/RespawnSwitch.Application/Diagnostics/SelfTestCommand.cs`
- Create: `build/Publish-RespawnSwitch.ps1`
- Create: `build/Verify-RespawnSwitchPublish.ps1`
- Create: `docs/acceptance/clean-machine-publish.md`
- Test: `tests/RespawnSwitch.Application.Tests/Diagnostics/SelfTestCommandTests.cs`
- Test: `tests/RespawnSwitch.Architecture.Tests/PublishConfigurationTests.cs`

**Interfaces:**

- Consumes: the complete app, locked package graph, and all prior gate evidence.
- Produces: an untrimmed, non-single-file, self-contained `win-x64` folder plus checksums and a clean-machine smoke report.

- [ ] **Step 1: Write failing publish-policy and self-test tests**

  Assert the App project has `RuntimeIdentifier=win-x64`, `SelfContained=true`, `PublishSingleFile=false`, `PublishTrimmed=false`, `PlatformTarget=x64`, the versioned Windows TFM, `SupportedOSPlatformVersion=10.0.17763.0`, `OutputType=WinExe`, WPF and Windows Forms enabled, and the `asInvoker` manifest. Reject references to Windows App SDK/CsWinRT/Windows SDK Contracts packages.

  `SelfTestCommand` must verify resource loading, writable LocalApplicationData paths, settings/journal JSON serialization, log redaction, Dispatcher creation, overlay HWND style creation/hide, hotkey register/unregister or structured collision, and GSMTC access or structured unavailable status. It must never start League, start Douyin, send media commands, or mutate an external window.

- [ ] **Step 2: Run RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Application.Tests -c Debug --filter FullyQualifiedName~SelfTest --no-restore
  dotnet test tests\RespawnSwitch.Architecture.Tests -c Debug --filter FullyQualifiedName~PublishConfiguration --no-restore
  ```

- [ ] **Step 3: Lock the App publish properties**

  ```xml
  <PropertyGroup>
    <TargetFramework>net8.0-windows10.0.26100.0</TargetFramework>
    <SupportedOSPlatformVersion>10.0.17763.0</SupportedOSPlatformVersion>
    <OutputType>WinExe</OutputType>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <PlatformTarget>x64</PlatformTarget>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>false</PublishSingleFile>
    <PublishTrimmed>false</PublishTrimmed>
    <PublishReadyToRun>false</PublishReadyToRun>
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>
  ```

- [ ] **Step 4: Implement deterministic publish and verification scripts**

  `Publish-RespawnSwitch.ps1` accepts only `-Configuration Release` and an optional output root under `artifacts\publish`. It sets the sandbox-local CLI variables, requires a clean tracked worktree, verifies Gate A and at least one passed targeted media gate, runs locked restore plus all tests, publishes to a new directory whose final name is `RespawnSwitch-win-x64-` followed by the current short commit, generates `SHA256SUMS.txt` sorted by relative path, and creates a ZIP beside the folder. It atomically writes `artifacts\publish\latest-publish.json` with absolute `PublishDirectory`, `ArchivePath`, `Commit`, and both SHA-256 values. It never deletes an arbitrary caller path; if the exact versioned output already exists it stops.

  Its build sequence is exactly:

  ```powershell
  dotnet restore RespawnSwitch.sln --locked-mode
  dotnet build RespawnSwitch.sln -c Release --no-restore
  dotnet test RespawnSwitch.sln -c Release --no-build
  dotnet publish src\RespawnSwitch.App\RespawnSwitch.App.csproj `
    -c Release -r win-x64 --self-contained true --no-restore `
    -p:PublishSingleFile=false -p:PublishTrimmed=false -p:PublishReadyToRun=false `
    -o $versionedOutput
  ```

  `Verify-RespawnSwitchPublish.ps1` rejects missing EXE/DLL/runtimeconfig/deps files, verifies every SHA-256 entry, checks that no settings/log/recovery files were accidentally packaged, creates an absolute `$selfTestResult` path under `artifacts\self-test`, launches `RespawnSwitch.exe --self-test --result $selfTestResult`, requires exit code 0, validates the JSON schema, and confirms the process exits without a lingering tray icon.

- [ ] **Step 5: Run GREEN and publish locally**

  ```powershell
  dotnet test tests\RespawnSwitch.Application.Tests -c Release --filter FullyQualifiedName~SelfTest --no-restore
  dotnet test tests\RespawnSwitch.Architecture.Tests -c Release --no-restore
  powershell -NoProfile -ExecutionPolicy Bypass -File build\Publish-RespawnSwitch.ps1
  $publishResult = Get-Content artifacts\publish\latest-publish.json -Raw | ConvertFrom-Json
  powershell -NoProfile -ExecutionPolicy Bypass -File build\Verify-RespawnSwitchPublish.ps1 -PublishDirectory $publishResult.PublishDirectory
  ```

- [ ] **Step 6: Execute the clean-machine gate**

  Copy only the ZIP to a Windows 10 1809+ or Windows 11 x64 VM/Windows Sandbox with no separately installed .NET runtime. Keep the machine offline for the first launch. Extract, run `--self-test`, then open the UI. Verify ordinary-user launch, no runtime download, tray creation/disposal, settings/log paths, overlay preview, and a clear unavailable result for absent League/Douyin. Repeat the targeted media smoke on the real target machine using this exact published folder.

  Record OS build, architecture, whether .NET was absent, archive SHA-256, self-test JSON SHA-256, startup result, media result, and `Passed` in `docs/acceptance/clean-machine-publish.md`. Code signing and an installer are explicitly outside this local prototype; document that the unsigned archive may show SmartScreen warnings rather than bypassing them.

- [ ] **Step 7: Commit**

  ```powershell
  git add src\RespawnSwitch.Application\Diagnostics src\RespawnSwitch.App tests\RespawnSwitch.Application.Tests tests\RespawnSwitch.Architecture.Tests build docs\acceptance\clean-machine-publish.md
  git commit -m "build: add verified self-contained Windows publish"
  ```

---

### Task 20: Run full Practice Tool acceptance, ten cycles, performance, and no-handle Gates F/G

**Files:**

- Create: `src/RespawnSwitch.Application/Logging/AcceptanceMetric.cs`
- Create: `tools/RespawnSwitch.SemanticProbe/AcceptanceReportAnalyzer.cs`
- Test: `tests/RespawnSwitch.Application.Tests/Logging/AcceptanceMetricTests.cs`
- Test: `tests/RespawnSwitch.Riot.Tests/SemanticProbe/AcceptanceReportAnalyzerTests.cs`
- Create: `docs/acceptance/practice-tool-end-to-end.md`
- Create: `docs/acceptance/no-league-process-handle.md`
- Create: `docs/acceptance/release-candidate-checklist.md`

**Interfaces:**

- Consumes: the exact Task 19 published folder and every prior real-environment report.
- Produces: quantitative Gate F evidence for the complete flow and independent Gate G evidence that the League process boundary was preserved.

- [ ] **Step 1: Write failing metric and report-validation tests**

  The analyzer rejects fewer than 10 complete cycles, duplicated Cycle IDs, a probe failure interpreted as respawn, missing cleanup, residual owned Topmost, media commands aimed at another session, countdown error above one second, or missing performance samples. Compute percentiles with the nearest-rank method and assert:

  - successful Riot sample interval P95 ≤ 400 ms and P99 ≤ 500 ms during API-online, non-timeout, non-overloaded windows;
  - completed HTTP response carrying changed `isDead` → state event P95 < 50 ms;
  - state event → overlay first frame/window action invocation P95 < 50 ms;
  - visible game countdown vs overlay absolute error ≤ 1 second;
  - observed screen-to-action latency is recorded, with 500 ms as an observation target rather than a Riot SLA.

  Resource stabilization after cycle 10 must return to at most `max(baseline × 1.10, baseline + 20)` process handles, at most baseline + 2 non-quarantined threads, no active cycle CTS, no active recovery journal, and no RespawnSwitch-owned Topmost on Douyin.

- [ ] **Step 2: Run RED, implement the analyzer, then run GREEN**

  ```powershell
  dotnet test tests\RespawnSwitch.Application.Tests -c Debug --filter FullyQualifiedName~AcceptanceMetric --no-restore
  dotnet test tests\RespawnSwitch.Riot.Tests -c Debug --filter FullyQualifiedName~AcceptanceReportAnalyzer --no-restore
  ```

  Implement typed JSON input, monotonic-duration calculations, explicit excluded-window reasons, nearest-rank percentiles, and a failure code for every threshold. Do not compute correctness by comparing the overlay only with the same API field; the timer-error series comes from synchronized external video/operator markers.

  ```powershell
  dotnet test tests\RespawnSwitch.Application.Tests -c Debug --filter FullyQualifiedName~AcceptanceMetric --no-restore
  dotnet test tests\RespawnSwitch.Riot.Tests -c Debug --filter FullyQualifiedName~AcceptanceReportAnalyzer --no-restore
  ```

- [ ] **Step 3: Establish the exact release candidate and safe setup**

  Verify archive/hash parity with Task 19, Gate A's current patch, Gate B or E, Gate C, and Gate D. Copy `artifacts\publish\latest-publish.json` to `artifacts\acceptance\gate-f-release-candidate.json` before the run and do not overwrite it during acceptance. User action required: start League Practice Tool in borderless mode and place Douyin on a harmless playable video. Do not run new builds first in ranked play. Do not alter the real port 2999, terminate Riot services, change firewall/proxy rules, or run fault injection against the live client.

- [ ] **Step 4: Execute ten complete death/respawn cycles**

  Capture redacted application JSONL and synchronized screen recording/operator markers. Across the ten cycles include:

  - Douyin initially hidden/absent, minimized, maximized, normal, and already Topmost;
  - timer `>=2`, exactly-at-boundary when reproducible, `<2`, and delayed valid timer attachment;
  - one manual media pause, one manual return to game, one Douyin close, one user move/resize, and one target HWND rebuild if safely reproducible;
  - training pause, reset, fast respawn, one naturally observed media failure if it occurs, and one hotkey fallback;
  - Spotify or browser media playing concurrently to prove no unrelated session is controlled.

  Each cycle must record its ID, death/respawn truth markers, sample/event/action timestamps, media pre/post state, overlay frames, window original/target/restored fields, focus outcome, cleanup reason, residual journal/Topmost, handle/thread counts, and pass/failure codes.

  After—not as a substitute for—the ten release-candidate cycles, run separate component acceptance with the injected HTTP handler for timeout/disconnect/schema corruption, a forced media failure, a hotkey collision, and startup recovery after killing a RespawnSwitch-owned TestWindowHost mutation. These tests may use the instrumented test build and must be labeled separately from real-client evidence.

- [ ] **Step 5: Analyze and decide Gate F**

  Run the analyzer against the captured JSONL plus operator-marker file and paste its deterministic summary into `docs/acceptance/practice-tool-end-to-end.md`. Gate F passes only when all ten cycles complete, no interface failure is called respawn, at least one targeted media controller verifies Play and Pause, window/overlay/focus fallbacks honor their contracts, all cleanup ownership checks pass, and every quantitative threshold above passes.

- [ ] **Step 6: Execute the independent League-handle audit for Gate G**

  First run the architecture tests. Then, while the exact release candidate monitors Practice Tool, capture RespawnSwitch process-handle activity with a current trusted Sysinternals/ETW tool. Filter source process to `RespawnSwitch.exe` and target to the actual `League of Legends.exe` PID. Keep the capture through startup, active monitoring, death, respawn, pause, resume, and exit. Also take beginning/end handle-table snapshots.

  `docs/acceptance/no-league-process-handle.md` records tool name/version/source, filters, timestamps, RespawnSwitch and League PIDs, capture hash, and results. Gate G passes only if code/IL audit finds no forbidden API and the live trace/snapshots show zero process handles opened by RespawnSwitch toward the League PID. If the audit tool itself requires elevation, elevate only the audit tool with the user's approval; RespawnSwitch remains ordinary `asInvoker`.

- [ ] **Step 7: Verify orderly exit and crash-recovery boundaries**

  Run one normal Exit, one Pause, one recoverable test exception, and one controlled forced termination while a RespawnSwitch-owned TestWindowHost—not real Douyin—is mutated. Verify normal/recoverable paths clean synchronously; the forced path leaves a journal and the next launch restores only still-owned fields. Do not claim guaranteed recovery after power loss or an uncatchable crash.

- [ ] **Step 8: Commit evidence and Gate F/G decision**

  ```powershell
  git add src\RespawnSwitch.Application\Logging tools\RespawnSwitch.SemanticProbe tests\RespawnSwitch.Application.Tests tests\RespawnSwitch.Riot.Tests docs\acceptance
  git commit -m "test: record RespawnSwitch practice tool acceptance"
  ```

  If either gate fails, keep the build internal and return to the responsible task; do not weaken a threshold or safety boundary to mark it passed.

---

### Task 21: Validate ordinary non-ranked play and enforce the distribution policy Gate H

**Files:**

- Create: `docs/acceptance/normal-non-ranked-readonly.md`
- Create: `docs/policy/distribution-gate.md`
- Create: `docs/support/mode-support.md`
- Modify: `docs/acceptance/release-candidate-checklist.md`
- Modify: `README.md`
- Test: `tests/RespawnSwitch.Architecture.Tests/DocumentationGateTests.cs`

**Interfaces:**

- Consumes: the exact Gate F/G release candidate and current official Riot policy pages read live during execution.
- Produces: an internal-prototype support decision and a separate, explicit block or approval record for any future distribution.

- [ ] **Step 1: Write failing documentation-gate tests**

  Add tests that locate the repository root from the test assembly and require:

  - all three Task 21 documents plus the release-candidate checklist;
  - `DistributionAllowed: false` as the default and a non-empty current-policy access date/source list;
  - Training Tool and ordinary Summoner's Rift non-ranked support backed by named passing evidence hashes;
  - Ranked, ARAM/rotating modes, replay, spectator, and unvalidated special mechanics never marked Supported;
  - README labels the artifact a local internal prototype and links the support/distribution documents;
  - every acceptance document referenced by the checklist exists and contains BuildCommit, EvidenceSha256, and Passed fields.

- [ ] **Step 2: Run RED**

  ```powershell
  $env:DOTNET_CLI_HOME = Join-Path $PWD 'work\dotnet-home'
  $env:NUGET_PACKAGES = Join-Path $PWD 'work\nuget-packages'
  $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

  dotnet test tests\RespawnSwitch.Architecture.Tests -c Debug --filter FullyQualifiedName~DocumentationGate --no-restore
  ```

  Expected: the new tests fail because Task 21 evidence and policy documents do not yet exist.

- [ ] **Step 3: Verify the frozen artifact and run one ordinary Summoner's Rift non-ranked read-only acceptance**

  ```powershell
  $releaseCandidate = Get-Content artifacts\acceptance\gate-f-release-candidate.json -Raw | ConvertFrom-Json
  powershell -NoProfile -ExecutionPolicy Bypass -File build\Verify-RespawnSwitchPublish.ps1 -PublishDirectory $releaseCandidate.PublishDirectory
  ```

  User action required: enter an ordinary non-ranked match with the validated build; do not use ranked play for first validation. Capture only the local player's read-only endpoint fields and redacted lifecycle metrics. Verify exact Riot ID selection, Alive→Dead→Alive, timer semantics/countdown error, targeted Douyin Play/Pause, overlay, restore/focus behavior, cleanup, and no other-player/private-state feature. Do not automate game input or publish chat.

  `docs/acceptance/normal-non-ranked-readonly.md` records patch, queue/mode, build/archive hashes, one complete cycle, deviations from Practice Tool, cleanup, and `Passed`. ARAM, rotating modes, replay, spectator, and untested special-respawn mechanics remain Experimental or Unsupported in `docs/support/mode-support.md`.

- [ ] **Step 4: Decide the local internal-prototype gate**

  The prototype is locally complete only if Gates A–G, the clean-machine gate, and the ordinary non-ranked read-only check all pass. Update the release-candidate checklist with exact evidence hashes. A local pass authorizes use only on this target machine under the tested conditions; it does not authorize sharing the ZIP.

- [ ] **Step 5: Refresh current official Riot distribution requirements**

  Browse and record the access date and exact current requirements from:

  ```text
  https://developer.riotgames.com/docs/lol
  https://developer.riotgames.com/policies/general
  https://developer.riotgames.com/docs/faqs#vanguard
  ```

  Determine current product-registration/review requirements, permitted use of the local endpoint, privacy/disclosure duties, required non-endorsement wording, and whether closed testing counts as distribution. Quote only the minimum necessary exact policy text and link the official source. Do not reuse cached disclaimer wording from the design spec.

- [ ] **Step 6: Enforce the external-submission boundary**

  `docs/policy/distribution-gate.md` starts with `DistributionAllowed: false`. It may change only after the user explicitly decides to distribute, reviews the exact current submission payload, and confirms immediately before any Developer Portal registration/review submission. Prepare documentation if requested, but do not submit a registration, accept terms, share a build, or claim Riot approval automatically.

  Any future distributable build must include the then-current required non-endorsement statement in First Run/About, purpose/endpoints, local-data/logging disclosure, privacy information, support modes, and known limitations. If policy status is uncertain, Gate H remains closed.

- [ ] **Step 7: Run GREEN, then the final automated and artifact verification**

  ```powershell
  dotnet restore RespawnSwitch.sln --locked-mode
  dotnet build RespawnSwitch.sln -c Release --no-restore
  dotnet test RespawnSwitch.sln -c Release --no-build
  powershell -NoProfile -ExecutionPolicy Bypass -File build\Verify-RespawnSwitchPublish.ps1 -PublishDirectory $releaseCandidate.PublishDirectory
  ```

  Expected: `DocumentationGateTests`, the full solution, and frozen artifact verification all pass. If documentation tests reveal unsupported claims or missing hashes, correct the document/evidence—not the assertion that keeps distribution closed.

- [ ] **Step 8: Commit the internal acceptance and distribution status**

  ```powershell
  git add README.md docs\acceptance docs\policy docs\support tests\RespawnSwitch.Architecture.Tests\DocumentationGateTests.cs
  git commit -m "docs: record supported modes and distribution gate"
  ```

---

## Part 2 Completion Verification

Before describing the program as complete, verify all of the following from fresh output:

```powershell
$env:DOTNET_CLI_HOME = Join-Path $PWD 'work\dotnet-home'
$env:NUGET_PACKAGES = Join-Path $PWD 'work\nuget-packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

dotnet restore RespawnSwitch.sln --locked-mode
dotnet build RespawnSwitch.sln -c Release --no-restore
dotnet test RespawnSwitch.sln -c Release --no-build
rg -n "OpenProcess|QueryFullProcessImageName|AttachThreadInput|SendInput|Thread\.Abort|DangerousAcceptAnyServerCertificateValidator|VK_MEDIA_PLAY_PAUSE" src
git diff --check
git status --short
```

The forbidden-API grep must have no production implementation match; declaration-name tests or explanatory comments must be reviewed individually. Completion additionally requires fresh passing evidence for Gate A, Gate B or E, Gate C, Gate D, Gate F, Gate G, clean-machine publishing, and ordinary non-ranked read-only validation. `DistributionAllowed: false` is an acceptable and expected final state for the local prototype; it must not be misreported as public-release approval.
