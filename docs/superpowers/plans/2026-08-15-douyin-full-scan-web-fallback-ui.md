# Douyin Full-Scan, Web Fallback, and UI Upgrade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade RespawnSwitch 0.2.0 to discover Douyin across every local fixed disk, fall back to the official website when no safe desktop target is available, and present the workflow in a polished Chinese WPF interface.

**Architecture:** Pure discovery contracts, selection rules, and web-cycle guards live in `RespawnSwitch.Application`; Windows-specific drive enumeration, filesystem traversal, registry/process/shortcut lookup, file identity validation, and browser launch live in `RespawnSwitch.Windows`. The WPF app owns settings migration, background discovery lifetime, progress presentation, and integration with the existing respawn coordinator.

**Tech Stack:** .NET 8, C# 12, WPF, Windows Registry, `System.Diagnostics.FileVersionInfo`, Authenticode/X.509 metadata, xUnit, PowerShell self-contained `win-x64` publishing.

## Global Constraints

- Scan every Windows `DriveType.Fixed` volume; do not scan network or removable drives.
- Ignore inaccessible entries and do not traverse directories carrying `FileAttributes.ReparsePoint`.
- Never execute a discovered candidate during validation.
- Never open a League process handle or use input injection/global media keys.
- Browser fallback URL is the constant `https://www.douyin.com/` and opens at most once per respawn cycle.
- Browser fallback is never paused globally on respawn; only League focus is restored.
- Discovery runs off the WPF dispatcher and publishes bounded progress updates.
- Candidate ambiguity fails closed and is visible in settings.
- Preserve Windows x64, self-contained, directory publishing with trimming and single-file publishing disabled.
- Add no external NuGet dependencies.

## File Map

- `src/RespawnSwitch.Application/Douyin/DouyinDiscoveryContracts.cs`: discovery value types and service interfaces.
- `src/RespawnSwitch.Application/Douyin/DouyinCandidateSelector.cs`: deterministic, pure candidate ranking and ambiguity handling.
- `src/RespawnSwitch.Application/Douyin/DouyinLaunchPlanResolver.cs`: desktop/web/unavailable decision and per-cycle web guard.
- `src/RespawnSwitch.Windows/DouyinDiscovery/FixedDriveCatalog.cs`: fixed-volume enumeration.
- `src/RespawnSwitch.Windows/DouyinDiscovery/FileSystemDouyinScanner.cs`: cancellable full-disk traversal and progress.
- `src/RespawnSwitch.Windows/DouyinDiscovery/DouyinCandidateValidator.cs`: product/version/signature validation.
- `src/RespawnSwitch.Windows/DouyinDiscovery/WindowsDouyinQuickSources.cs`: saved/running/registry/shortcut candidates.
- `src/RespawnSwitch.Windows/DouyinDiscovery/WindowsDouyinInstallationDetector.cs`: quick sources followed by full scan.
- `src/RespawnSwitch.Windows/DouyinDiscovery/DouyinWebFallbackLauncher.cs`: fixed-URL browser launch.
- `src/RespawnSwitch.App/AppSettings.cs`: 0.1 JSON migration and new discovery settings.
- `src/RespawnSwitch.App/DouyinDiscoveryController.cs`: background operation, cancellation, snapshots, and UI events.
- `src/RespawnSwitch.App/RespawnCoordinator.cs`: consume the current discovery result and web fallback.
- `src/RespawnSwitch.App/MainWindow.xaml`: modern card layout and reusable local styles.
- `src/RespawnSwitch.App/MainWindow.xaml.cs`: scan commands, file picker, status rendering, and lifecycle.

---

### Task 1: Lock Discovery Contracts, Selection, and Settings Migration

**Files:**

- Create: `src/RespawnSwitch.Application/Douyin/DouyinDiscoveryContracts.cs`
- Create: `src/RespawnSwitch.Application/Douyin/DouyinCandidateSelector.cs`
- Modify: `src/RespawnSwitch.App/AppSettings.cs`
- Test: `tests/RespawnSwitch.Application.Tests/Douyin/DouyinCandidateSelectorTests.cs`
- Test: `tests/RespawnSwitch.Desktop.IntegrationTests/Settings/AppSettingsMigrationTests.cs`

