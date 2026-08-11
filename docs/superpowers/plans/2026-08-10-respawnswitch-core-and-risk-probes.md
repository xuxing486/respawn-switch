# RespawnSwitch Core and Riot Risk Probes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the testable domain core, secure Riot Live Client Data reader, monotonic respawn clock, and a real-client semantic probe that proves whether the current League patch exposes a trustworthy respawn timer before any Douyin automation is enabled.

**Architecture:** Pure domain rules live in `RespawnSwitch.Core`; orchestration contracts live in `RespawnSwitch.Application`; Riot HTTP, parsing, TLS, and polling live in `RespawnSwitch.Riot`. The probe is a separate console tool that records only redacted observations and operator markers. Part 2 is blocked until Gate A validates the current patch, certificate behavior, exact Riot ID matching, and `respawnTimer` semantics against visible in-game evidence.

**Tech Stack:** C# 12, .NET SDK 8.0.423, .NET 8, xUnit 2.5.3, Microsoft.NET.Test.Sdk 17.8.0, `System.Net.Http`, `System.Text.Json`, WPF project placeholders for Part 2, Windows x64.

## Global Constraints

- The approved design is `docs/superpowers/specs/2026-08-10-lol-douyin-respawn-assistant-design.md`; do not reopen settled product decisions during implementation.
- Run all PowerShell commands from `C:\Users\1\Documents\Codex\2026-08-10\yeah-2`.
- Before every `dotnet` command in this sandbox, set:

  ```powershell
  $env:DOTNET_CLI_HOME = Join-Path $PWD 'work\dotnet-home'
  $env:NUGET_PACKAGES = Join-Path $PWD 'work\nuget-packages'
  $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
  ```

- The standard `.git` directory is write-protected by the desktop sandbox. Before every Git command in this session, set:

  ```powershell
  $env:GIT_DIR = Join-Path $PWD 'work\git-metadata'
  $env:GIT_WORK_TREE = $PWD
  ```

- The first restore may require one explicit network permission grant for NuGet. After `packages.lock.json` files exist, use `dotnet restore --locked-mode` and do not update packages incidentally.
- Use the locally installed SDK template package versions: xUnit 2.5.3, xUnit runner 2.5.3, Microsoft.NET.Test.Sdk 17.8.0, coverlet.collector 6.0.0.
- Production Windows projects target `net8.0-windows10.0.26100.0` and set `SupportedOSPlatformVersion` to `10.0.17763.0`; pure Core/Application/Riot projects target `net8.0` unless they directly consume a Windows-only API.
- Do not add `Microsoft.Windows.SDK.Contracts`, `Microsoft.Windows.CsWinRT`, or `Microsoft.WindowsAppSDK`.
- Use ordinary, unpackaged, `asInvoker` processes. Do not request administrator privileges or `uiAccess`.
- Riot access is read-only HTTPS GET to the literal endpoint `https://127.0.0.1:2999`; disable proxies and redirects. Never send POST, PUT, PATCH, or DELETE.
- Never open a League process handle, read process memory, inject a DLL, install a hook, simulate game input, or modify Riot files.
- `isDead` is the only life-state truth. Probe failures, malformed JSON, connection refusal, and TLS failure cannot produce a respawn event.
- Treat `respawnTimer` as raw, unverified data until Gate A passes for the current League patch. Before Gate A, do not label it as seconds and do not use a 2-second media attachment threshold.
- Use `TimeProvider.GetTimestamp()` and `TimeProvider.GetElapsedTime()` for all durations. Never compute countdown or stale durations with wall-clock subtraction.
- Every task follows RED → GREEN → focused regression → commit. A task is not complete if its named tests were not observed failing before implementation and passing afterward.
- Store generated diagnostics under `artifacts/`; exclude `artifacts/`, `work/`, and `outputs/` from Git.

---

## Scope Split

This is Part 1 of 2. It ends at a hard real-environment gate.

- Part 1: solution baseline, domain model, orthogonal state machine, monotonic clock, Riot parsing/TLS/polling, semantic probe, Gate A.
- Part 2: GSMTC/UIA, Win32 window identity, transactional recovery, overlay, coordinator, tray/settings, packaging, and end-to-end acceptance.

Do not begin Part 2 until Gate A has a committed evidence report with `Passed: true`.

## File Map for Part 1

### Root

- Create: `RespawnSwitch.sln` — solution membership only.
- Create: `global.json` — pins SDK 8.0.423.
- Create: `Directory.Build.props` — common compiler, deterministic build, lock-file, and warning policy.
- Create: `.editorconfig` — UTF-8, LF, four-space C# indentation, final newline.
- Create: `.gitattributes` — normalize tracked text to LF while preserving binary files.
- Create: `.gitignore` — excludes build, local sandbox, artifacts, outputs, and user settings.
- Create: `README.md` — safe-scope summary and developer commands.

### Production projects

- Create: `src/RespawnSwitch.Core/RespawnSwitch.Core.csproj` — pure domain types and rules.
- Create: `src/RespawnSwitch.Application/RespawnSwitch.Application.csproj` — application ports; Part 1 adds monitoring contracts only.
- Create: `src/RespawnSwitch.Riot/RespawnSwitch.Riot.csproj` — endpoint policy, TLS, JSON parsers, HTTP client, polling.
- Create: `src/RespawnSwitch.Infrastructure/RespawnSwitch.Infrastructure.csproj` — placeholder project for Part 2 atomic persistence/logging.
- Create: `src/RespawnSwitch.Windows/RespawnSwitch.Windows.csproj` — Windows-only placeholder for Part 2.
- Create: `src/RespawnSwitch.App/RespawnSwitch.App.csproj` — WPF placeholder for Part 2 composition.

### Tools

- Create: `tools/RespawnSwitch.SemanticProbe/RespawnSwitch.SemanticProbe.csproj` — real-client recorder and report generator.
- Create: `tools/RespawnSwitch.TestWindowHost/RespawnSwitch.TestWindowHost.csproj` — WPF test host placeholder for Part 2.

### Tests

- Create: `tests/RespawnSwitch.Core.Tests/RespawnSwitch.Core.Tests.csproj` — state, policy, clock, and timeline rules.
- Create: `tests/RespawnSwitch.Application.Tests/RespawnSwitch.Application.Tests.csproj` — coordinator tests in Part 2.
- Create: `tests/RespawnSwitch.Riot.Tests/RespawnSwitch.Riot.Tests.csproj` — parsing, endpoint, TLS, cancellation, cadence.
- Create: `tests/RespawnSwitch.Infrastructure.Tests/RespawnSwitch.Infrastructure.Tests.csproj` — persistence tests in Part 2.
- Create: `tests/RespawnSwitch.Windows.Tests/RespawnSwitch.Windows.Tests.csproj` — fake Win32/media tests in Part 2.
- Create: `tests/RespawnSwitch.Desktop.IntegrationTests/RespawnSwitch.Desktop.IntegrationTests.csproj` — serialized real HWND/WPF tests in Part 2.
- Create: `tests/RespawnSwitch.Architecture.Tests/RespawnSwitch.Architecture.Tests.csproj` — forbidden dependency/API checks.

## Locked Part 1 Public Contracts

These names and signatures are consumed by Part 2. Do not rename them in an individual task.

```csharp
namespace RespawnSwitch.Core.Game;

public enum SchemaSource
{
    PlayerList,
    AllGameData
}

public sealed record GameInstanceKey(
    int ProcessId,
    string RiotId,
    string TimelineKey);

public sealed record GamePresenceSnapshot(
    bool ProcessPresent,
    bool WindowPresent,
    int? ProcessId,
    string? InstanceKey);

public sealed record GameSample(
    long SampleId,
    long ObservedAtTimestamp,
    string RiotId,
    bool IsDead,
    double? RespawnTimerRaw,
    double? RespawnTimerSeconds,
    double GameTimeSeconds,
    string GameMode,
    SchemaSource SchemaSource,
    string TimelineKey);
```

