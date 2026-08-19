<!-- Author: Stress Monster -->
# RespawnSwitch 0.3.6 Reliable Switching and Desktop Pet Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate late Play/Pause/window races and replace the panel-like main window with a transparent edge-docking Q-style catgirl desktop pet.

**Architecture:** A per-`RespawnCycleId` runtime becomes the single owner of media, window and cleanup state; return-to-League sets an irreversible terminal intent, cancels entry, joins it, then executes verified cleanup in order. The main WPF window becomes a transparent pet shell with pure dock geometry, a hover/click speech panel and persisted placement, while the existing death overlay remains independent.

**Tech Stack:** .NET 8, C# 12, WPF, Win32, Windows GSMTC, local Chrome/Edge bridge, xUnit, ImageGen, PowerShell release packaging.

**Spec:** `docs/superpowers/specs/2026-08-19-respawnswitch-cycle-and-desktop-pet-design.md`

## Global Constraints

- Target Windows x64 with `net8.0-windows10.0.26100.0`; retain `SupportedOSPlatformVersion=10.0.17763.0`.
- Keep self-contained multi-file publishing; do not enable trimming or single-file publishing.
- Do not read League memory, inject, simulate keyboard/mouse, send space, or use global media keys.
- Desktop Douyin control must remain exact-profile GSMTC; web control must remain unique visible `douyin.com` video through the localhost bridge.
- Preserve the existing one-time Riot respawn anchor, local monotonic countdown, KDA/hero overlay, League-only audio lease and borderless window behavior.
- New assets, source files, package metadata and manuals retain the discoverable `Stress Monster` signature.
- No global input hooks are added to RespawnSwitch.
- Every production behavior change begins with a test observed failing for the intended reason.
- User has selected inline execution and requested no additional approval checkpoints.
- Live verification gets one bounded attempt only; if a real League match or exact media target is unavailable, record and skip it without repeated retries or extra setup.

---

### Task 1: Eventual GSMTC Pause Verification

**Files:**
- Create: `src/RespawnSwitch.Windows/Media/IMediaVerificationDelay.cs`
- Modify: `src/RespawnSwitch.Windows/Media/GsmtcDouyinMediaController.cs`
- Modify: `tests/RespawnSwitch.Windows.Tests/Media/GsmtcDouyinMediaControllerTests.cs`

**Interfaces:**
- Consumes: existing `IGsmtcGateway`, `GsmtcMediaProfile`, `MediaCommandResult`.
- Produces: internal `IMediaVerificationDelay.WaitAsync(TimeSpan delay, CancellationToken token)` and a controller that polls accepted commands and retries explicit Pause once without ever toggling.

- [ ] **Step 1: Add failing eventual-state tests**

Add literal scripted gateway cases:

```csharp
[Fact]
public async Task PauseAsync_WaitsUntilAcceptedCommandActuallyBecomesPaused()
{
    var gateway = new FakeGateway(
        enumerations: [[Session("stable")], [Session("stable")], [Session("stable")], [Session("stable")]],
        states: [PlaybackState.Playing, PlaybackState.Playing, PlaybackState.Paused]);
    var delay = new RecordingVerificationDelay();

    var result = await new GsmtcDouyinMediaController(Profile, gateway, delay)
        .PauseAsync(CancellationToken.None);

    Assert.True(result.StateVerified);
    Assert.Equal(PlaybackState.Paused, result.FinalState);
    Assert.Equal(1, gateway.PauseCalls);
    Assert.Single(delay.Delays);
}
```

Add a second test whose first verification window stays `Playing`, whose re-resolved exact session accepts a second explicit Pause and then becomes `Paused`; assert exactly two Pause calls and zero Play calls. This catches removal of the retry branch or accidental toggle behavior.

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

```powershell
dotnet test tests\RespawnSwitch.Windows.Tests\RespawnSwitch.Windows.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~GsmtcDouyinMediaControllerTests
```

Expected: compile failure because the delay-aware constructor and `RecordingVerificationDelay` contract do not exist.

- [ ] **Step 3: Implement bounded polling and one Pause retry**

Create:

