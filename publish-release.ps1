param(
    [string]$Runtime = 'win-x64',
    [string]$Configuration = 'Release',
    [string]$AppVersion = '2.0.4'
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishDir = Join-Path $projectRoot 'release\MouseTool'
$zipPath = Join-Path $projectRoot ("release\MouseTool-{0}.zip" -f $Runtime)
$installerPath = Join-Path $projectRoot 'release\MouseTool-Setup.exe'
$legacyInstallerPath = Join-Path $projectRoot 'release\MouseTool-Installer.exe'
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

foreach ($path in @($publishDir, $zipPath, $installerPath, $legacyInstallerPath)) {
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

Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -CompressionLevel Optimal

& $innoCompiler `
    "/DAppVersion=$AppVersion" `
    "/DPublishDir=$publishDir" `
    "/DOutputDir=$(Join-Path $projectRoot 'release')" `
    $innoScriptPath | Out-Null

Write-Host "Release published to: $publishDir"
Write-Host "Release zip created at: $zipPath"
Write-Host "Installer created at: $installerPath"