```csharp
using RespawnSwitch.Core.Game;

namespace RespawnSwitch.Application.Monitoring;

public abstract record LeagueProbeObservation(long ObservedAtTimestamp);

public sealed record LeagueSampleObserved(GameSample Sample)
    : LeagueProbeObservation(Sample.ObservedAtTimestamp);

public sealed record LeagueProbeFailed(ProbeFailure Failure)
    : LeagueProbeObservation(Failure.ObservedAtTimestamp);

public interface ILeagueGameProbe
{
    IAsyncEnumerable<LeagueProbeObservation> WatchAsync(
        CancellationToken cancellationToken);

    ValueTask<LeagueProbeObservation> SampleOnceAsync(
        CancellationToken cancellationToken);
}
```

---

### Task 1: Scaffold the solution and lock the `GameSample` contract

**Files:**

- Create: all root, production, tool, and test project files listed in the Part 1 file map.
- Create: `src/RespawnSwitch.Core/Game/GameSample.cs`
- Create: `src/RespawnSwitch.Core/Game/GamePresenceSnapshot.cs`
- Create: `src/RespawnSwitch.Core/Game/GameInstanceKey.cs`
- Test: `tests/RespawnSwitch.Core.Tests/Game/GameSampleTests.cs`

**Interfaces:**

- Consumes: none.
- Produces: `SchemaSource`, `GameSample`, `GamePresenceSnapshot`, and `GameInstanceKey` exactly as locked above; a buildable solution used by every later task.

- [ ] **Step 1: Set the sandbox-local CLI environment and scaffold without restoring**

  ```powershell
  $env:DOTNET_CLI_HOME = Join-Path $PWD 'work\dotnet-home'
  $env:NUGET_PACKAGES = Join-Path $PWD 'work\nuget-packages'
  $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

  dotnet new sln -n RespawnSwitch
  dotnet new classlib -n RespawnSwitch.Core -o src\RespawnSwitch.Core -f net8.0 --no-restore
  dotnet new classlib -n RespawnSwitch.Application -o src\RespawnSwitch.Application -f net8.0 --no-restore
  dotnet new classlib -n RespawnSwitch.Riot -o src\RespawnSwitch.Riot -f net8.0 --no-restore
  dotnet new classlib -n RespawnSwitch.Infrastructure -o src\RespawnSwitch.Infrastructure -f net8.0 --no-restore
  dotnet new classlib -n RespawnSwitch.Windows -o src\RespawnSwitch.Windows -f net8.0 --no-restore
  dotnet new wpf -n RespawnSwitch.App -o src\RespawnSwitch.App -f net8.0 --no-restore

  dotnet new console -n RespawnSwitch.SemanticProbe -o tools\RespawnSwitch.SemanticProbe -f net8.0 --no-restore
  dotnet new wpf -n RespawnSwitch.TestWindowHost -o tools\RespawnSwitch.TestWindowHost -f net8.0 --no-restore

  @(
    'RespawnSwitch.Core.Tests',
    'RespawnSwitch.Application.Tests',
    'RespawnSwitch.Riot.Tests',
    'RespawnSwitch.Infrastructure.Tests',
    'RespawnSwitch.Windows.Tests',
    'RespawnSwitch.Desktop.IntegrationTests',
    'RespawnSwitch.Architecture.Tests'
  ) | ForEach-Object {
    dotnet new xunit -n $_ -o "tests\$_" -f net8.0 --no-restore
  }
  ```

- [ ] **Step 2: Add project membership and references**

  ```powershell
  dotnet sln RespawnSwitch.sln add (Get-ChildItem src,tools,tests -Recurse -Filter *.csproj | ForEach-Object FullName)

  dotnet add src\RespawnSwitch.Application reference src\RespawnSwitch.Core
  dotnet add src\RespawnSwitch.Riot reference src\RespawnSwitch.Core src\RespawnSwitch.Application
  dotnet add src\RespawnSwitch.Infrastructure reference src\RespawnSwitch.Core src\RespawnSwitch.Application
  dotnet add src\RespawnSwitch.Windows reference src\RespawnSwitch.Core src\RespawnSwitch.Application
  dotnet add src\RespawnSwitch.App reference src\RespawnSwitch.Core src\RespawnSwitch.Application src\RespawnSwitch.Riot src\RespawnSwitch.Infrastructure src\RespawnSwitch.Windows
  dotnet add tools\RespawnSwitch.SemanticProbe reference src\RespawnSwitch.Core src\RespawnSwitch.Application src\RespawnSwitch.Riot src\RespawnSwitch.Infrastructure

  dotnet add tests\RespawnSwitch.Core.Tests reference src\RespawnSwitch.Core
  dotnet add tests\RespawnSwitch.Application.Tests reference src\RespawnSwitch.Core src\RespawnSwitch.Application
  dotnet add tests\RespawnSwitch.Riot.Tests reference src\RespawnSwitch.Core src\RespawnSwitch.Application src\RespawnSwitch.Riot
  dotnet add tests\RespawnSwitch.Infrastructure.Tests reference src\RespawnSwitch.Core src\RespawnSwitch.Application src\RespawnSwitch.Infrastructure
  dotnet add tests\RespawnSwitch.Windows.Tests reference src\RespawnSwitch.Core src\RespawnSwitch.Application src\RespawnSwitch.Windows
  dotnet add tests\RespawnSwitch.Desktop.IntegrationTests reference src\RespawnSwitch.Core src\RespawnSwitch.Application src\RespawnSwitch.Windows src\RespawnSwitch.App
  ```

- [ ] **Step 3: Add exact root build policy files**

  `global.json`:

  ```json
  {
    "sdk": {
      "version": "8.0.423",
      "rollForward": "latestPatch"
    }
  }
  ```

  `Directory.Build.props`:

  ```xml
  <Project>
    <PropertyGroup>
      <LangVersion>12.0</LangVersion>
      <Nullable>enable</Nullable>
      <ImplicitUsings>enable</ImplicitUsings>
      <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
      <Deterministic>true</Deterministic>
      <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
    </PropertyGroup>
  </Project>
  ```

  `.gitignore`:

  ```gitignore
  .vs/
  **/bin/
  **/obj/
  *.user
  TestResults/
  artifacts/
  work/
  outputs/
  ```

  `.gitattributes`:

  ```gitattributes
  * text=auto eol=lf
  *.png binary
  *.ico binary
  *.zip binary
  *.dll binary
  *.exe binary
  ```

  `.editorconfig`:

  ```ini
  root = true

  [*]
  charset = utf-8
  end_of_line = lf
  insert_final_newline = true
  trim_trailing_whitespace = true

  [*.{cs,csx}]
  indent_style = space
  indent_size = 4

  [*.{csproj,props,targets,xaml,json,md,yml,yaml}]
  indent_style = space
  indent_size = 2
  ```

  `README.md`:

  ````markdown
  # RespawnSwitch

  RespawnSwitch is a local-only Windows x64 prototype that reads the Riot Live Client Data API for the current player, shows a no-activate respawn overlay, and controls only the calibrated Douyin desktop client. It does not open the League process, read memory, inject code, install hooks, or send game input.

  The current implementation is gated by `docs/superpowers/plans/2026-08-10-respawnswitch-core-and-risk-probes.md`. Do not enable Windows or media automation until Gate A has passed for the current League patch.

  ## Developer commands

  ```powershell
  $env:DOTNET_CLI_HOME = Join-Path $PWD 'work\dotnet-home'
  $env:NUGET_PACKAGES = Join-Path $PWD 'work\nuget-packages'
  $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
  dotnet restore RespawnSwitch.sln
  dotnet build RespawnSwitch.sln -c Debug --no-restore
  dotnet test RespawnSwitch.sln -c Debug --no-build
  ```
  ````

  Update `RespawnSwitch.Windows`, `RespawnSwitch.App`, `RespawnSwitch.TestWindowHost`, `RespawnSwitch.Windows.Tests`, and `RespawnSwitch.Desktop.IntegrationTests` to:

  ```xml
  <TargetFramework>net8.0-windows10.0.26100.0</TargetFramework>
  <SupportedOSPlatformVersion>10.0.17763.0</SupportedOSPlatformVersion>
  <PlatformTarget>x64</PlatformTarget>
  ```

  `RespawnSwitch.App.csproj` additionally contains:

  ```xml
  <OutputType>WinExe</OutputType>
  <UseWPF>true</UseWPF>
  <UseWindowsForms>true</UseWindowsForms>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <SelfContained>true</SelfContained>
  <PublishSingleFile>false</PublishSingleFile>
  <PublishTrimmed>false</PublishTrimmed>
  ```

