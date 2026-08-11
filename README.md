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