```csharp
internal interface IMediaVerificationDelay
{
    ValueTask WaitAsync(TimeSpan delay, CancellationToken cancellationToken);
}

internal sealed class SystemMediaVerificationDelay : IMediaVerificationDelay
{
    public async ValueTask WaitAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
}
```

Extend the internal controller constructor to accept this dependency. After an accepted command, reselect the same exact profile and read state up to four times using literal delays `40ms, 70ms, 110ms`; reject a changed session token. For `Paused` only, if the first window does not verify, reselect the same unique profile, call `TryPauseAsync` once more and use a second `70ms, 120ms, 180ms` window. Return the existing `MediaCommandResult` shape with the final verified state or `gsmtc-final-state-mismatch`.

- [ ] **Step 4: Run focused and complete Windows tests**

Run the focused command, then:

```powershell
dotnet test tests\RespawnSwitch.Windows.Tests\RespawnSwitch.Windows.Tests.csproj -c Release --no-restore
```

Expected: all Windows tests pass and no warning is emitted.

- [ ] **Step 5: Commit Task 1**

```powershell
git add src/RespawnSwitch.Windows/Media tests/RespawnSwitch.Windows.Tests/Media/GsmtcDouyinMediaControllerTests.cs
git commit -m "fix: verify delayed Douyin pause state"
```

---

### Task 2: Per-Cycle Terminal Runtime

**Files:**
- Create: `src/RespawnSwitch.App/Cycles/RespawnCycleRuntime.cs`
- Create: `src/RespawnSwitch.App/Cycles/RespawnCycleStage.cs`
- Create: `tests/RespawnSwitch.Desktop.IntegrationTests/Coordinator/RespawnCycleRuntimeTests.cs`
- Modify: `src/RespawnSwitch.App/RespawnCycleRunner.cs`
- Modify: `tests/RespawnSwitch.Desktop.IntegrationTests/Coordinator/RespawnCycleRunnerTests.cs`

**Interfaces:**
- Consumes: `RespawnCycleId`, `GameWindowTarget`, cancellation tokens.
- Produces: `RespawnCycleRuntime.StartEnter(Func<RespawnCycleRuntime,CancellationToken,Task>)`, `RequestReturnAsync()`, `TryCommit(Action<RespawnCycleRuntime>)`, `ReturnOnceAsync(Func<RespawnCycleRuntime,Task>)`, terminal `RespawnCycleStage` and per-cycle resource properties.

- [ ] **Step 1: Add failing race tests**

Write three tests with real `TaskCompletionSource` synchronization:

```csharp
[Fact]
public async Task Return_rejects_a_late_enter_commit()
{
    var runtime = NewRuntime();
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    runtime.StartEnter(async (cycle, token) =>
    {
        entered.SetResult();
        await release.Task;
        Assert.False(cycle.TryCommit(c => c.DesktopAttached = true));
    });

    await entered.Task;
    var returning = runtime.ReturnOnceAsync(_ => Task.CompletedTask);
    release.SetResult();
    await returning;

    Assert.Equal(RespawnCycleStage.Completed, runtime.Stage);
    Assert.False(runtime.DesktopAttached);
}
```

Add `Return_waits_for_enter_cleanup_instead_of_abandoning_the_task` and `Duplicate_return_executes_cleanup_once`. Mutating `TryCommit` to ignore `ReturningToLeague`, or removing the enter-task join, must fail at least one test.

Add a runner test where work ignores cancellation longer than the old 750ms boundary; assert it remains tracked until it really completes. This catches the old `active.TryRemove` timeout behavior.

- [ ] **Step 2: Run and verify RED**

```powershell
dotnet test tests\RespawnSwitch.Desktop.IntegrationTests\RespawnSwitch.Desktop.IntegrationTests.csproj -c Release --no-restore --filter "FullyQualifiedName~RespawnCycleRuntimeTests|FullyQualifiedName~RespawnCycleRunnerTests"
```

Expected: compile failure for missing runtime and behavior failure for the runner timeout case.

- [ ] **Step 3: Implement the minimal terminal runtime**