- [ ] **Step 4: Write the failing `GameSample` contract test**

  ```csharp
  using RespawnSwitch.Core.Game;

  namespace RespawnSwitch.Core.Tests.Game;

  public sealed class GameSampleTests
  {
      [Fact]
      public void Constructor_PreservesRawAndVerifiedTimerSeparately()
      {
          var sample = new GameSample(
              SampleId: 7,
              ObservedAtTimestamp: 12_345,
              RiotId: "Player#NA1",
              IsDead: true,
              RespawnTimerRaw: 18.75,
              RespawnTimerSeconds: null,
              GameTimeSeconds: 1_234.5,
              GameMode: "PRACTICETOOL",
              SchemaSource: SchemaSource.PlayerList,
              TimelineKey: "42:Player#NA1:0");

          Assert.Equal(18.75, sample.RespawnTimerRaw);
          Assert.Null(sample.RespawnTimerSeconds);
          Assert.True(sample.IsDead);
      }
  }
  ```

- [ ] **Step 5: Run the focused test and observe RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Core.Tests -c Debug --filter FullyQualifiedName~GameSampleTests
  ```

  Expected: compilation fails because `GameSample` and `SchemaSource` do not exist.

- [ ] **Step 6: Add the locked domain records and remove template `Class1.cs`/`UnitTest1.cs` files**

  Implement the exact locked `GameSample`, `SchemaSource`, `GamePresenceSnapshot`, and `GameInstanceKey` declarations above. Do not add timer interpretation logic in this task.

- [ ] **Step 7: Restore once, run GREEN, then build the entire solution**

  ```powershell
  dotnet restore RespawnSwitch.sln
  dotnet test tests\RespawnSwitch.Core.Tests -c Debug --filter FullyQualifiedName~GameSampleTests --no-restore
  dotnet build RespawnSwitch.sln -c Debug --no-restore
  ```

  Expected: the focused test and full build pass; every project has a generated `packages.lock.json`.

- [ ] **Step 8: Commit the independently buildable baseline**

  ```powershell
  $env:GIT_DIR = Join-Path $PWD 'work\git-metadata'
  $env:GIT_WORK_TREE = $PWD
  git add .gitignore .gitattributes .editorconfig global.json Directory.Build.props README.md RespawnSwitch.sln src tools tests
  git commit -m "build: scaffold .NET 8 RespawnSwitch solution"
  ```

---

### Task 2: Add timer semantics and the one-shot attachment policy

**Files:**

- Create: `src/RespawnSwitch.Core/Clock/RespawnTimerSemantics.cs`
- Create: `src/RespawnSwitch.Core/Clock/RespawnTimerNormalizer.cs`
- Create: `src/RespawnSwitch.Core/Respawn/AttachmentPolicy.cs`
- Test: `tests/RespawnSwitch.Core.Tests/Clock/RespawnTimerNormalizerTests.cs`
- Test: `tests/RespawnSwitch.Core.Tests/Respawn/AttachmentPolicyTests.cs`

**Interfaces:**

- Consumes: `GameSample.RespawnTimerRaw` from Task 1.
- Produces: `RespawnTimerSemantics`, `TimerSemanticStatus`, `RespawnTimerNormalizer.TryNormalize`, `AttachmentDecision`, and `AttachmentPolicy.Evaluate` for Tasks 3, 4, 7, and Part 2.

- [ ] **Step 1: Write failing normalization and threshold tests**

  ```csharp
  public sealed class RespawnTimerNormalizerTests
  {
      [Theory]
      [MemberData(nameof(InvalidValues))]
      public void TryNormalize_InvalidOrUnverifiedValue_ReturnsFalse(double? raw)
      {
          var normalizer = new RespawnTimerNormalizer();
          var semantics = new RespawnTimerSemantics(
              TimerSemanticStatus.Unverified,
              PatchLabel: "16.15",
              SecondsPerRawUnit: 1.0,
              EvidenceReportId: "none");

          Assert.False(normalizer.TryNormalize(raw, semantics, out _));
      }

      public static TheoryData<double?> InvalidValues => new()
      {
          null,
          double.NaN,
          double.PositiveInfinity,
          double.NegativeInfinity,
          -0.01,
          10.0
      };
  }
  ```

  ```csharp
  public sealed class AttachmentPolicyTests
  {
      [Theory]
      [InlineData(null, false, AttachmentDecision.WaitForVerifiedTimer)]
      [InlineData(1.999, false, AttachmentDecision.CountdownOnly)]
      [InlineData(2.0, false, AttachmentDecision.AttachOnce)]
      [InlineData(9.0, true, AttachmentDecision.AlreadyIssued)]
      public void Evaluate_UsesExactTwoSecondBoundary(
          double? verifiedSeconds,
          bool attachmentIssued,
          AttachmentDecision expected)
      {
          Assert.Equal(
              expected,
              AttachmentPolicy.Evaluate(
                  verifiedSeconds,
                  attachmentIssued,
                  thresholdSeconds: 2.0));
      }
  }
  ```

- [ ] **Step 2: Run RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Core.Tests -c Debug --filter "FullyQualifiedName~RespawnTimerNormalizerTests|FullyQualifiedName~AttachmentPolicyTests" --no-restore
  ```

  Expected: compilation fails because semantic and attachment types are missing.

- [ ] **Step 3: Implement the exact policy contracts**

  ```csharp
  public enum TimerSemanticStatus
  {
      Unverified,
      VerifiedForCurrentPatch
  }

  public sealed record RespawnTimerSemantics(
      TimerSemanticStatus Status,
      string PatchLabel,
      double SecondsPerRawUnit,
      string EvidenceReportId);

  public enum AttachmentDecision
  {
      WaitForVerifiedTimer,
      CountdownOnly,
      AttachOnce,
      AlreadyIssued
  }
  ```

  `TryNormalize` returns true only when semantics are verified, the raw value and multiplier are finite and non-negative, and multiplication remains finite. `AttachmentPolicy.Evaluate` returns `AlreadyIssued` first, then `WaitForVerifiedTimer` for null/invalid values, `CountdownOnly` for `[0, 2)`, and `AttachOnce` for `>= 2`.

- [ ] **Step 4: Run GREEN and Core regression**

  ```powershell
  dotnet test tests\RespawnSwitch.Core.Tests -c Debug --no-restore
  ```

- [ ] **Step 5: Commit**

  ```powershell
  git add src\RespawnSwitch.Core\Clock src\RespawnSwitch.Core\Respawn tests\RespawnSwitch.Core.Tests\Clock tests\RespawnSwitch.Core.Tests\Respawn
  git commit -m "feat(core): add timer semantics and attachment policy"
  ```

---

### Task 3: Implement the orthogonal respawn state machine

**Files:**

- Create: `src/RespawnSwitch.Core/Respawn/RespawnCycleId.cs`
- Create: `src/RespawnSwitch.Core/Respawn/RespawnStates.cs`
- Create: `src/RespawnSwitch.Core/Respawn/RespawnMachineState.cs`
- Create: `src/RespawnSwitch.Core/Respawn/StateMachineInput.cs`
- Create: `src/RespawnSwitch.Core/Respawn/RespawnDomainEvent.cs`
- Create: `src/RespawnSwitch.Core/Respawn/RespawnTransition.cs`
- Create: `src/RespawnSwitch.Core/Respawn/RespawnStateMachineOptions.cs`
- Create: `src/RespawnSwitch.Core/Respawn/RespawnStateMachine.cs`
- Create: `src/RespawnSwitch.Core/Game/ProbeFailure.cs`
- Create: `src/RespawnSwitch.Core/Properties/AssemblyInfo.cs`
- Test: `tests/RespawnSwitch.Core.Tests/Respawn/RespawnStateMachineTests.cs`
- Test: `tests/RespawnSwitch.Core.Tests/Respawn/StaleRecoveryTests.cs`
- Test: `tests/RespawnSwitch.Core.Tests/Respawn/AbandonCycleTests.cs`
- Test helper: `tests/RespawnSwitch.Core.Tests/Respawn/StateMachineScenario.cs`

