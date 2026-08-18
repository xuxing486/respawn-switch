# RespawnSwitch Reliability Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a responsive Windows product that completes pre-match readiness checks, controls pre-opened Douyin desktop or Chrome/Edge web targets, and shows a locally interpolated hero/KDA respawn overlay without blocking League monitoring.

**Architecture:** Keep Riot sampling and the state machine single-threaded and fast; run one cancellable side-effect task per respawn cycle. Model readiness and faults as structured immutable values, use GSMTC for desktop media, a restricted Manifest V3 extension plus native host for web media, and a WPF monotonic overlay timer for rendering.

**Tech Stack:** C# 12, .NET 8, WPF, Win32/Toolhelp, WinRT GSMTC, Chromium Manifest V3 JavaScript, xUnit.

**Spec:** `docs/superpowers/specs/2026-08-18-respawnswitch-reliability-redesign.md`

## Global Constraints

- Windows x64, League borderless/windowed mode, no game memory reads, injection, hooks, simulated game input, global media keys, spacebar, or coordinate clicks.
- No Douyin disk scan or automatic executable launch; users pre-open desktop or web video.
- Firefox is out of scope; Chrome and Microsoft Edge are supported.
- `isDead` remains life-state truth; one valid `respawnTimer` creates each cycle's local monotonic anchor.
- External media/window operations never block the League observation loop.
- Every claimed ready/success state has a verified postcondition and a structured component/error code.

---

### Task 1: Countdown snapshot and structured readiness model

**Files:**
- Modify: `src/RespawnSwitch.Core/Game/GameSample.cs`
- Create: `src/RespawnSwitch.Core/Clock/LocalRespawnCountdown.cs`
- Create: `src/RespawnSwitch.Application/Readiness/ReadinessModels.cs`
- Test: `tests/RespawnSwitch.Core.Tests/Clock/LocalRespawnCountdownTests.cs`
- Test: `tests/RespawnSwitch.Application.Tests/Readiness/ReadinessModelsTests.cs`

**Interfaces:**
- Produces `GameSample.ChampionName`, `Kills`, `Deaths`, `Assists`.
- Produces `LocalRespawnCountdown.Create(TimeProvider,double)` and `Snapshot()`.
- Produces `Subsystem`, `ReadinessLevel`, `ComponentReadiness`, `ProductReadiness`.

- [ ] Write failing tests proving a 12.2-second anchor renders 13 then 12, reaches confirmation state at zero, and hero/KDA fields survive sample construction.
- [ ] Run the two targeted test classes and confirm compilation/test failure.
- [ ] Implement immutable models and aggregation where any Error makes the product blocked, any Waiting makes it waiting, and all Ready makes it ready.
- [ ] Run targeted tests and commit `feat(core): add local countdown and readiness models`.

### Task 2: Minimal Riot sampling

**Files:**
- Modify: `src/RespawnSwitch.Riot/Parsing/RiotDtos.cs`
- Modify: `src/RespawnSwitch.Riot/Parsing/PlayerListParser.cs`
- Modify: `src/RespawnSwitch.Riot/Polling/LeagueGameProbe.cs`
- Modify: `src/RespawnSwitch.Riot/Polling/LeaguePollingSchedule.cs`
- Test: `tests/RespawnSwitch.Riot.Tests/Parsing/PlayerListParserTests.cs`
- Test: `tests/RespawnSwitch.Riot.Tests/Polling/LeagueGameProbeTests.cs`

**Interfaces:**
- Consumes the Task 1 `GameSample` fields.
- Produces samples with champion and KDA and caches active Riot ID per active game.

- [ ] Add failing fixture assertions for champion/K/D/A and a route-count test proving repeated samples do not repeatedly fetch active player and game stats.
- [ ] Run targeted Riot tests and confirm RED.
- [ ] Extend parsing and cache identity/timeline metadata; use `playerlist` as the recurring endpoint and bounded backoff outside games.
- [ ] Run all Riot tests and commit `perf(riot): minimize live client polling`.

### Task 3: Pre-match readiness and media target selection

**Files:**
- Create: `src/RespawnSwitch.Application/Readiness/PreflightCoordinator.cs`
- Create: `src/RespawnSwitch.Application/Media/MediaTargetContracts.cs`
- Create: `src/RespawnSwitch.Windows/Windows/LeagueClientPresenceProbe.cs`
- Modify: `src/RespawnSwitch.Windows/Media/GsmtcDouyinMediaController.cs`
- Test: `tests/RespawnSwitch.Application.Tests/Readiness/PreflightCoordinatorTests.cs`
- Test: `tests/RespawnSwitch.Windows.Tests/Windows/LeagueClientPresenceProbeTests.cs`

**Interfaces:**
- Produces `IMediaTarget.ProbeAsync/ShowAndPlayAsync/PauseAndRestoreAsync`.
- Produces `MediaTargetPreference.Auto/Desktop/Web` and deterministic one-target selection.
- Produces a League client readiness result from Toolhelp plus a visible top-level client window without LCU credentials.

- [ ] Write failing tests for League client present/absent, desktop-only, web-only, both-preferred, and neither-ready states.
- [ ] Run targeted tests and confirm RED.
- [ ] Implement the smallest deterministic preflight and target selection path; GSMTC probe must report exact state/control failures.
- [ ] Run targeted tests and commit `feat(readiness): add pre-match component checks`.

### Task 4: Non-blocking respawn cycle and local overlay

