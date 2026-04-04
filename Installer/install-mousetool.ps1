Add-Type -AssemblyName System.Windows.Forms

$ErrorActionPreference = 'Stop'

$sourceRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$archivePath = Join-Path $sourceRoot 'MouseTool.zip'
$installRoot = Join-Path $env:LOCALAPPDATA 'Programs\MouseTool'
$tempExtractRoot = Join-Path $env:TEMP ('MouseTool-Install-' + [Guid]::NewGuid().ToString('N'))
$desktopShortcutPath = Join-Path ([Environment]::GetFolderPath('Desktop')) 'MouseTool.lnk'
$programsShortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'MouseTool.lnk'
$uninstallShortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'Uninstall MouseTool.lnk'
$uninstallRegistryPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\MouseTool'

try {
    Get-Process MouseTool -ErrorAction SilentlyContinue | Stop-Process -Force

    if (Test-Path $tempExtractRoot) {
        Remove-Item -LiteralPath $tempExtractRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Path $tempExtractRoot | Out-Null
    Expand-Archive -LiteralPath $archivePath -DestinationPath $tempExtractRoot -Force

    if (Test-Path $installRoot) {
        Remove-Item -LiteralPath $installRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
    Copy-Item -Path (Join-Path $tempExtractRoot '*') -Destination $installRoot -Recurse -Force

    $shell = New-Object -ComObject WScript.Shell

    foreach ($shortcutPath in @($desktopShortcutPath, $programsShortcutPath)) {
        $shortcut = $shell.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = Join-Path $installRoot 'MouseTool.exe'
        $shortcut.WorkingDirectory = $installRoot
        $shortcut.IconLocation = Join-Path $installRoot 'MouseTool.exe'
        $shortcut.Save()
    }

    $uninstallShortcut = $shell.CreateShortcut($uninstallShortcutPath)
    $uninstallShortcut.TargetPath = Join-Path $installRoot 'uninstall.cmd'
    $uninstallShortcut.WorkingDirectory = $installRoot
    $uninstallShortcut.IconLocation = Join-Path $installRoot 'MouseTool.exe'
    $uninstallShortcut.Save()

    New-Item -Path $uninstallRegistryPath -Force | Out-Null
    Set-ItemProperty -Path $uninstallRegistryPath -Name 'DisplayName' -Value 'MouseTool'
    Set-ItemProperty -Path $uninstallRegistryPath -Name 'Publisher' -Value 'Marcos Vidal'
    Set-ItemProperty -Path $uninstallRegistryPath -Name 'InstallLocation' -Value $installRoot
    Set-ItemProperty -Path $uninstallRegistryPath -Name 'DisplayIcon' -Value (Join-Path $installRoot 'MouseTool.exe')
    Set-ItemProperty -Path $uninstallRegistryPath -Name 'UninstallString' -Value ('"' + (Join-Path $installRoot 'uninstall.cmd') + '"')
    Set-ItemProperty -Path $uninstallRegistryPath -Name 'QuietUninstallString' -Value ('"' + (Join-Path $installRoot 'uninstall.cmd') + '"')
    Set-ItemProperty -Path $uninstallRegistryPath -Name 'NoModify' -Value 1 -Type DWord
    Set-ItemProperty -Path $uninstallRegistryPath -Name 'NoRepair' -Value 1 -Type DWord

    Start-Process -FilePath (Join-Path $installRoot 'MouseTool.exe')

    [System.Windows.Forms.MessageBox]::Show(
        "MouseTool foi instalado com sucesso em:`r`n$installRoot",
        "MouseTool Installer",
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Information
    ) | Out-Null
}
catch {
    [System.Windows.Forms.MessageBox]::Show(
        "Nao foi possivel instalar o MouseTool.`r`n`r`n$($_.Exception.Message)",
        "MouseTool Installer",
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Error
    ) | Out-Null
    exit 1
}
finally {
    if (Test-Path $tempExtractRoot) {
        Remove-Item -LiteralPath $tempExtractRoot -Recurse -Force
    }
}
