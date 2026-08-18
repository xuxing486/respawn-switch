# Author: Stress Monster
[CmdletBinding()]
param([switch]$Restore, [string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\RespawnSwitch.App\RespawnSwitch.App.csproj'
$version = '0.3.5'
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
$extensionSource = Join-Path $root 'browser-extension'
$extensionTarget = Join-Path $publish 'browser-extension'
Copy-Item -LiteralPath $extensionSource -Destination $extensionTarget -Recurse
$userGuides = @(Get-ChildItem -LiteralPath (Join-Path $root 'docs') -File -Filter "RespawnSwitch-$version-*.txt")
$developerGuides = @(Get-ChildItem -LiteralPath (Join-Path $root 'docs') -File -Filter "RespawnSwitch-$version-*.md")
if ($userGuides.Count -ne 1 -or $developerGuides.Count -ne 1) { throw 'Expected exactly one user guide and one developer guide.' }
$userGuide = $userGuides[0]
$developerGuide = $developerGuides[0]
$authorNotice = Join-Path $root 'AUTHOR.txt'
Copy-Item -LiteralPath $userGuide.FullName -Destination (Join-Path $publish $userGuide.Name)
Copy-Item -LiteralPath $developerGuide.FullName -Destination (Join-Path $publish $developerGuide.Name)
Copy-Item -LiteralPath $authorNotice -Destination (Join-Path $publish 'AUTHOR.txt')
$selfTest = Start-Process -FilePath $exe -ArgumentList '--self-test' -Wait -PassThru -WindowStyle Hidden
if ($selfTest.ExitCode -ne 0) { throw "Self-test failed with exit code $($selfTest.ExitCode)" }
$hashes = Get-ChildItem -File $publish -Recurse | Sort-Object FullName | ForEach-Object {
    $relative = $_.FullName.Substring($publish.Length).TrimStart([char]92).Replace('\', '/')
    "$( (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant() )  $relative"
}
$hashes | Set-Content -Encoding ascii (Join-Path $publish 'SHA256SUMS')
if (Test-Path $archive) { Remove-Item -LiteralPath $archive -Force }
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $archive -CompressionLevel Optimal
$zipHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $archive).Hash.ToLowerInvariant()
Write-Host "Published: $publish`nArchive SHA256: $zipHash"