Use one private lock for stage/resource commits, an enter CTS linked to app shutdown, one stored enter task, and one stored return task. `RequestReturn` changes the stage to `ReturningToLeague` before cancellation. `TryCommit` succeeds only in `EnteringDouyin`. `ReturnOnceAsync` cancels, awaits the real enter task, runs cleanup once and finally sets `Completed`. The runner must never remove a task merely because a caller's wait budget expired; a timeout returns control but leaves the actual task tracked until its own `finally` removes it.

- [ ] **Step 4: Run focused and desktop integration tests**

Run the focused command and then the full Desktop Integration project. Expected: all pass.

- [ ] **Step 5: Commit Task 2**

```powershell
git add src/RespawnSwitch.App/Cycles src/RespawnSwitch.App/RespawnCycleRunner.cs tests/RespawnSwitch.Desktop.IntegrationTests/Coordinator
git commit -m "feat: serialize each respawn switch cycle"
```

---

### Task 3: Cycle-Owned Desktop and Web Return Sequence

**Files:**
- Create: `src/RespawnSwitch.App/Cycles/DouyinCycleResources.cs`
- Create: `src/RespawnSwitch.App/Cycles/RespawnReturnResult.cs`
- Create: `tests/RespawnSwitch.Desktop.IntegrationTests/Coordinator/RespawnReturnSequenceTests.cs`
- Modify: `src/RespawnSwitch.App/RespawnCoordinator.cs`
- Modify: `src/RespawnSwitch.App/Browser/BrowserBridgeState.cs`
- Modify: `src/RespawnSwitch.App/Browser/BrowserBridgeServer.cs`
- Modify: `browser-extension/service-worker.js`
- Modify: `tests/RespawnSwitch.Desktop.IntegrationTests/Browser/BrowserBridgeStateTests.cs`

**Interfaces:**
- Consumes: Task 1 verified media controller and Task 2 runtime.
- Produces: per-cycle `DouyinCycleResources` containing exact media, target mode, desktop attachment and web command state; `RespawnReturnResult` with separate Pause/window/focus/audio outcomes; browser commands identified by cycle and sequence.

- [ ] **Step 1: Add failing coordinator-order tests**

Create a real return-sequence component with fake external boundaries and observable outcomes, not assertions on mock existence. Required tests:

```csharp
[Fact]
public async Task Return_pauses_before_window_restore_focus_and_audio_release()
{
    var log = new List<string>();
    var sequence = NewSequence(log, pauseVerified: true);

    var result = await sequence.ExecuteAsync(CancellationToken.None);

    Assert.Equal(["pause", "restore-douyin", "focus-league", "restore-audio"], log);
    Assert.True(result.PauseVerified);
}
```

Also test: Pause failure still performs all later cleanup; a late Play completion after return cannot commit; two cycles use different media targets; browser Pause supersedes an unconfirmed Play and stale Publish is ignored.

- [ ] **Step 2: Run and verify RED**

```powershell
dotnet test tests\RespawnSwitch.Desktop.IntegrationTests\RespawnSwitch.Desktop.IntegrationTests.csproj -c Release --no-restore --filter "FullyQualifiedName~RespawnReturnSequenceTests|FullyQualifiedName~BrowserBridgeStateTests"
```

Expected: compile failure for new cycle resources/return sequence and behavior failure because browser commands have no cycle identity.

- [ ] **Step 3: Implement cycle-owned resources and ordered cleanup**

Move the shared `media`, `desktopCycles` and `webCycles` state into the active runtime. Store a desktop media controller before Play begins. On `RespawnConfirmed`, hide the overlay immediately, then await the runtime's one return task. Cleanup catches each step independently and continues in exact order. Audio restoration remains after League focus as explicitly required.

Replace the fire-and-forget `CompleteRespawnAsync` call with an awaited, idempotent per-cycle return. Every continuation after Attach, Play, media discovery or settings save must use `TryCommit`; a rejected commit invokes compensation cleanup and never reports Watching.

Change browser records to:

```csharp
public sealed record BrowserCommand(Guid CycleId, long Sequence, string Command);
public sealed record BrowserCommandResult(Guid CycleId, long Sequence, bool Ok, string State, string Browser, int TabCount, string ErrorCode);
```