**Interfaces:**

- Produces:

  ```csharp
  public enum DouyinDiscoverySource { SavedPath, RunningProcess, Registry, StartMenu, FullDisk }
  public enum DouyinDiscoveryStatus { NotStarted, Scanning, Found, NotFound, Ambiguous, Cancelled, Failed }
  public enum DouyinDiscoveryMode { Auto, Manual, WebOnly }

  public sealed record DouyinCandidate(
      string NormalizedPath,
      DouyinDiscoverySource Source,
      bool IsRunning,
      bool HasTrustedSignature,
      string? SignatureThumbprint,
      Version FileVersion,
      DateTimeOffset LastWriteTimeUtc,
      string ProductName,
      string FileDescription);

  public sealed record DouyinScanProgress(
      string? CurrentDrive,
      string? CurrentDirectory,
      long DirectoriesScanned,
      long DirectoriesSkipped,
      int CandidatesFound);

  public sealed record DouyinDiscoveryResult(
      DouyinDiscoveryStatus Status,
      DouyinCandidate? Selected,
      IReadOnlyList<DouyinCandidate> Candidates,
      DouyinScanProgress Progress,
      string Code);

  public interface IDouyinInstallationDetector
  {
      Task<DouyinDiscoveryResult> DetectAsync(
          string? savedPath,
          IProgress<DouyinScanProgress>? progress,
          CancellationToken cancellationToken);
  }
  ```

- `DouyinCandidateSelector.Select` returns `Found` for one unique highest-ranked normalized path and `Ambiguous` when different paths tie at the top rank.
- `AppSettings` becomes:

  ```csharp
  public sealed record AppSettings(
      string? PreferredDouyinPath,
      bool AutoDetectDouyin,
      bool OpenWebFallback,
      DouyinDiscoveryMode DiscoveryMode,
      string? LastValidatedSignatureThumbprint,
      string? DouyinWindowClass,
      string? SourceAppUserModelId,
      string? DiagnosticFingerprint);
  ```

- `AppSettingsStore.LoadAsync` reads the former `DouyinPath` JSON property when `PreferredDouyinPath` is absent.

- [ ] **Step 1: Write failing selection tests**

  Cover running > saved > registry/start-menu > full-disk, newer version ordering, normalized-path deduplication, and exact top-rank ambiguity:

  ```csharp
  [Fact]
  public void Select_TwoDifferentRunningCandidatesWithSameRank_ReturnsAmbiguous()
  {
      var result = DouyinCandidateSelector.Select([
          Candidate(@"C:\Apps\Douyin\douyin.exe", DouyinDiscoverySource.RunningProcess, running: true),
          Candidate(@"D:\Apps\Douyin\douyin.exe", DouyinDiscoverySource.RunningProcess, running: true)
      ]);

      Assert.Equal(DouyinDiscoveryStatus.Ambiguous, result.Status);
      Assert.Null(result.Selected);
  }
  ```

- [ ] **Step 2: Run the selection tests and verify RED**

  Run:

  ```powershell
  dotnet test tests\RespawnSwitch.Application.Tests -c Debug --filter FullyQualifiedName~DouyinCandidateSelectorTests --no-restore
  ```

  Expected: compile failure because discovery contracts and selector do not exist.

- [ ] **Step 3: Implement contracts and minimal selector**

  Rank by `IsRunning`, source precedence, `HasTrustedSignature`, `FileVersion`, then `LastWriteTimeUtc`. Deduplicate with `StringComparer.OrdinalIgnoreCase`. Determine ambiguity before applying path text as the final stable sort, so alphabetic order never hides a true tie.

- [ ] **Step 4: Run selection tests and Application regression**

  ```powershell
  dotnet test tests\RespawnSwitch.Application.Tests -c Debug --no-restore
  ```

  Expected: all discovered tests pass.

- [ ] **Step 5: Write failing settings migration tests**

  Add an old 0.1 JSON fixture containing `DouyinPath`, `DouyinWindowClass`, AUMID, and fingerprint. Assert that loading produces `PreferredDouyinPath`, retains identity fields, and defaults `AutoDetectDouyin` and `OpenWebFallback` to `true`.

