Add-Type -AssemblyName System.Windows.Forms

$ErrorActionPreference = 'Stop'

$installRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$desktopShortcutPath = Join-Path ([Environment]::GetFolderPath('Desktop')) 'MouseTool.lnk'
$programsFolder = [Environment]::GetFolderPath('Programs')
$programsShortcutPath = Join-Path $programsFolder 'MouseTool.lnk'
$uninstallShortcutPath = Join-Path $programsFolder 'Uninstall MouseTool.lnk'
$registryPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\MouseTool'
$runKeyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$cleanupScriptPath = Join-Path $env:TEMP ('MouseTool-Uninstall-' + [Guid]::NewGuid().ToString('N') + '.cmd')

try {
    Get-Process MouseTool -ErrorAction SilentlyContinue | Stop-Process -Force
    Remove-Item -LiteralPath $desktopShortcutPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $programsShortcutPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $uninstallShortcutPath -Force -ErrorAction SilentlyContinue
    Remove-Item -Path $registryPath -Recurse -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $runKeyPath -Name 'MouseTool' -ErrorAction SilentlyContinue

    $cleanupScript = @"
@echo off
timeout /t 2 /nobreak >nul
rmdir /s /q "$installRoot"
del "%~f0"
"@

    Set-Content -Path $cleanupScriptPath -Value $cleanupScript -Encoding ASCII
    Start-Process -FilePath $cleanupScriptPath -WindowStyle Hidden

    [System.Windows.Forms.MessageBox]::Show(
        "MouseTool foi removido do computador.",
        "MouseTool Uninstaller",
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Information
    ) | Out-Null
}
catch {
    [System.Windows.Forms.MessageBox]::Show(
        "Nao foi possivel remover o MouseTool.`r`n`r`n$($_.Exception.Message)",
        "MouseTool Uninstaller",
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Error
    ) | Out-Null
    exit 1
}