The server and extension must echo `cycleId`. `Publish` accepts only exact cycle and sequence. Issuing Pause for a cycle supersedes that cycle's Play even if Play has not acknowledged.

- [ ] **Step 4: Run coordinator, browser and full solution tests**

Run focused tests, then:

```powershell
dotnet build RespawnSwitch.sln -c Release --no-restore
dotnet test RespawnSwitch.sln -c Release --no-build --no-restore
```

Expected: zero warnings/errors and all tests pass.

- [ ] **Step 5: Commit Task 3**

```powershell
git add src/RespawnSwitch.App browser-extension tests/RespawnSwitch.Desktop.IntegrationTests
git commit -m "fix: make Douyin return cycle-owned and ordered"
```

---

### Task 4: Pure Dock Geometry and Persisted Pet Settings

**Files:**
- Create: `src/RespawnSwitch.Application/Pet/PetDockGeometry.cs`
- Create: `src/RespawnSwitch.Application/Pet/PetDockState.cs`
- Create: `tests/RespawnSwitch.Application.Tests/Pet/PetDockGeometryTests.cs`
- Modify: `src/RespawnSwitch.App/AppSettings.cs`
- Modify: `tests/RespawnSwitch.Desktop.IntegrationTests/Settings/AppSettingsMigrationTests.cs`

**Interfaces:**
- Produces: `PetDockGeometry.Snap(PixelRect workArea, PixelRect window, int threshold)`, `PetDockGeometry.PlacePeek(...)`, `PetDockState(Edge, Offset, IsPinned, Scale)` and backward-compatible settings defaults.

- [ ] **Step 1: Add failing hand-derived geometry and migration tests**

Use literal expected rectangles for left/right/top/bottom and a second monitor whose work area does not start at `(0,0)`. Add a settings test loading a 0.3.5 JSON document with no pet fields and assert safe defaults: right edge, 120px offset, not pinned, scale 1.0.

- [ ] **Step 2: Run and verify RED**

```powershell
dotnet test tests\RespawnSwitch.Application.Tests\RespawnSwitch.Application.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~PetDockGeometryTests
dotnet test tests\RespawnSwitch.Desktop.IntegrationTests\RespawnSwitch.Desktop.IntegrationTests.csproj -c Release --no-restore --filter FullyQualifiedName~AppSettingsMigrationTests
```

Expected: compile failures for missing pet types/settings fields.

- [ ] **Step 3: Implement pure geometry and compatible settings**

Select the nearest edge only when distance is within the supplied threshold; clamp offsets to the selected monitor work area. Persist only edge, offset, pinned and scale. Do not persist absolute coordinates as the authoritative state because monitors can be disconnected.

- [ ] **Step 4: Run focused and Application tests**

Expected: all pass.

- [ ] **Step 5: Commit Task 4**

```powershell
git add src/RespawnSwitch.Application/Pet src/RespawnSwitch.App/AppSettings.cs tests/RespawnSwitch.Application.Tests/Pet tests/RespawnSwitch.Desktop.IntegrationTests/Settings
git commit -m "feat: add desktop pet docking state"
```

---

### Task 5: Transparent Q-Style Catgirl Desktop Pet UI

**Files:**
- Create: `src/RespawnSwitch.App/Assets/stress-monster-pet-idle.png`
- Create: `src/RespawnSwitch.App/Assets/stress-monster-pet-blink.png`
- Create: `src/RespawnSwitch.App/Assets/README.md`
- Create: `src/RespawnSwitch.App/Pet/PetPresentationState.cs`
- Create: `src/RespawnSwitch.App/Pet/PetWindowController.cs`
- Modify: `src/RespawnSwitch.App/MainWindow.xaml`
- Modify: `src/RespawnSwitch.App/MainWindow.xaml.cs`
- Modify: `src/RespawnSwitch.App/App.xaml`
- Modify: `src/RespawnSwitch.App/RespawnSwitch.App.csproj`
- Modify: `tests/RespawnSwitch.Desktop.IntegrationTests/Ui/MainWindowPresentationTests.cs`
- Modify: `tests/RespawnSwitch.Desktop.IntegrationTests/Ui/MainWindowSmokeTests.cs`
- Create: `tests/RespawnSwitch.Desktop.IntegrationTests/Ui/PetWindowControllerTests.cs`