- [ ] **Step 6: Run migration tests and verify RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Desktop.IntegrationTests -c Debug --filter FullyQualifiedName~AppSettingsMigrationTests --no-restore
  ```

  Expected: tests fail because the new schema/migration overload is missing.

- [ ] **Step 7: Implement schema migration and atomic save**

  Add `AppSettingsStore.Deserialize(string json)` for deterministic tests. Use `JsonDocument` to distinguish an absent Boolean from `false`, migrate `DouyinPath`, and keep `SaveAsync` as temp-file plus replace/move.

- [ ] **Step 8: Run migration and full settings tests**

  ```powershell
  dotnet test tests\RespawnSwitch.Desktop.IntegrationTests -c Debug --filter FullyQualifiedName~Settings --no-restore
  ```

- [ ] **Step 9: Commit Task 1**

  ```powershell
  git add src\RespawnSwitch.Application\Douyin src\RespawnSwitch.App\AppSettings.cs tests\RespawnSwitch.Application.Tests\Douyin tests\RespawnSwitch.Desktop.IntegrationTests\Settings
  git commit -m "feat(discovery): add Douyin selection and settings migration"
  ```

---

### Task 2: Implement Safe Full-Disk Scanning and Candidate Validation

**Files:**

- Create: `src/RespawnSwitch.Windows/DouyinDiscovery/IFixedDriveCatalog.cs`
- Create: `src/RespawnSwitch.Windows/DouyinDiscovery/FixedDriveCatalog.cs`
- Create: `src/RespawnSwitch.Windows/DouyinDiscovery/IDouyinCandidateValidator.cs`
- Create: `src/RespawnSwitch.Windows/DouyinDiscovery/DouyinCandidateValidator.cs`
- Create: `src/RespawnSwitch.Windows/DouyinDiscovery/FileSystemDouyinScanner.cs`
- Test: `tests/RespawnSwitch.Windows.Tests/DouyinDiscovery/FileSystemDouyinScannerTests.cs`
- Test: `tests/RespawnSwitch.Windows.Tests/DouyinDiscovery/DouyinCandidateValidatorTests.cs`

**Interfaces:**

- Consumes: Task 1 discovery records.
- Produces:

  ```csharp
  public interface IFixedDriveCatalog
  {
      IReadOnlyList<string> GetFixedDriveRoots();
  }

  public interface IDouyinCandidateValidator
  {
      ValueTask<DouyinCandidate?> ValidateAsync(
          string path,
          DouyinDiscoverySource source,
          bool isRunning,
          CancellationToken cancellationToken);
  }

  public sealed class FileSystemDouyinScanner
  {
      public Task<IReadOnlyList<DouyinCandidate>> ScanAsync(
          IProgress<DouyinScanProgress>? progress,
          CancellationToken cancellationToken);
  }
  ```

- [ ] **Step 1: Write failing fixed-drive and traversal tests**

  Use temporary trees on the test volume and a fake `IFixedDriveCatalog`. Assert discovery below a nonstandard nested directory, exact filename matching, two-drive enumeration, duplicate path suppression, cancellation, and continuation after an inaccessible/disappearing directory. Test `FileSystemDouyinScanner.ShouldTraverse(FileAttributes.ReparsePoint)` returns `false`.

- [ ] **Step 2: Run scanner tests and verify RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Windows.Tests -c Debug --filter FullyQualifiedName~FileSystemDouyinScannerTests --no-restore
  ```

  Expected: compile failure because the scanner interfaces and class are absent.

- [ ] **Step 3: Implement drive enumeration and iterative scanner**

  `FixedDriveCatalog` returns ready `DriveInfo` items whose `DriveType == DriveType.Fixed`. `FileSystemDouyinScanner` uses a `Stack<string>`, checks directory attributes before pushing children, catches access/path/I/O exceptions per directory, and reports every 128 processed directories plus every candidate.

- [ ] **Step 4: Run scanner tests and verify GREEN**

  ```powershell
  dotnet test tests\RespawnSwitch.Windows.Tests -c Debug --filter FullyQualifiedName~FileSystemDouyinScannerTests --no-restore
  ```