**Interfaces:**

- Consumes: `GameSample`, `GamePresenceSnapshot`, and `AttachmentPolicy`.
- Produces: `RespawnStateMachine.Apply(StateMachineInput)`, immutable state, and domain events consumed by Part 2's coordinator.

- [ ] **Step 1: Add failing table-driven state tests**

  Include these exact cases and assertions:

  ```csharp
  [Fact]
  public void ProbeFailure_WhileDead_DoesNotPublishRespawnOrChangeLifeState()
  {
      var machine = StateMachineScenario.DeadOnline();
      var transition = machine.Apply(new ProbeFailureInput(
          new ProbeFailure(
              ProbeFailureKind.Timeout,
              "riot.timeout",
              "连接超时",
              ObservedAtTimestamp: 2_000)));

      Assert.Equal(LifeState.Dead, transition.Current.LifeState);
      Assert.Equal(LifeState.Dead, transition.Current.LastConfirmedLifeState);
      Assert.DoesNotContain(
          transition.Events,
          item => item is RespawnConfirmed);
  }
  ```

  ```csharp
  [Fact]
  public void DeadStaleForFiveSeconds_AbandonsOnceWithoutBecomingAlive()
  {
      var machine = StateMachineScenario.DeadStale(
          staleSinceTimestamp: 1_000,
          timestampFrequency: 1_000);

      var first = machine.Apply(new TimePulseInput(6_000));
      var second = machine.Apply(new TimePulseInput(7_000));

      Assert.Single(first.Events.OfType<AbandonCycleDueToUnknown>());
      Assert.Empty(second.Events.OfType<AbandonCycleDueToUnknown>());
      Assert.Equal(LifeState.Dead, second.Current.LifeState);
      Assert.Equal(ActiveCycleStatus.AbandonedUnknown, second.Current.ActiveCycleStatus);
  }
  ```

  Also implement named tests for:

  - `UnknownOnlineAlive_SynchronizesWithoutPublishingRespawn`
  - `UnknownOnlineDead_CreatesCycleAndUsesAttachmentPolicy`
  - `AliveOnlineDead_CreatesExactlyOneCycle`
  - `DeadOnlineDead_DoesNotCreateSecondCycle`
  - `DeadOnlineAlive_PublishesRespawnExactlyOnce`
  - `OnlineBecomesStaleOnlyWhenElapsedTimeIsGreaterThanOneSecond`
  - `StaleRecovery_LastAliveThenDead_CreatesLateCycle`
  - `StaleRecovery_LastDeadThenAlive_CleansOriginalCycle`
  - `StaleRecovery_LastDeadThenDead_DoesNotReplayAttachment`
  - `StaleRecovery_AbandonedDead_UpdatesWithoutReopening`
  - `NoGameRequiresProcessAndWindowAbsentTwiceAcrossTwoSeconds`
  - `NoGameTransientAbsence_DoesNotTransition`
  - `PositiveTimerAlone_CannotTriggerDeath`
  - `IsDeadFalseWithPositiveTimer_TrustsAliveAndSuppressesTimer`
  - `NewRiotIdOrGameTimeRollback_PublishesTimelineResetNotRespawn`