**Interfaces:**
- Consumes: Task 4 dock state and existing readiness/runtime status callbacks.
- Produces: a transparent taskbar-hidden pet, hover/click bubble, edge peek, drag/snap, simple status states, tray actions and unchanged overlay preview.

- [ ] **Step 1: Add failing behavioral UI tests**

Instantiate the real WPF window on STA and assert consumer-visible behavior: transparent background, no standard chrome, taskbar hidden, compact dimensions, bubble collapsed initially, bubble visible after `ShowPetPanel`, and state projection maps technical messages to `准备中/可以开局/正在对局/需要处理`. Add controller tests for hover-delay expansion and leave-delay collapse using an injected timer scheduler. Assert buttons remain discoverable by names and existing handlers can execute.

- [ ] **Step 2: Run and verify RED**

```powershell
dotnet test tests\RespawnSwitch.Desktop.IntegrationTests\RespawnSwitch.Desktop.IntegrationTests.csproj -c Release --no-restore --filter "FullyQualifiedName~MainWindow|FullyQualifiedName~PetWindowControllerTests"
```

Expected: behavioral failures against the current 930×650 opaque panel.

- [ ] **Step 3: Generate and inspect original pet assets**

Use the `imagegen` skill to create one original transparent-background Q-style adult catgirl mascot in a navy hoodie with mint/pink accents, full silhouette, readable at 260–320px and not based on a named copyrighted character. Create a blink variant by editing only the eyes. Inspect both images with `view_image`; reject visible background rectangles, clipped ears/tail, inconsistent pose or unreadable small-scale silhouette. Record `Author: Stress Monster`, generation date and intended use in `Assets/README.md`.

- [ ] **Step 4: Implement the transparent pet shell**

Replace the large grid with a 330×390 transparent `WindowStyle=None`, `AllowsTransparency=True`, `ShowInTaskbar=False` shell. The character is the hit area; a compact status bead is always visible, while a rounded speech panel contains League status, Douyin status, source selector and three plain-language actions. Use idle float and blink animations only.

Add drag, snap, edge-peek, hover/click expansion, saved scale/position, optional Topmost and a tray menu. Keep the technical event list collapsed. Do not add global input hooks. During a live match, collapse to peek/non-pinned behavior; the separate respawn overlay remains unchanged.

- [ ] **Step 5: Run UI, integration and solution tests**

Run the focused command, full Desktop Integration project, then full solution build/test. Expected: zero warnings/errors and all tests pass.

- [ ] **Step 6: Render and visually inspect four states**

Launch the app in preview arguments for `preparing`, `ready`, `in-game` and `issue`, capture screenshots, and inspect each with `view_image`. Verify transparent edges, readable bubble, no permanent panel, no clipping at 100% and 150% scale, and consistent original asset.

- [ ] **Step 7: Commit Task 5**

```powershell
git add src/RespawnSwitch.App tests/RespawnSwitch.Desktop.IntegrationTests/Ui
git commit -m "feat: transform RespawnSwitch into a desktop pet"
```

---

### Task 6: Live Media Regression and Failure Records

**Files:**
- Modify: `tools/RespawnSwitch.MediaSmoke/Program.cs`
- Modify: `tests/RespawnSwitch.Windows.Tests/Media/MediaSmokeCliTests.cs`
- Modify: `项目错误记录.txt`
- Modify: `docs/最终验证记录.md`

**Interfaces:**
- Produces: `cycle-test --aumid ... --fingerprint ...` that captures initial state, executes explicit Play/Pause with verification and restores initial state in `finally`.

- [ ] **Step 1: Add failing CLI parse and restoration tests**

Test the new command requires both exact identity fields, reports separate play/pause verification, and invokes initial-state restoration even when the middle Pause fails. Use a fake gateway behind a small testable runner; assert final observable state, not mock existence.