- [ ] **Step 5: Write failing candidate validator tests**

  Extract metadata behind an internal `IDouyinFileMetadataReader` so tests provide signed/unsigned Douyin product metadata without manufacturing certificates. Assert missing file, wrong filename, unrelated product with no trusted identity, and invalid version are rejected; a signed `Douyin`/`抖音` product returns a normalized candidate.

- [ ] **Step 6: Run validator tests and verify RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Windows.Tests -c Debug --filter FullyQualifiedName~DouyinCandidateValidatorTests --no-restore
  ```

- [ ] **Step 7: Implement metadata reader and validator**

  Use `FileVersionInfo.GetVersionInfo`, `File.GetLastWriteTimeUtc`, `Path.GetFullPath`, and `X509Certificate.CreateFromSignedFile`. Dispose certificates. Product recognition uses ordinal-ignore-case containment of `douyin` or `抖音`; a signature thumbprint supplements, but does not replace, product identity unless it matches the saved trusted thumbprint supplied to the validator.

- [ ] **Step 8: Run Windows unit regression**

  ```powershell
  dotnet test tests\RespawnSwitch.Windows.Tests -c Debug --no-restore
  ```

- [ ] **Step 9: Commit Task 2**

  ```powershell
  git add src\RespawnSwitch.Windows\DouyinDiscovery tests\RespawnSwitch.Windows.Tests\DouyinDiscovery
  git commit -m "feat(windows): scan fixed disks for verified Douyin installs"
  ```

---

### Task 3: Compose Quick Sources, Full Scan, and Browser Fallback

**Files:**

- Create: `src/RespawnSwitch.Windows/DouyinDiscovery/WindowsDouyinQuickSources.cs`
- Create: `src/RespawnSwitch.Windows/DouyinDiscovery/WindowsDouyinInstallationDetector.cs`
- Create: `src/RespawnSwitch.Application/Douyin/DouyinLaunchPlanResolver.cs`
- Create: `src/RespawnSwitch.Windows/DouyinDiscovery/DouyinWebFallbackLauncher.cs`
- Test: `tests/RespawnSwitch.Windows.Tests/DouyinDiscovery/WindowsDouyinInstallationDetectorTests.cs`
- Test: `tests/RespawnSwitch.Application.Tests/Douyin/DouyinLaunchPlanResolverTests.cs`

**Interfaces:**

- Produces:

  ```csharp
  public enum DouyinLaunchMode { Desktop, Web, Unavailable }

  public sealed record DouyinLaunchPlan(
      DouyinLaunchMode Mode,
      DouyinCandidate? DesktopCandidate,
      Uri? WebUri,
      string Code);

  public sealed record DouyinRuntimePreferences(
      DouyinDiscoveryMode DiscoveryMode,
      bool OpenWebFallback);

  public interface IDouyinWebFallbackLauncher
  {
      ValueTask<bool> OpenAsync(CancellationToken cancellationToken);
  }

  public sealed class WebFallbackCycleGuard
  {
      public bool TryBegin(RespawnCycleId cycleId);
      public void Complete(RespawnCycleId cycleId);
      public void Reset();
  }
  ```

- The only browser URI is `new Uri("https://www.douyin.com/")`.

- [ ] **Step 1: Write failing detector orchestration tests**

  Assert a valid saved path stops before full scan, a running candidate wins, quick-source failure invokes every fake fixed volume, cancellation returns `Cancelled`, and no candidates returns `NotFound` rather than `Failed`.

- [ ] **Step 2: Run detector tests and verify RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Windows.Tests -c Debug --filter FullyQualifiedName~WindowsDouyinInstallationDetectorTests --no-restore
  ```

- [ ] **Step 3: Implement quick sources and detector composition**

  Quick sources enumerate the saved path, Douyin processes, App Paths, four uninstall registry views, and `.lnk` targets below per-user/all-users Start Menu directories. Validate every emitted path through `IDouyinCandidateValidator`; never trust source strings directly. Only start `FileSystemDouyinScanner` when selection of quick candidates is not `Found`.

- [ ] **Step 4: Run detector tests and Windows regression**

  ```powershell
  dotnet test tests\RespawnSwitch.Windows.Tests -c Debug --no-restore
  ```