**Files:**
- Create: `src/RespawnSwitch.App/RespawnCycleRunner.cs`
- Modify: `src/RespawnSwitch.App/RespawnCoordinator.cs`
- Modify: `src/RespawnSwitch.App/Overlay/RespawnOverlayWindow.xaml`
- Modify: `src/RespawnSwitch.App/Overlay/RespawnOverlayWindow.xaml.cs`
- Test: `tests/RespawnSwitch.Desktop.IntegrationTests/Coordinator/RespawnCycleRunnerTests.cs`
- Test: `tests/RespawnSwitch.Desktop.IntegrationTests/Ui/OverlayCountdownTests.cs`

**Interfaces:**
- `RespawnCycleRunner.StartAsync(cycleId,target,game,sample,token)` returns immediately after scheduling side effects.
- `CancelAndRestoreAsync(cycleId)` preempts attachment and performs bounded cleanup.
- Overlay `BeginCycle(target,sample,countdown)`, `MarkAwaitingConfirmation()`, `MarkConnectionUnstable()`, `EndCycle()`.

- [ ] Write failing tests where media attachment is blocked for ten seconds but overlay ticks and respawn cancellation completes within 500 ms.
- [ ] Add failing UI assertions for champion name, `K / D / A`, and 12-to-11 countdown progression.
- [ ] Run targeted tests and confirm RED.
- [ ] Implement per-cycle cancellation, fire-and-observe side-effect ownership, dispatcher timer rendering, and idempotent cleanup.
- [ ] Run targeted plus Core tests and commit `fix(runtime): decouple respawn effects from monitoring`.

### Task 5: Verified desktop window switching

**Files:**
- Modify: `src/RespawnSwitch.Windows/Interop/User32.cs`
- Modify: `src/RespawnSwitch.Windows/Windows/MvpDouyinWindowController.cs`
- Modify: `src/RespawnSwitch.Windows/Windows/LeagueWindowLocator.cs`
- Test: `tests/RespawnSwitch.Windows.Tests/Windows/DouyinWindowControllerTests.cs`
- Test: `tests/RespawnSwitch.Windows.Tests/Windows/LeagueWindowLocatorTests.cs`

**Interfaces:**
- Desktop target operates only on a currently running, uniquely verified Douyin window.
- Window results distinguish request dispatch from verified visibility/Z-order/focus.

- [ ] Write failing tests proving no executable launch occurs, `ShowWindowAsync` return is not treated as postcondition, Douyin becomes visible/topmost, and League focus is read back after one attempt.
- [ ] Run targeted tests and confirm RED.
- [ ] Add `GetForegroundWindow`, correct show/position semantics, bounded state verification, and compare-and-restore behavior.
- [ ] Run Windows and desktop integration tests and commit `fix(windows): verify Douyin and League switching`.

### Task 6: Chrome and Edge web target

**Files:**
- Create: `browser-extension/manifest.json`
- Create: `browser-extension/service-worker.js`
- Create: `browser-extension/content.js`
- Create: `src/RespawnSwitch.BrowserHost/RespawnSwitch.BrowserHost.csproj`
- Create: `src/RespawnSwitch.BrowserHost/Program.cs`
- Create: `src/RespawnSwitch.App/Browser/BrowserTargetController.cs`
- Test: `tests/RespawnSwitch.Desktop.IntegrationTests/Browser/BrowserTargetControllerTests.cs`

**Interfaces:**
- Native messages: `{sequence,command}` with commands `probe`, `play`, `pause`, `activate`; replies `{sequence,ok,state,browser,tabCount,errorCode}`.
- Content script selects exactly one largest visible `<video>`, applies idempotent play/pause, and verifies `paused`.

- [ ] Write failing controller tests for heartbeat, unique target, multiple tabs, rejected play, stale sequence, and timeout.
- [ ] Run tests and confirm RED.
- [ ] Implement restricted Douyin host permissions, Native Messaging relay, local status persistence, Chrome/Edge host registration helper, and structured errors.
- [ ] Validate `manifest.json` parsing and run `node --check` on both JavaScript files.
- [ ] Run browser/controller tests and commit `feat(web): add Chrome and Edge Douyin control`.

### Task 7: Minimal polished UI, diagnostics, packaging, and delivery

**Files:**
- Modify: `src/RespawnSwitch.App/AppSettings.cs`
- Modify: `src/RespawnSwitch.App/MainWindow.xaml`
- Modify: `src/RespawnSwitch.App/MainWindow.xaml.cs`
- Create: `src/RespawnSwitch.App/Diagnostics/RollingDiagnosticLog.cs`
- Modify: `src/RespawnSwitch.App/App.xaml.cs`
- Modify: `build/publish.ps1`
- Modify: `tests/RespawnSwitch.Desktop.IntegrationTests/Ui/MainWindowSmokeTests.cs`

**Interfaces:**
- UI binds only to `ProductReadiness` and structured issues.
- Settings retain target preference and browser pairing metadata but remove discovery/path controls.

- [ ] Add failing smoke assertions for the overall status, League/Douyin/automation cards, component-specific issue text, target selector, retest button, and overlay hero/KDA text.
- [ ] Run UI tests and confirm RED.
- [ ] Replace scan-heavy page with the compact three-card layout, diagnostics drawer, and accurate pre-match/in-game states; add redacted rolling log.
- [ ] Extend self-test and publish script to include browser extension/host and verify required files.
- [ ] Run `dotnet build RespawnSwitch.sln -c Release --no-restore`, full Release tests, JS syntax checks, source and packaged `--self-test`, and real launch/close validation.
- [ ] Build versioned ZIP, copy final artifacts and updated history to the established desktop workspace, commit, push the existing public repository, and verify local/remote parity.
