[CmdletBinding()]
param([switch]$Restore, [string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\RespawnSwitch.App\RespawnSwitch.App.csproj'
$version = '0.2.0'
$artifacts = Join-Path $root 'artifacts'
$publish = Join-Path $artifacts "publish\$version-win-x64"
$archive = Join-Path $root "artifacts\RespawnSwitch-$version-win-x64.zip"
if ($Restore) {
    dotnet restore $project
    if ($LASTEXITCODE -ne 0) { throw "Restore failed with exit code $LASTEXITCODE" }
}
if (Test-Path -LiteralPath $publish) {
    $resolvedPublish = [System.IO.Path]::GetFullPath($publish)
    $resolvedArtifacts = [System.IO.Path]::GetFullPath($artifacts) + [System.IO.Path]::DirectorySeparatorChar
    if (!$resolvedPublish.StartsWith($resolvedArtifacts, [System.StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe publish path: $resolvedPublish" }
    Remove-Item -LiteralPath $resolvedPublish -Recurse -Force
}
dotnet publish $project -c $Configuration -r win-x64 --self-contained true --no-restore -p:PublishSingleFile=false -p:PublishTrimmed=false -o $publish
if ($LASTEXITCODE -ne 0) { throw "Publish failed with exit code $LASTEXITCODE" }
$exe = Join-Path $publish 'RespawnSwitch.exe'
if (!(Test-Path $exe)) { throw "Publish output missing: $exe" }
Get-ChildItem -LiteralPath $publish -Filter '*.pdb' -File | Remove-Item -Force
$selfTest = Start-Process -FilePath $exe -ArgumentList '--self-test' -Wait -PassThru -WindowStyle Hidden
if ($selfTest.ExitCode -ne 0) { throw "Self-test failed with exit code $($selfTest.ExitCode)" }
$hashes = Get-ChildItem -File $publish | Sort-Object FullName | ForEach-Object { "$( (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant() )  $($_.Name)" }
$hashes | Set-Content -Encoding ascii (Join-Path $publish 'SHA256SUMS')
if (Test-Path $archive) { Remove-Item -LiteralPath $archive -Force }
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $archive -CompressionLevel Optimal
$zipHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $archive).Hash.ToLowerInvariant()
Write-Host "Published: $publish`nArchive SHA256: $zipHash"
