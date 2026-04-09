param(
    [string]$Runtime = 'win-x64',
    [string]$Configuration = 'Release',
    [string]$AppVersion = '2.0.4'
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishDir = Join-Path $projectRoot 'release\MouseTool'
$updaterPublishDir = Join-Path $projectRoot 'release\MouseToolUpdater'
$zipPath = Join-Path $projectRoot ("release\MouseTool-{0}.zip" -f $Runtime)
$installerPath = Join-Path $projectRoot 'release\MouseTool-Setup.exe'
$changelogPath = Join-Path $projectRoot 'release\MouseTool-CHANGELOG.md'
$legacyInstallerPath = Join-Path $projectRoot 'release\MouseTool-Installer.exe'
$updateManifestPath = Join-Path $projectRoot 'release\update.json'
$innoScriptPath = Join-Path $projectRoot 'Installer\MouseTool.iss'
$innoCompiler = (Get-Command iscc.exe -ErrorAction SilentlyContinue).Source

if (-not $innoCompiler) {
    $candidate = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
    if (Test-Path $candidate) {
        $innoCompiler = $candidate
    }
}

if (-not $innoCompiler) {
    $candidate = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
    if (Test-Path $candidate) {
        $innoCompiler = $candidate
    }
}

if (-not $innoCompiler) {
    throw 'Inno Setup compiler (ISCC.exe) was not found.'
}

foreach ($path in @($publishDir, $updaterPublishDir, $zipPath, $installerPath, $changelogPath, $legacyInstallerPath, $updateManifestPath)) {
    if (Test-Path $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

& 'C:\Program Files\dotnet\dotnet.exe' publish `
    (Join-Path $projectRoot 'MouseTool.csproj') `
    -c $Configuration `
    -r $Runtime `
    --self-contained false `
    -p:PublishSingleFile=false `
    -p:DebugSymbols=false `
    -p:DebugType=None `
    -o $publishDir

& 'C:\Program Files\dotnet\dotnet.exe' publish `
    (Join-Path $projectRoot 'Updater\MouseTool.Updater.csproj') `
    -c $Configuration `
    -r $Runtime `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:DebugSymbols=false `
    -p:DebugType=None `
    -o $updaterPublishDir

Copy-Item `
    -LiteralPath (Join-Path $updaterPublishDir 'MouseTool.Updater.exe') `
    -Destination (Join-Path $publishDir 'MouseTool.Updater.exe') `
    -Force

Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -CompressionLevel Optimal

& $innoCompiler `
    "/DAppVersion=$AppVersion" `
    "/DPublishDir=$publishDir" `
    "/DOutputDir=$(Join-Path $projectRoot 'release')" `
    $innoScriptPath | Out-Null

$tagCandidates = @(git tag --sort=-creatordate 2>$null)
$previousTag = $tagCandidates |
    Where-Object { $_ -and $_ -ne "v$AppVersion" -and $_ -ne $AppVersion } |
    Select-Object -First 1

if ($previousTag) {
    $changeEntries = @(git log "$previousTag..HEAD" --pretty=format:'- %s (%h)' 2>$null)
    $comparisonLine = "Changes since $previousTag"
}
else {
    $changeEntries = @(git log -n 20 --pretty=format:'- %s (%h)' 2>$null)
    $comparisonLine = 'Recent changes included in this release'
}

if (-not $changeEntries -or $changeEntries.Count -eq 0) {
    $changeEntries = @('- Release packaged with the current repository state.')
}

$changelogContent = @(
    "# MouseTool $AppVersion"
    ''
    "$comparisonLine."
    ''
    '## Changelog'
    ''
) + $changeEntries + @(
    ''
    '## Downloads'
    ''
    '- Installer: `MouseTool-Setup.exe`'
    '- Portable package: `MouseTool-win-x64.zip`'
)

$changelogContent | Set-Content -Path $changelogPath -Encoding UTF8

$installerHash = (Get-FileHash -Path $installerPath -Algorithm SHA256).Hash.ToUpperInvariant()
$updateManifest = [ordered]@{
    version = $AppVersion
    installerUrl = 'https://github.com/mvidaldev/MouseTool/releases/latest/download/MouseTool-Setup.exe'
    sha256 = $installerHash
    publishedAt = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    releaseNotesUrl = 'https://github.com/mvidaldev/MouseTool/releases/latest'
    changelogUrl = 'https://github.com/mvidaldev/MouseTool/releases/latest/download/MouseTool-CHANGELOG.md'
}

$updateManifest | ConvertTo-Json | Set-Content -Path $updateManifestPath -Encoding ASCII

Write-Host "Release published to: $publishDir"
Write-Host "Release zip created at: $zipPath"
Write-Host "Installer created at: $installerPath"
Write-Host "Changelog created at: $changelogPath"
Write-Host "Update manifest created at: $updateManifestPath"
