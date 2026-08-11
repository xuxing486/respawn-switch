# Task 2 Report — Timer semantics and one-shot attachment policy

## Baseline and HEAD

- Baseline commit: `73ec5fd19397b118a1508db4497982d137777943` (`build: scaffold .NET 8 RespawnSwitch solution`).
- Implementation commit before this evidence-report amendment:
  `695f525` (`feat(core): add timer semantics and attachment policy`). This
  report is force-added because the SDD directory is intentionally ignored;
  amending the commit to include it will produce the final HEAD identifier.

## RED evidence

Tests were added before any Task 2 production source. The following command was
run with the required local .NET/NuGet locations and `--no-restore`:

```powershell
$env:DOTNET_CLI_HOME = Join-Path $PWD 'work\dotnet-home'
$env:NUGET_PACKAGES = Join-Path $PWD 'work\nuget-packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet test tests\RespawnSwitch.Core.Tests -c Debug --filter "FullyQualifiedName~RespawnTimerNormalizerTests|FullyQualifiedName~AttachmentPolicyTests" --no-restore
```

Result: exit code 1 as expected. Compilation reported `CS0234` for the missing
`RespawnSwitch.Core.Clock` and `RespawnSwitch.Core.Respawn` namespaces, followed
by `CS0246`/`CS0103` for the absent semantic and attachment decision types. This
demonstrates that the newly written behavior tests failed because Task 2's
contracts did not yet exist.

## GREEN evidence

After the minimal implementation was added, the focused command above completed
with exit code 0: 20 passed, 0 failed, 0 skipped.

Core regression was then run using the same environment:

```powershell
dotnet test tests\RespawnSwitch.Core.Tests -c Debug --no-restore
```

Result: exit code 0; 21 passed, 0 failed, 0 skipped.

## Implemented contracts

- `RespawnTimerSemantics` and `TimerSemanticStatus` record whether raw timer
  semantics have been verified for the current patch and preserve the evidence
  metadata.
- `RespawnTimerNormalizer.TryNormalize` accepts only verified semantics, finite
  non-negative raw values and multipliers, and a finite multiplication result.
- `AttachmentPolicy.Evaluate` always returns `AlreadyIssued` first, waits for a
  null/invalid timer, shows countdown before the exact threshold, and returns
  `AttachOnce` at the threshold or later.

## Changed files

- `src/RespawnSwitch.Core/Clock/RespawnTimerSemantics.cs`
- `src/RespawnSwitch.Core/Clock/RespawnTimerNormalizer.cs`
- `src/RespawnSwitch.Core/Respawn/AttachmentPolicy.cs`
- `tests/RespawnSwitch.Core.Tests/Clock/RespawnTimerNormalizerTests.cs`
- `tests/RespawnSwitch.Core.Tests/Respawn/AttachmentPolicyTests.cs`
- `.superpowers/sdd/2026-08-10-respawnswitch-core-and-risk-probes/task-2-report.md`

## Final checks and concerns

- `git diff --check` completed with exit code 0 before staging.
- No restore was needed; all test runs used the workspace's local package cache
  and `--no-restore`.
- `AttachmentPolicy` accepts a caller-provided threshold exactly as supplied.
  The Task 2 contract defines validation for the timer value, not a separate
  validity policy for the threshold; downstream callers must provide the
  intended non-negative finite threshold.
