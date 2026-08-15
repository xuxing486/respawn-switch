[CmdletBinding()]
param([switch]$Restore, [string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\RespawnSwitch.App\RespawnSwitch.App.csproj'
$publish = Join-Path $root 'artifacts\publish\win-x64'
$archive = Join-Path $root "artifacts\RespawnSwitch-$Configuration-win-x64.zip"
if ($Restore) { dotnet restore $project }
dotnet publish $project -c $Configuration -r win-x64 --self-contained true --no-restore -p:PublishSingleFile=false -p:PublishTrimmed=false -o $publish
$exe = Join-Path $publish 'RespawnSwitch.exe'
if (!(Test-Path $exe)) { throw "Publish output missing: $exe" }
& $exe --self-test
if ($LASTEXITCODE -ne 0) { throw "Self-test failed with exit code $LASTEXITCODE" }
$hashes = Get-ChildItem -File $publish | Sort-Object FullName | ForEach-Object { "$( (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant() )  $($_.Name)" }
$hashes | Set-Content -Encoding ascii (Join-Path $publish 'SHA256SUMS')
if (Test-Path $archive) { Remove-Item -LiteralPath $archive -Force }
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $archive -CompressionLevel Optimal
$zipHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $archive).Hash.ToLowerInvariant()
Write-Host "Published: $publish`nArchive SHA256: $zipHash"