- [ ] **Step 5: Write failing launch plan and cycle-guard tests**

  Assert desktop is selected when a candidate exists, web is selected only when enabled and desktop is absent, unavailable is selected when fallback is disabled, and `TryBegin` returns true once per `RespawnCycleId` until completion/reset.

- [ ] **Step 6: Run launch tests and verify RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Application.Tests -c Debug --filter FullyQualifiedName~DouyinLaunchPlanResolverTests --no-restore
  ```

- [ ] **Step 7: Implement resolver, guard, and fixed browser launcher**

  `DouyinWebFallbackLauncher.OpenAsync` uses `ProcessStartInfo("https://www.douyin.com/") { UseShellExecute = true }`, maps process-launch exceptions to `false`, and never accepts a caller-supplied URL.

- [ ] **Step 8: Run Application and Windows regression**

  ```powershell
  dotnet test tests\RespawnSwitch.Application.Tests -c Debug --no-restore
  dotnet test tests\RespawnSwitch.Windows.Tests -c Debug --no-restore
  ```

- [ ] **Step 9: Commit Task 3**

  ```powershell
  git add src\RespawnSwitch.Application\Douyin src\RespawnSwitch.Windows\DouyinDiscovery tests\RespawnSwitch.Application.Tests\Douyin tests\RespawnSwitch.Windows.Tests\DouyinDiscovery
  git commit -m "feat(discovery): add quick sources and web fallback"
  ```

---

### Task 4: Integrate Background Discovery with Respawn Runtime

**Files:**

- Create: `src/RespawnSwitch.App/DouyinDiscoveryController.cs`
- Modify: `src/RespawnSwitch.App/RespawnCoordinator.cs`
- Modify: `src/RespawnSwitch.App/App.xaml.cs`
- Test: `tests/RespawnSwitch.Desktop.IntegrationTests/Discovery/DouyinDiscoveryControllerTests.cs`
- Test: `tests/RespawnSwitch.Application.Tests/Douyin/WebFallbackCycleGuardTests.cs`

**Interfaces:**

- `DouyinDiscoveryController` owns one cancellable scan, exposes `CurrentResult`, and raises `Changed` with immutable snapshots. `StartAsync` returns immediately after scheduling background detection; `RescanAsync` cancels and awaits the previous run before starting another.
- `RespawnCoordinator` receives `DouyinDiscoveryController`, `IDouyinWebFallbackLauncher`, and `WebFallbackCycleGuard` instead of assuming `settings.DouyinPath` is valid.

- [ ] **Step 1: Write failing controller lifecycle tests**

  Use a detector blocked by `TaskCompletionSource`. Assert `StartAsync` does not block the caller, progress updates become snapshots, `RescanAsync` cancels the first scan, and `DisposeAsync` leaves no active operation.

- [ ] **Step 2: Run lifecycle tests and verify RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Desktop.IntegrationTests -c Debug --filter FullyQualifiedName~DouyinDiscoveryControllerTests --no-restore
  ```

- [ ] **Step 3: Implement the background controller**

  Use `Task.Run` only around the detector operation, post immutable changes through the captured `SynchronizationContext`, and catch cancellation separately from failure. Do not hold a lock while raising `Changed`.

- [ ] **Step 4: Run controller tests and verify GREEN**

  ```powershell
  dotnet test tests\RespawnSwitch.Desktop.IntegrationTests -c Debug --filter FullyQualifiedName~DouyinDiscoveryControllerTests --no-restore
  ```

- [ ] **Step 5: Write failing coordinator decision tests around extracted pure handler**

  Extract `RespawnDouyinActionPlanner.Plan(RespawnCycleId, DouyinDiscoveryResult, DouyinRuntimePreferences)` and assert: desktop candidate produces desktop attach; scanning/no candidate plus fallback produces web once; found desktop never produces web; disabled fallback produces countdown-only. The WPF layer maps `AppSettings` to `DouyinRuntimePreferences`, so the cross-platform Application tests never reference the Windows App project.