- [ ] **Step 2: Run and verify RED**

Run Windows tests filtered to `MediaSmokeCliTests`; expected failure because `cycle-test` is unknown.

- [ ] **Step 3: Implement safe cycle-test**

The command must refuse ambiguous/no-match targets, log elapsed milliseconds and state reads, and restore the captured initial state in `finally`. It must never operate on a global/current session without exact AUMID and fingerprint.

- [ ] **Step 4: Run automated and live Douyin tests**

If the unique `electron.app.douyin` profile is still available, run the published tool's list, probe and cycle-test once. Confirm exact target count 1, Play verified, Pause verified, and final state equals the pre-test state. If it is unavailable, record that fact and continue without retry loops or environment setup.

- [ ] **Step 5: Commit Task 6**

```powershell
git add tools/RespawnSwitch.MediaSmoke tests/RespawnSwitch.Windows.Tests/Media/MediaSmokeCliTests.cs 项目错误记录.txt docs/最终验证记录.md
git commit -m "test: add verified Douyin media cycle smoke"
```

---

### Task 7: Version 0.3.6, Manuals, Package, Workspace and GitHub

**Files:**
- Modify: `src/RespawnSwitch.App/RespawnSwitch.App.csproj`
- Modify: `build/publish.ps1`
- Modify: `README.md`
- Create: `docs/RespawnSwitch-0.3.6-用户使用说明.txt`
- Create: `docs/RespawnSwitch-0.3.6-开发者说明.md`
- Modify: `docs/用户需求与操作历史.md`
- Modify: `docs/操作要求.md`
- Modify: `docs/最终验证记录.md`

**Interfaces:**
- Produces: self-contained `RespawnSwitch-0.3.6-win-x64.zip`, SHA-256 sidecar, user/developer manuals, archived 0.3.5, synchronized source/bundle and public `main`.

- [ ] **Step 1: Update release metadata and manuals**

Set Version/FileVersion/AssemblyVersion to `0.3.6`. User instructions cover only startup, pet interaction, readiness, desktop/web Douyin and simple troubleshooting. Developer instructions document cycle states, exact return ordering, time budgets, pet architecture, tests, limitations and `Stress Monster` authorship.

- [ ] **Step 2: Run fresh completion verification**

```powershell
dotnet build RespawnSwitch.sln -c Release --no-restore
dotnet test RespawnSwitch.sln -c Release --no-build --no-restore
powershell -ExecutionPolicy Bypass -File .\build\publish.ps1
```

Run the final published `RespawnSwitch.exe --self-test`, inspect ZIP entries for both manuals, `AUTHOR.txt`, extension and `SHA256SUMS`, confirm no PDB, and compare ZIP SHA-256 twice.

- [ ] **Step 3: Validate the final desktop pet executable**

Start the exact published executable, confirm one process, transparent/taskbar-hidden window, pet screenshot, tray exit and clean shutdown. Do not label this as a real League match. Record that the next Practice Tool match remains the final current-patch end-to-end check.

- [ ] **Step 4: Commit release source**

```powershell
git add -A
git commit -m "release: ship RespawnSwitch 0.3.6 desktop pet"
```

- [ ] **Step 5: Organize the long-term workspace**

Resolve and verify the exact existing targets. Move every 0.3.5 delivery item into `outputs\历史版本\0.3.5`; place only the 0.3.6 runnable folder, ZIP, hash, user manual, developer manual and preview in the current delivery directory. Refresh the tracked-source snapshot, versioned source ZIP and complete Git bundle. Never delete earlier history.

- [ ] **Step 6: Push and verify public GitHub main**

Push the current branch to `origin/main` using the existing authenticated Git Credential Manager path. Verify local HEAD equals `git ls-remote origin refs/heads/main`, the public repository remains public, the raw README at the final commit says 0.3.6, and the worktree is clean.

- [ ] **Step 7: Final handoff**

Report the delivery links, final commit, test counts, live Douyin cycle result, package self-test, ZIP hash, workspace archive state and the honest remaining Practice Tool validation boundary.