- [ ] **Step 2: Run RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Core.Tests -c Debug --filter "FullyQualifiedName~RespawnStateMachineTests|FullyQualifiedName~StaleRecoveryTests|FullyQualifiedName~AbandonCycleTests" --no-restore
  ```

  Expected: compilation fails on missing state machine types.

- [ ] **Step 3: Implement locked state and event types**

  ```csharp
  public enum LifeState { Unknown, Alive, Dead }
  public enum ConnectionState { NoGame, Online, Stale }
  public enum ActiveCycleStatus { None, Active, AbandonedUnknown, Completed }

  public readonly record struct RespawnCycleId(Guid Value)
  {
      public static RespawnCycleId New() => new(Guid.NewGuid());
  }

  public sealed record RespawnStateMachineOptions(
      TimeSpan StaleAfter,
      TimeSpan AbandonDeadCycleAfter,
      TimeSpan NoGameConfirmationSpan,
      double AttachmentThresholdSeconds,
      long TimestampFrequency);
  ```

  `ProbeFailure.cs` is exact:

  ```csharp
  namespace RespawnSwitch.Core.Game;

  public enum ProbeFailureKind
  {
      ConnectionRefused,
      Timeout,
      TlsRejected,
      InvalidJson,
      SchemaChanged,
      PlayerNotFound,
      AmbiguousPlayer,
      Cancelled,
      Unexpected
  }

  public sealed record ProbeFailure(
      ProbeFailureKind Kind,
      string Code,
      string Message,
      long ObservedAtTimestamp);
  ```

  The state/input/transition contracts are exact:

  ```csharp
  public sealed record RespawnMachineState(
      LifeState LifeState,
      LifeState LastConfirmedLifeState,
      ConnectionState ConnectionState,
      RespawnCycleId? ActiveCycleId,
      ActiveCycleStatus ActiveCycleStatus,
      bool AttachmentIssued,
      long? LastSuccessfulSampleTimestamp,
      long? StaleSinceTimestamp,
      long? FirstAbsentPresenceTimestamp,
      int ConsecutiveAbsentPresenceChecks,
      string? RiotId,
      string? TimelineKey,
      double? LastGameTimeSeconds);

  public abstract record StateMachineInput(long ObservedAtTimestamp);

  public sealed record SuccessfulSampleInput(GameSample Sample)
      : StateMachineInput(Sample.ObservedAtTimestamp);

  public sealed record ProbeFailureInput(ProbeFailure Failure)
      : StateMachineInput(Failure.ObservedAtTimestamp);

  public sealed record PresenceCheckInput(
      GamePresenceSnapshot Presence,
      long Timestamp)
      : StateMachineInput(Timestamp);

  public sealed record TimePulseInput(long Timestamp)
      : StateMachineInput(Timestamp);

  public sealed record RespawnTransition(
      RespawnMachineState Previous,
      RespawnMachineState Current,
      IReadOnlyList<RespawnDomainEvent> Events);
  ```

  Add this internal test factory to `RespawnMachineState`; its implementation validates inputs and returns the record:

  ```text
  CreateForTest(
      LifeState lifeState,
      LifeState lastConfirmedLifeState,
      ConnectionState connectionState,
      ActiveCycleStatus activeCycleStatus,
      RespawnCycleId? activeCycleId,
      long? lastSuccessfulSampleTimestamp,
      long? staleSinceTimestamp,
      bool attachmentIssued = false,
      long? firstAbsentPresenceTimestamp = null,
      int consecutiveAbsentPresenceChecks = 0,
      string? riotId = "Player#NA1",
      string? timelineKey = "test-timeline",
      double? lastGameTimeSeconds = 100)
  ```

  The event records are exact:

  ```csharp
  public abstract record RespawnDomainEvent(long OccurredAtTimestamp);

  public sealed record LifeStateSynchronized(
      LifeState State,
      long Timestamp) : RespawnDomainEvent(Timestamp);

  public sealed record DeathConfirmed(
      RespawnCycleId CycleId,
      GameSample Sample,
      bool IsLateDiscovery) : RespawnDomainEvent(Sample.ObservedAtTimestamp);

  public sealed record AttachmentRequested(
      RespawnCycleId CycleId,
      GameSample Sample) : RespawnDomainEvent(Sample.ObservedAtTimestamp);

  public sealed record DeadSampleUpdated(
      RespawnCycleId CycleId,
      GameSample Sample) : RespawnDomainEvent(Sample.ObservedAtTimestamp);

  public sealed record RespawnConfirmed(
      RespawnCycleId CycleId,
      GameSample Sample) : RespawnDomainEvent(Sample.ObservedAtTimestamp);

  public sealed record ConnectionBecameStale(long Timestamp)
      : RespawnDomainEvent(Timestamp);

  public sealed record ConnectionRestored(long Timestamp)
      : RespawnDomainEvent(Timestamp);

  public sealed record AbandonCycleDueToUnknown(
      RespawnCycleId CycleId,
      long Timestamp) : RespawnDomainEvent(Timestamp);

  public sealed record NoGameConfirmed(
      RespawnCycleId? PriorCycleId,
      long Timestamp) : RespawnDomainEvent(Timestamp);

  public sealed record TimelineResetRequested(
      RespawnCycleId? PriorCycleId,
      string ReasonCode,
      long Timestamp) : RespawnDomainEvent(Timestamp);
  ```

  `RespawnStateMachine` exposes `State`, a public constructor taking options for a fresh `Unknown + NoGame` state, the internal state/options constructor used by tests, and `Apply(StateMachineInput)`:

  ```text
  public RespawnStateMachine(RespawnStateMachineOptions options)
  internal RespawnStateMachine(
      RespawnMachineState initialState,
      RespawnStateMachineOptions options)
  public RespawnMachineState State { get; }
  public RespawnTransition Apply(StateMachineInput input)
  ```

  The production defaults in Part 2's composition root are exactly 1 second stale, 5 seconds dead-stale abandon, 2 seconds no-game confirmation, and 2.0 verified seconds attachment threshold.

  `StateMachineScenario` is test-only and exposes these exact factories so table-driven tests start from explicit immutable state rather than replaying unrelated history:

  ```csharp
  internal static class StateMachineScenario
  {
      public static RespawnStateMachine DeadOnline(
          long timestampFrequency = 1_000) =>
          new(
              RespawnMachineState.CreateForTest(
                  lifeState: LifeState.Dead,
                  lastConfirmedLifeState: LifeState.Dead,
                  connectionState: ConnectionState.Online,
                  activeCycleStatus: ActiveCycleStatus.Active,
                  activeCycleId: new RespawnCycleId(
                      Guid.Parse("00000000-0000-0000-0000-000000000001")),
                  lastSuccessfulSampleTimestamp: 1_000,
                  staleSinceTimestamp: null),
              Options(timestampFrequency));

      public static RespawnStateMachine DeadStale(
          long staleSinceTimestamp,
          long timestampFrequency) =>
          new(
              RespawnMachineState.CreateForTest(
                  lifeState: LifeState.Dead,
                  lastConfirmedLifeState: LifeState.Dead,
                  connectionState: ConnectionState.Stale,
                  activeCycleStatus: ActiveCycleStatus.Active,
                  activeCycleId: new RespawnCycleId(
                      Guid.Parse("00000000-0000-0000-0000-000000000001")),
                  lastSuccessfulSampleTimestamp: 0,
                  staleSinceTimestamp: staleSinceTimestamp),
              Options(timestampFrequency));

      private static RespawnStateMachineOptions Options(long frequency) =>
          new(
              StaleAfter: TimeSpan.FromSeconds(1),
              AbandonDeadCycleAfter: TimeSpan.FromSeconds(5),
              NoGameConfirmationSpan: TimeSpan.FromSeconds(2),
              AttachmentThresholdSeconds: 2.0,
              TimestampFrequency: frequency);
  }
  ```

  `RespawnMachineState.CreateForTest` is `internal`; production code never calls it. Add `[assembly: InternalsVisibleTo("RespawnSwitch.Core.Tests")]` in `AssemblyInfo.cs`. The factory validates impossible combinations, including an Active status without a Cycle ID.

- [ ] **Step 4: Implement `Apply` as an exhaustive input switch**

  ```csharp
  public RespawnTransition Apply(StateMachineInput input) => input switch
  {
      SuccessfulSampleInput sample => ApplySuccessfulSample(sample),
      ProbeFailureInput failure => ApplyProbeFailure(failure),
      PresenceCheckInput presence => ApplyPresence(presence),
      TimePulseInput pulse => ApplyTimePulse(pulse),
      _ => throw new ArgumentOutOfRangeException(nameof(input))
  };
  ```

  Keep connection and life updates in separate private methods. A probe failure may set connection to Stale only after the elapsed threshold; it never mutates `LastConfirmedLifeState`. A no-game transition requires two absent presence checks spanning at least two seconds. An abandoned cycle stays Dead and cannot reissue attachment when Dead returns after Stale.

- [ ] **Step 5: Run GREEN, then all Core tests**

  ```powershell
  dotnet test tests\RespawnSwitch.Core.Tests -c Debug --no-restore
  ```

- [ ] **Step 6: Commit**

  ```powershell
  git add src\RespawnSwitch.Core\Game src\RespawnSwitch.Core\Respawn src\RespawnSwitch.Core\Properties tests\RespawnSwitch.Core.Tests\Respawn
  git commit -m "feat(core): implement orthogonal respawn state machine"
  ```

---

### Task 4: Add the monotonic clock and timeline detector

**Files:**

- Create: `src/RespawnSwitch.Core/Clock/RespawnClockStatus.cs`
- Create: `src/RespawnSwitch.Core/Clock/RespawnClockFrame.cs`
- Create: `src/RespawnSwitch.Core/Clock/RespawnClock.cs`
- Create: `src/RespawnSwitch.Core/Timeline/GameTimelineDetector.cs`
- Create: `src/RespawnSwitch.Core/Timeline/GameTimelineDecision.cs`
- Test: `tests/RespawnSwitch.Core.Tests/Clock/RespawnClockTests.cs`
- Test: `tests/RespawnSwitch.Core.Tests/Timeline/GameTimelineDetectorTests.cs`
- Test helper: `tests/RespawnSwitch.Core.Tests/TestTimeProvider.cs`

**Interfaces:**

- Consumes: verified seconds from `RespawnTimerNormalizer` and `RespawnCycleId`.
- Produces: `RespawnClock.Reanchor`, `MarkWaiting`, `MarkStale`, `Clear`, `Read`; timeline decisions used by Task 6.

  Use these exact contracts:

  ```csharp
  public enum RespawnClockStatus
  {
      Inactive,
      WaitingForVerifiedTimer,
      Running,
      Stale
  }

  public sealed record RespawnClockFrame(
      RespawnClockStatus Status,
      RespawnCycleId? CycleId,
      int? DisplaySeconds,
      double? InterpolatedSeconds,
      long ReadAtTimestamp);

  public enum GameTimelineDecisionKind
  {
      FirstObservation,
      Continue,
      ResetForRiotId,
      ResetForTimelineKey,
      ResetForGameTimeRollback
  }

  public sealed record GameTimelineDecision(
      GameTimelineDecisionKind Kind,
      string TimelineKey,
      string ReasonCode);
  ```

  ```text
  RespawnClock(TimeProvider timeProvider, TimeSpan staleAfter)
  void Reanchor(RespawnCycleId cycleId, double remainingSeconds, long observedAtTimestamp)
  void MarkWaiting(RespawnCycleId cycleId)
  void MarkStale(RespawnCycleId cycleId)
  void Clear(RespawnCycleId? expectedCycleId)
  RespawnClockFrame Read()

  GameTimelineDetector(double rollbackThresholdSeconds)
  GameTimelineDecision Observe(GameSample sample)
  void Reset()
  ```

  Use a 10-second default rollback threshold in Part 2 composition; an exact Riot ID or `TimelineKey` change resets regardless of game-time delta.

- [ ] **Step 1: Write failing clock boundary tests**

  ```csharp
  [Fact]
  public void Read_UsesCeilingAndBecomesStaleOnlyAfterOneSecond()
  {
      var time = new TestTimeProvider(frequency: 1_000);
      var clock = new RespawnClock(time, staleAfter: TimeSpan.FromSeconds(1));
      var cycle = new RespawnCycleId(Guid.Parse("00000000-0000-0000-0000-000000000001"));

      clock.Reanchor(cycle, remainingSeconds: 2.1, observedAtTimestamp: time.GetTimestamp());
      Assert.Equal(3, clock.Read().DisplaySeconds);

      time.Advance(TimeSpan.FromSeconds(1));
      Assert.Equal(RespawnClockStatus.Running, clock.Read().Status);

      time.Advance(TimeSpan.FromMilliseconds(1));
      Assert.Equal(RespawnClockStatus.Stale, clock.Read().Status);
      Assert.Null(clock.Read().DisplaySeconds);
  }
  ```

  Add exact tests for invalid anchors, zero clamp, 100 ms animation refresh, newer sample reanchor, older sample suppression, wall-clock jumps, cycle-mismatch clear, and game-time rollback detection.

- [ ] **Step 2: Run RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Core.Tests -c Debug --filter "FullyQualifiedName~RespawnClockTests|FullyQualifiedName~GameTimelineDetectorTests" --no-restore
  ```