- [ ] **Step 6: Run decision tests and verify RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Application.Tests -c Debug --filter FullyQualifiedName~RespawnDouyinActionPlannerTests --no-restore
  ```

- [ ] **Step 7: Integrate desktop/web actions in the coordinator**

  Desktop mode updates and saves `PreferredDouyinPath`, then retains current window/GSMTC workflow. Web mode calls the fixed launcher only when the cycle guard accepts the cycle. On `RespawnConfirmed`, always hide overlay and restore League; call media pause/window restore only for desktop mode, then complete the web guard cycle.

- [ ] **Step 8: Run Core, Application, Windows, and desktop regression**

  ```powershell
  dotnet test tests\RespawnSwitch.Core.Tests -c Debug --no-restore
  dotnet test tests\RespawnSwitch.Application.Tests -c Debug --no-restore
  dotnet test tests\RespawnSwitch.Windows.Tests -c Debug --no-restore
  dotnet test tests\RespawnSwitch.Desktop.IntegrationTests -c Debug --no-restore
  ```

- [ ] **Step 9: Commit Task 4**

  ```powershell
  git add src\RespawnSwitch.App src\RespawnSwitch.Application\Douyin tests\RespawnSwitch.Application.Tests\Douyin tests\RespawnSwitch.Desktop.IntegrationTests\Discovery
  git commit -m "feat(app): integrate background Douyin discovery"
  ```

---

### Task 5: Rebuild the WPF Interface and Settings Experience

**Files:**

- Modify: `src/RespawnSwitch.App/MainWindow.xaml`
- Modify: `src/RespawnSwitch.App/MainWindow.xaml.cs`
- Modify: `src/RespawnSwitch.App/App.xaml`
- Test: `tests/RespawnSwitch.Desktop.IntegrationTests/Ui/MainWindowSmokeTests.cs`

**Interfaces:**

- The window renders `DouyinDiscoveryResult` without performing discovery itself.
- User commands call `DouyinDiscoveryController.RescanAsync`, `CancelAsync`, and `SelectCandidateAsync`.

- [ ] **Step 1: Write failing WPF smoke tests**

  Instantiate the window on an STA thread and assert named elements exist: `MonitoringStatusPill`, `LeagueStatusCard`, `DouyinStatusCard`, `MediaStatusCard`, `AutoDetectToggle`, `WebFallbackToggle`, `DiscoveryProgressBar`, `CandidateList`, `RescanButton`, `CancelScanButton`, and `EventLog`.

- [ ] **Step 2: Run UI smoke tests and verify RED**

  ```powershell
  dotnet test tests\RespawnSwitch.Desktop.IntegrationTests -c Debug --filter FullyQualifiedName~MainWindowSmokeTests --no-restore
  ```

  Expected: named elements are absent from the current minimal layout.

- [ ] **Step 3: Add local WPF design tokens and card styles**

  In `App.xaml`, define exact resources: background `#0B0D12`, surface `#151922`, raised surface `#1C222E`, primary text `#F7F8FA`, secondary text `#98A2B3`, accent `#FE2C55`, success `#32D583`, warning `#FDB022`, danger `#F97066`, corner radius `14`, and standard spacing `16`/`24`.

- [ ] **Step 4: Replace the main layout**

  Build an 820×620 window with a header/status pill, three equal status cards, quick actions, settings card, candidate list, progress row, and bounded log. Use native WPF vector/text glyphs only. Preserve keyboard tab order and visible focus rectangles.

- [ ] **Step 5: Wire scan, cancel, browse, candidate selection, and settings save**

  Use `Microsoft.Win32.OpenFileDialog` filtered to `douyin.exe`. Manual selection is passed through the validator before persistence. Toggle changes save immediately and update the launch plan. Render ambiguous candidates as path + version + source; never show raw signature data except the shortened thumbprint suffix.

- [ ] **Step 6: Run UI smoke and desktop integration tests**

  ```powershell
  dotnet test tests\RespawnSwitch.Desktop.IntegrationTests -c Debug --no-restore
  ```

- [ ] **Step 7: Launch visual smoke build**

  ```powershell
  dotnet run --project src\RespawnSwitch.App -c Debug
  ```

  Verify the window remains responsive during a scan, all controls fit at 100% and 150% DPI, logs do not expand the window, and cancelling updates the status pill.

- [ ] **Step 8: Commit Task 5**

  ```powershell
  git add src\RespawnSwitch.App\App.xaml src\RespawnSwitch.App\MainWindow.xaml src\RespawnSwitch.App\MainWindow.xaml.cs tests\RespawnSwitch.Desktop.IntegrationTests\Ui
  git commit -m "feat(ui): add polished discovery and fallback settings"
  ```

---

### Task 6: Version, Verify, and Publish RespawnSwitch 0.2.0

**Files:**

- Modify: `src/RespawnSwitch.App/RespawnSwitch.App.csproj`
- Modify: `README.md`
- Modify: `build/publish.ps1`
- Modify after restore: affected `packages.lock.json`
- Produce: `outputs/RespawnSwitch-0.2.0-win-x64.zip`
- Produce: `outputs/RespawnSwitch-0.2.0-win-x64.sha256.txt`
- Produce: `outputs/RespawnSwitch-0.2-快速使用.txt`

**Interfaces:**

- Product/file/assembly version becomes `0.2.0` / `0.2.0.0`.
- Publish archive name becomes `RespawnSwitch-0.2.0-win-x64.zip`.

- [ ] **Step 1: Update version and user documentation**

  Document automatic all-fixed-disk scan, progress/cancel, manual candidate selection, and browser fallback limitation. Keep the real-game verification disclaimer.

- [ ] **Step 2: Restore from the existing offline source**

  ```powershell
  $env:DOTNET_CLI_HOME = Join-Path $PWD 'work\dotnet-home'
  $env:NUGET_PACKAGES = Join-Path $PWD 'work\nuget-packages'
  dotnet restore RespawnSwitch.sln --source "$PWD\work\offline-nuget" --ignore-failed-sources
  ```

- [ ] **Step 3: Run full Release build**

  ```powershell
  dotnet build RespawnSwitch.sln -c Release --no-restore
  ```

  Expected: exit code 0, zero warnings, zero errors.

- [ ] **Step 4: Run the complete Release test suite**

  ```powershell
  dotnet test RespawnSwitch.sln -c Release --no-build --no-restore
  ```

  Expected: every discovered test passes with zero failures.

- [ ] **Step 5: Run source self-test**

  ```powershell
  $exe = Resolve-Path 'src\RespawnSwitch.App\bin\Release\net8.0-windows10.0.26100.0\win-x64\RespawnSwitch.exe'
  $process = Start-Process -FilePath $exe -ArgumentList '--self-test' -Wait -PassThru -WindowStyle Hidden
  if ($process.ExitCode -ne 0) { throw "Self-test failed: $($process.ExitCode)" }
  ```

- [ ] **Step 6: Publish and package**

  ```powershell
  .\build\publish.ps1 -Configuration Release
  ```

  Update the script to emit the versioned archive rather than the old `Release` name.

- [ ] **Step 7: Verify the actual output ZIP**

  Copy the archive to `outputs`, calculate SHA-256, expand it into a new ignored smoke directory, and run the packaged `RespawnSwitch.exe --self-test`. Assert the output contains `RespawnSwitch.exe`, `SHA256SUMS`, and no `.pdb` files.

- [ ] **Step 8: Run forbidden-operation scan**

  ```powershell
  rg -n "DangerousAcceptAnyServerCertificateValidator|AttachThreadInput|SendInput|OpenProcess|QueryFullProcessImageName|TryTogglePlayPauseAsync" src
  ```

  Expected: no production match.

- [ ] **Step 9: Commit Task 6**

  ```powershell
  git add src\RespawnSwitch.App\RespawnSwitch.App.csproj README.md build\publish.ps1 **\packages.lock.json
  git commit -m "release: package RespawnSwitch 0.2.0"
  ```

## Plan Self-Review

- Spec coverage: full fixed-drive scanning, quick sources, candidate validation/ranking, progress/cancel, ambiguity, settings migration, browser fallback, one-open-per-cycle, UI redesign, error handling, testing, and publishing are each assigned to a task.
- Type consistency: `DouyinCandidate`, `DouyinDiscoveryResult`, `IDouyinInstallationDetector`, `DouyinLaunchPlan`, `IDouyinWebFallbackLauncher`, and `WebFallbackCycleGuard` are defined before consumers.
- Scope: the plan changes only Douyin discovery/fallback/settings/UI and required packaging; it does not introduce browser automation, global media keys, or unrelated refactors.
- Placeholder scan: the plan contains no unresolved implementation placeholders.