- [ ] **Step 3: Implement the clock with `TimeProvider` only**

  ```csharp
  double elapsedSeconds = _timeProvider
      .GetElapsedTime(_anchorTimestamp, _timeProvider.GetTimestamp())
      .TotalSeconds;

  int display = (int)Math.Ceiling(
      Math.Max(0, _anchorRemainingSeconds - elapsedSeconds));
  ```

  Reject an anchor if the value is non-finite or negative. Reject samples older than the current anchor timestamp. At exactly one second the frame is still Running; at greater than one second it is Stale.

- [ ] **Step 4: Run GREEN and Core regression**

  ```powershell
  dotnet test tests\RespawnSwitch.Core.Tests -c Debug --no-restore
  ```

- [ ] **Step 5: Commit**

  ```powershell
  git add src\RespawnSwitch.Core\Clock src\RespawnSwitch.Core\Timeline tests\RespawnSwitch.Core.Tests\Clock tests\RespawnSwitch.Core.Tests\Timeline tests\RespawnSwitch.Core.Tests\TestTimeProvider.cs
  git commit -m "feat(core): add monotonic respawn clock and timeline detection"
  ```

---

### Task 5: Parse Riot endpoints and match the exact Riot ID

**Files:**

- Create: `src/RespawnSwitch.Riot/Parsing/ActivePlayerParser.cs`
- Create: `src/RespawnSwitch.Riot/Parsing/PlayerListParser.cs`
- Create: `src/RespawnSwitch.Riot/Parsing/GameStatsParser.cs`
- Create: `src/RespawnSwitch.Riot/Parsing/AllGameDataParser.cs`
- Create: `src/RespawnSwitch.Riot/Parsing/RiotIdMatcher.cs`
- Create: `src/RespawnSwitch.Riot/Parsing/RiotJsonError.cs`
- Create: `src/RespawnSwitch.Riot/Parsing/RiotDtos.cs`
- Test: `tests/RespawnSwitch.Riot.Tests/Parsing/*.cs`
- Test fixtures: `tests/RespawnSwitch.Riot.Tests/Fixtures/Riot/*.json`

**Interfaces:**

- Consumes: Core `GameSample` and `SchemaSource`.
- Produces: endpoint-specific parser results and a `RiotSampleAssembler` used by Task 6. Parsers never perform state transitions.

  Internal parser contracts are fixed so endpoint fallback cannot silently change meaning:

  ```csharp
  internal sealed record RiotJsonError(
      string Code,
      string JsonPath,
      string Detail);

  internal sealed record RiotParseResult<T>(
      T? Value,
      IReadOnlyList<RiotJsonError> Errors)
  {
      public bool IsSuccess => Value is not null && Errors.Count == 0;
  }

  internal sealed record RiotPlayerSnapshot(
      string RiotId,
      bool IsDead,
      double? RespawnTimerRaw,
      int? Deaths);

  internal sealed record RiotGameStatsSnapshot(
      double GameTimeSeconds,
      string GameMode);

  internal sealed record ActivePlayerSnapshot(string RiotId);
  ```

  ```text
  ActivePlayerParser.Parse(string json) -> RiotParseResult<ActivePlayerSnapshot>
  PlayerListParser.Parse(string json, string exactRiotId) -> RiotParseResult<RiotPlayerSnapshot>
  GameStatsParser.Parse(string json) -> RiotParseResult<RiotGameStatsSnapshot>
  AllGameDataParser.Parse(string json, string exactRiotId) -> RiotParseResult<(RiotPlayerSnapshot Player, RiotGameStatsSnapshot Stats)>
  RiotSampleAssembler.Assemble(
      long sampleId,
      long observedAtTimestamp,
      RiotPlayerSnapshot player,
      RiotGameStatsSnapshot stats,
      SchemaSource source,
      string timelineKey,
      RespawnTimerSemantics semantics) -> GameSample
  ```

  `RiotSampleAssembler` invokes `RespawnTimerNormalizer`; it always preserves `RespawnTimerRaw` and fills `RespawnTimerSeconds` only for verified current-patch semantics.

- [ ] **Step 1: Add minimal exact fixtures**

  `playerlist-dead.json`:

  ```json
  [
    {
      "riotId": "Player#NA1",
      "championName": "Annie",
      "isDead": true,
      "respawnTimer": 18.75,
      "scores": { "deaths": 1 }
    },
    {
      "riotId": "Other#NA1",
      "championName": "Annie",
      "isDead": false,
      "respawnTimer": 0,
      "scores": { "deaths": 0 }
    }
  ]
  ```

  Also add fixtures for missing `isDead`, invalid types, duplicate champion names, same Game Name with a different Tag, positive timer while alive, compatible `allgamedata`, and valid `gamestats`.

- [ ] **Step 2: Write failing parser tests**

  ```csharp
  [Fact]
  public void PlayerList_UsesFullRiotIdAndNeverChampionNameOrArrayPosition()
  {
      string json = Fixture.Read("playerlist-dead.json");
      var result = PlayerListParser.Parse(json, "Player#NA1");

      Assert.True(result.IsSuccess);
      Assert.Equal("Player#NA1", result.Player!.RiotId);
      Assert.True(result.Player.IsDead);
      Assert.Equal(18.75, result.Player.RespawnTimerRaw);
  }
  ```

  Tests must assert structured failures for absent, ambiguous, or type-invalid fields; no parser may silently coerce a string to bool/number.

- [ ] **Step 3: Run RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Riot.Tests -c Debug --filter FullyQualifiedName~Parsing --no-restore
  ```

- [ ] **Step 4: Implement endpoint-specific parsers with `Utf8JsonReader` or strict `JsonSerializerOptions`**

  Use `JsonNumberHandling.Strict`, case-sensitive property mapping for documented fields, finite-number checks, and structured error codes such as `riot.schema.playerlist.isdead-missing`. `RiotSampleAssembler` sets `RespawnTimerRaw` but leaves `RespawnTimerSeconds=null` until verified semantics are supplied.

- [ ] **Step 5: Run GREEN and regression**

  ```powershell
  dotnet test tests\RespawnSwitch.Riot.Tests -c Debug --no-restore
  dotnet test tests\RespawnSwitch.Core.Tests -c Debug --no-restore
  ```

- [ ] **Step 6: Commit**

  ```powershell
  git add src\RespawnSwitch.Riot\Parsing tests\RespawnSwitch.Riot.Tests\Parsing tests\RespawnSwitch.Riot.Tests\Fixtures
  git commit -m "feat(riot): parse live client data safely"
  ```

---

### Task 6: Add the fixed endpoint, strict TLS, cancellation, and independent polling cadence

**Files:**

- Create: `src/RespawnSwitch.Riot/Http/RiotEndpoint.cs`
- Create: `src/RespawnSwitch.Riot/Http/RiotTlsCertificateValidator.cs`
- Create: `src/RespawnSwitch.Riot/Http/RiotHttpClientFactory.cs`
- Create: `src/RespawnSwitch.Riot/Http/RiotLiveClientApi.cs`
- Create: `src/RespawnSwitch.Riot/Http/RiotRequestTimeouts.cs`
- Create: `src/RespawnSwitch.Riot/Polling/LeagueGameProbe.cs`
- Create: `src/RespawnSwitch.Riot/Polling/LeaguePollingSchedule.cs`
- Create: `src/RespawnSwitch.Riot/Polling/ProbeSequenceGuard.cs`
- Create: `src/RespawnSwitch.Application/Monitoring/ILeagueGameProbe.cs`
- Create: `src/RespawnSwitch.Application/Monitoring/LeagueProbeObservation.cs`
- Create: `src/RespawnSwitch.Riot/Certificates/riotgames.pem`
- Create: `src/RespawnSwitch.Riot/Certificates/riotgames.pem.sha256`
- Create: `src/RespawnSwitch.Riot/Certificates/README.md`
- Test: `tests/RespawnSwitch.Riot.Tests/Http/*.cs`
- Test: `tests/RespawnSwitch.Riot.Tests/Polling/*.cs`
- Test helper: `tests/RespawnSwitch.Riot.Tests/TestHttp/SequenceHttpMessageHandler.cs`

**Interfaces:**

- Consumes: parser results from Task 5, timeline detector from Task 4, and locked monitoring contracts.
- Produces: `LeagueGameProbe : ILeagueGameProbe` and an HTTP client that cannot escape the literal Riot loopback origin.

  Use these exact external-path and polling contracts:

  ```csharp
  public static class RiotEndpoint
  {
      public static Uri Origin { get; } = new("https://127.0.0.1:2999/");

      private static readonly HashSet<string> AllowedPaths = new(
          StringComparer.Ordinal)
      {
          "/liveclientdata/activeplayername",
          "/liveclientdata/playerlist",
          "/liveclientdata/gamestats",
          "/liveclientdata/allgamedata",
          "/swagger/v3/openapi.json"
      };

      public static bool Allows(Uri requestUri) =>
          requestUri.IsAbsoluteUri &&
          requestUri.Scheme == Uri.UriSchemeHttps &&
          requestUri.Host == "127.0.0.1" &&
          requestUri.Port == 2999 &&
          string.IsNullOrEmpty(requestUri.UserInfo) &&
          string.IsNullOrEmpty(requestUri.Query) &&
          string.IsNullOrEmpty(requestUri.Fragment) &&
          AllowedPaths.Contains(requestUri.AbsolutePath);
  }

  public sealed record RiotRequestTimeouts(
      TimeSpan ActivePlayer,
      TimeSpan PlayerList,
      TimeSpan GameStats,
      TimeSpan AllGameData,
      TimeSpan OpenApi);

  public sealed record LeaguePollingSchedule(
      TimeSpan PlayerListInterval,
      TimeSpan GameStatsInterval,
      TimeSpan NoGameInitialBackoff,
      TimeSpan NoGameMaximumBackoff);
  ```

  Production defaults are 750 ms per request, 250 ms player-list interval, one-second game-stats interval, one-second initial no-game backoff, and two-second maximum no-game backoff. Each API method constructs an absolute URI and validates it with `RiotEndpoint.Allows` immediately before GET:

  ```text
  RiotLiveClientApi.GetActivePlayerAsync(CancellationToken) -> string JSON
  RiotLiveClientApi.GetPlayerListAsync(CancellationToken) -> string JSON
  RiotLiveClientApi.GetGameStatsAsync(CancellationToken) -> string JSON
  RiotLiveClientApi.GetAllGameDataAsync(CancellationToken) -> string JSON
  RiotLiveClientApi.GetOpenApiAsync(CancellationToken) -> string JSON

  LeagueGameProbe(
      RiotLiveClientApi api,
      RespawnTimerSemantics semantics,
      LeaguePollingSchedule schedule,
      TimeProvider timeProvider)
      : ILeagueGameProbe
  ```

  `LeagueGameProbe` owns one exact active Riot ID per timeline, one monotonically increasing request/sample sequence, and a generated timeline key. It replaces the timeline key only when exact Riot ID changes or `GameTimelineDetector` reports reset; old in-flight results are discarded by `ProbeSequenceGuard`.

- [ ] **Step 1: Write failing endpoint and request-policy tests**

  ```csharp
  [Theory]
  [InlineData("https://127.0.0.1:2999/liveclientdata/playerlist", true)]
  [InlineData("https://localhost:2999/liveclientdata/playerlist", false)]
  [InlineData("https://[::1]:2999/liveclientdata/playerlist", false)]
  [InlineData("http://127.0.0.1:2999/liveclientdata/playerlist", false)]
  [InlineData("https://127.0.0.1:3000/liveclientdata/playerlist", false)]
  public void Allows_OnlyLiteralHttpsLoopbackOrigin(string value, bool expected)
  {
      Assert.Equal(expected, RiotEndpoint.Allows(new Uri(value)));
  }
  ```

  Add tests that redirects are not followed, proxies are disabled, only GET is constructed, per-request timeout cancels, caller cancellation propagates, old responses cannot overwrite newer sequences, `playerlist` does not wait for `gamestats`, `allgamedata` is unused when `playerlist` is valid, a failed/incompatible `playerlist` makes one bounded `allgamedata` compatibility attempt, and an observed schema change makes one rate-limited OpenAPI diagnostic request without feeding OpenAPI data into life-state decisions.

- [ ] **Step 2: Write failing certificate validator tests with generated test certificates**

  Test exact pinned-leaf acceptance and custom-root-chain acceptance. Reject wrong root, expired/not-yet-valid cert, non-server-auth EKU, malformed PEM, wrong request origin, and any fallback to plain HTTP. Verify the implementation never changes the system certificate store or `ServicePointManager.ServerCertificateValidationCallback`.

- [ ] **Step 3: Run RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Riot.Tests -c Debug --filter "FullyQualifiedName~Http|FullyQualifiedName~Polling" --no-restore
  ```

- [ ] **Step 4: Download and fingerprint Riot's official PEM**

  ```powershell
  Invoke-WebRequest `
    -Uri 'https://static.developer.riotgames.com/docs/lol/riotgames.pem' `
    -OutFile 'src\RespawnSwitch.Riot\Certificates\riotgames.pem'

  (Get-FileHash `
    -Algorithm SHA256 `
    -LiteralPath 'src\RespawnSwitch.Riot\Certificates\riotgames.pem').Hash.ToLowerInvariant() |
    Set-Content -Encoding ascii 'src\RespawnSwitch.Riot\Certificates\riotgames.pem.sha256'
  ```

  Record the exact source URL in the certificate directory README. This network action requires explicit permission during execution.

  `src/RespawnSwitch.Riot/Certificates/README.md` contains:

  ```markdown
  # Riot Live Client Data certificate

  Source: https://static.developer.riotgames.com/docs/lol/riotgames.pem

  The adjacent `riotgames.pem.sha256` is generated from the downloaded bytes. RespawnSwitch uses this material only for its application-local validation of `https://127.0.0.1:2999`; it does not modify the Windows certificate store or disable global TLS verification.
  ```

- [ ] **Step 5: Implement the strict HTTP handler**

  ```csharp
  var handler = new HttpClientHandler
  {
      AllowAutoRedirect = false,
      UseProxy = false,
      Proxy = null,
      ServerCertificateCustomValidationCallback = validator.Validate
  };

  return new HttpClient(handler, disposeHandler: true)
  {
      BaseAddress = RiotEndpoint.Origin,
      Timeout = Timeout.InfiniteTimeSpan
  };
  ```

  The validator first rejects any request outside the literal origin. It accepts an exact DER match to a pinned official PEM certificate only after validity/EKU checks; otherwise it requires a chain to a configured PEM custom root and an IP SAN for `127.0.0.1`. Gate A decides whether the current Riot deployment follows the exact-leaf or custom-root branch.

- [ ] **Step 6: Implement independent polling loops and sequence suppression**

  `playerlist` runs every 250 ms in an active game; `gamestats` runs every one second; `activeplayername` runs on first connection, timeline reset, or failed exact match. Do not serialize the high-frequency player request behind lower-frequency calls. When `playerlist` fails or has an incompatible schema, make one bounded `allgamedata` attempt for that cadence; never request it on the success path. A schema-change diagnostic may fetch OpenAPI at most once per timeline and once per five minutes. `ProbeSequenceGuard` accepts only a monotonically newer sequence. Connection refusal, timeout, TLS rejection, invalid JSON, and schema changes produce typed `LeagueProbeFailed` values—not Alive samples.

- [ ] **Step 7: Run GREEN, all non-desktop tests, and architecture grep**

  ```powershell
  dotnet test tests\RespawnSwitch.Riot.Tests -c Debug --no-restore
  dotnet test tests\RespawnSwitch.Core.Tests -c Debug --no-restore
  rg -n "DangerousAcceptAnyServerCertificateValidator|ServerCertificateValidationCallback\s*=" src
  ```

  Expected: tests pass; the grep has no production match other than the local handler assignment to `validator.Validate` and no dangerous accept-all callback.

- [ ] **Step 8: Commit**

  ```powershell
  git add src\RespawnSwitch.Application\Monitoring src\RespawnSwitch.Riot\Http src\RespawnSwitch.Riot\Polling src\RespawnSwitch.Riot\Certificates tests\RespawnSwitch.Riot.Tests
  git commit -m "feat(riot): add secure cancellable live client probe"
  ```

---

### Task 7: Build the current-patch semantic probe and evidence report

**Files:**

- Create: `tools/RespawnSwitch.SemanticProbe/Program.cs`
- Create: `tools/RespawnSwitch.SemanticProbe/SemanticProbeOptions.cs`
- Create: `tools/RespawnSwitch.SemanticProbe/SemanticProbeRecorder.cs`
- Create: `tools/RespawnSwitch.SemanticProbe/SemanticProbeObservation.cs`
- Create: `tools/RespawnSwitch.SemanticProbe/OperatorMarker.cs`
- Create: `tools/RespawnSwitch.SemanticProbe/SemanticProbeAnalyzer.cs`
- Create: `tools/RespawnSwitch.SemanticProbe/SemanticProbeReport.cs`
- Create: `tools/RespawnSwitch.SemanticProbe/JsonLinesProbeWriter.cs`
- Create: `tools/RespawnSwitch.SemanticProbe/README.md`
- Test: `tests/RespawnSwitch.Riot.Tests/SemanticProbe/SemanticProbeAnalyzerTests.cs`
- Create after real run: `docs/acceptance/riot-semantic-probe.md`

**Interfaces:**

- Consumes: `ILeagueGameProbe`, raw Riot observations, and monotonic timestamps.
- Produces: redacted JSONL plus a report whose ID can populate `RespawnTimerSemantics.EvidenceReportId`; Gate A consumes that report.

- [ ] **Step 1: Write failing analyzer tests**

  First add the probe project reference required by these tests:

  ```powershell
  dotnet add tests\RespawnSwitch.Riot.Tests reference tools\RespawnSwitch.SemanticProbe
  ```

  ```csharp
  [Fact]
  public void Analyze_RefusesVerifiedSecondsWithoutVisibleCountdownMarker()
  {
      var report = SemanticProbeAnalyzer.Analyze(
          ProbeFixture.OrdinaryDeathWithoutVideoMarker());

      Assert.False(report.Passed);
      Assert.Contains(
          "visible-countdown-evidence-missing",
          report.FailureCodes);
  }
  ```

  Add tests for strictly increasing sequence/timestamps, finite non-negative timer during ordinary death, approximately `-1 raw unit/second` slope, reset timeline detection, pause not being respawn, fast respawn cancellation, and special-mechanism experimental classification.

- [ ] **Step 2: Run RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Riot.Tests -c Debug --filter FullyQualifiedName~SemanticProbe --no-restore
  ```

- [ ] **Step 3: Implement the exact observation schema**

  ```csharp
  public sealed record SemanticProbeObservation(
      long Sequence,
      long ObservedAtTimestamp,
      DateTimeOffset ObservedAtUtc,
      long HttpResponseCompletedAtTimestamp,
      bool IsDead,
      double? RespawnTimerRaw,
      double GameTimeSeconds,
      string GameMode,
      SchemaSource SchemaSource,
      string MaskedRiotId,
      OperatorMarker? Marker);
  ```

  Mask the Riot ID before writing. Do not write the full API response, access tokens, client command lines, or full Riot ID. Wall-clock time exists only for coarse video alignment; analysis uses monotonic timestamps.

- [ ] **Step 4: Implement CLI and deterministic exit codes**

  ```text
  0 = capture completed and report passed
  2 = capture completed but semantic evidence failed
  3 = Riot endpoint unavailable or TLS rejected
  4 = invalid command arguments
  ```

  Supported arguments are `--output`, `--report`, `--sample-ms` (default 250), and `--duration-minutes` (default 15). Console keys create markers for ordinary death, visible countdown value, respawn, pause, reset, fast respawn, and special mechanism.

- [ ] **Step 5: Run GREEN and a fake-data dry run**

  ```powershell
  dotnet test tests\RespawnSwitch.Riot.Tests -c Debug --no-restore
  dotnet run --project tools\RespawnSwitch.SemanticProbe -c Debug -- --help
  ```

- [ ] **Step 6: Commit the probe before touching the real game**

  ```powershell
  git add tools\RespawnSwitch.SemanticProbe tests\RespawnSwitch.Riot.Tests\SemanticProbe
  git commit -m "feat(tools): add current-patch respawn semantic probe"
  ```

- [ ] **Step 7: Execute Gate A in League Practice Tool**

  User action required: open Practice Tool in borderless mode and make the champion die normally. Record the visible respawn countdown with screen recording or timestamped operator markers. Also exercise training pause, reset, fast respawn, and one special respawn mechanism when safely possible.

  ```powershell
  dotnet run --project tools\RespawnSwitch.SemanticProbe -c Release -- `
    --output artifacts\semantic-probe\training-tool.jsonl `
    --report artifacts\semantic-probe\training-tool-report.json `
    --sample-ms 250 `
    --duration-minutes 15
  ```

- [ ] **Step 8: Write and commit the evidence summary**

  `docs/acceptance/riot-semantic-probe.md` must record:

  ```text
  BuildCommit
  WindowsVersion
  LeaguePatch
  GameMode
  CertificateBranch (ExactPinnedLeaf or PinnedCustomRoot)
  ActivePlayerUniqueMatch
  RawTimerFiniteAndNonNegative
  RawUnitsPerVisibleSecond
  VisibleCountdownMaxErrorSeconds
  PauseResult
  ResetResult
  FastRespawnResult
  SpecialMechanismResult
  EvidenceSha256
  Passed
  ```

  ```powershell
  git add docs\acceptance\riot-semantic-probe.md
  git commit -m "test: record Riot respawn semantic evidence"
  ```

---

## Gate A: Mandatory Stop Condition Before Part 2

Part 2 may begin only when all are true:

- Current real client accepts the bundled certificate policy.
- `activeplayername`, `playerlist`, and `gamestats` parse on the current patch.
- Full Riot ID uniquely identifies the local player.
- Ordinary-death raw timer is finite, non-negative, and approximately decreases by one unit per visible second.
- Visible countdown and analyzed timer agree within one second using external video/operator evidence, not circular comparison against the same API field.
- Practice Tool pause, reset, and fast respawn have recorded outcomes.
- At least one special mechanism has a recorded outcome or an explicit “not safely reproducible” reason and remains experimental.
- `docs/acceptance/riot-semantic-probe.md` says `Passed: true` and includes an evidence SHA-256.

If Gate A fails:

- Certificate or endpoint failure → return to Task 6; never disable TLS globally.
- Schema or Riot ID failure → return to Task 5 or Task 6; never match by champion name or array position.
- Timer semantics failure → keep `respawnTimer` raw/unverified, disable countdown seconds and the 2-second attachment rule, and stop before Part 2.

## Part 1 Completion Verification

```powershell
$env:DOTNET_CLI_HOME = Join-Path $PWD 'work\dotnet-home'
$env:NUGET_PACKAGES = Join-Path $PWD 'work\nuget-packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

dotnet restore RespawnSwitch.sln --locked-mode
dotnet build RespawnSwitch.sln -c Release --no-restore
dotnet test tests\RespawnSwitch.Core.Tests -c Release --no-build
dotnet test tests\RespawnSwitch.Riot.Tests -c Release --no-build
dotnet test tests\RespawnSwitch.Architecture.Tests -c Release --no-build
```

Expected: all automated tests pass, Gate A evidence is committed, and no Part 2 implementation files contain unreviewed production behavior.
