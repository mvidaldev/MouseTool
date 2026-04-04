# MouseTool

`MouseTool` is a Windows tray utility built with `.NET 10`, `WPF`, and a small amount of native Win32 interop.

It is designed for dual-screen setups where:

- the physical mouse should stay anchored to the main monitor
- a touchscreen is used on a secondary monitor
- the app should remember the last useful mouse position on the primary screen
- the cursor should return to that saved primary-screen position after touchscreen interaction ends

The project was inspired by the workflow described by Touch Mouse Tools and adapted into a standalone desktop utility focused on a practical day-to-day Windows workflow.

## What the app does

MouseTool separates mouse and touchscreen behavior so the touchscreen can be used as a control surface without permanently pulling the mouse away from the main work display.

Core behavior:

- remembers the last valid mouse position on the primary monitor
- treats touchscreen activity on the configured touchscreen monitor as secondary interaction
- restores the cursor to the saved primary-monitor position when physical mouse activity resumes
- can optionally restore immediately when touch ends
- can optionally allow the physical mouse to enter the touchscreen monitor area
- keeps the app available through a tray icon and desktop control panel

## Why this is not a Windows Service

Windows Services run in Session 0 and do not have normal access to the interactive desktop session used by the logged-in user.

Because MouseTool needs to:

- observe global input
- react to touchscreen and mouse movement
- move the cursor inside the active desktop session

it must run as a per-user background application instead of a true Windows Service.

## Current product status

MouseTool currently includes:

- tray-based background execution
- a WPF control panel with fixed-size desktop layout
- manual monitor selection with readable names
- configurable restore behavior
- optional startup with Windows
- optional manual diagnostics logging
- multilingual UI driven by JSON translation files
- contextual help file opening based on the selected language
- release packaging
- setup installer
- uninstaller

## Architecture

The project is now fully centered on WPF for the desktop interface.

Main layers:

- `App.xaml` and `App.xaml.cs` for WPF application startup
- `MainWindow.xaml` and `MainWindow.xaml.cs` for the main dashboard
- `CloseChoiceWindow.xaml` and `CloseChoiceWindow.xaml.cs` for the close/minimize decision dialog
- `Program.cs` for application coordination, configuration, localization, help, startup registration, and the input engine
- `TrayIconHost.cs` for the native tray icon and tray menu
- `MonitorManager.cs` for native monitor enumeration without Windows Forms
- `NativeMethods.cs` for input hooks, cursor movement, tray interop, and monitor APIs

## Main UI

The application window includes:

- top header with branding, app status, and language selector
- tabbed interface:
  - `Overview`
  - `Displays`
  - `Behavior`
  - `Diagnostics`
- footer command bar with run, pause, tray, config, and apply actions

### Overview tab

The Overview tab is split into two columns:

- left column:
  - active status summary
  - explanation of how MouseTool works
  - quick usage guide
  - best-practices section
  - scroll support when content exceeds the available height
- right column:
  - fixed support area
  - gratitude text
  - clickable support email
  - help button
  - buy-me-a-coffee button

### Displays tab

Lets the user choose:

- the primary display
- the touchscreen display

Monitor names are shown in a readable format based on role, order, resolution, and coordinates.

### Behavior tab

Current options:

- restore the mouse immediately when touch ends
- allow the physical mouse to enter the touchscreen display area
- start MouseTool with Windows

### Diagnostics tab

Current controls:

- enable logging
- disable logging
- open log file

Logging is disabled by default.

## Tray behavior

The app stays available through the system tray and supports:

- opening the dashboard from the tray
- starting protection
- pausing protection
- opening help
- exiting the app
- opening the dashboard with left click or double click on the tray icon
- opening the tray menu with right click

## Language system

The UI text is driven by JSON files under:

- [Resources\Lang](Z:\projetos\Codex\MouseTool\Resources\Lang)

Currently supported languages:

- English
- Portuguese (Brazil)
- French
- Spanish
- German
- Italian
- Russian

Behavior:

- default language follows the Windows system language when available
- users can manually choose a language from the header selector
- after changing the language, the app asks the user to restart
- the UI loads from the selected JSON language file on the next launch

Current language files:

- [lang.en.json](Z:\projetos\Codex\MouseTool\Resources\Lang\lang.en.json)
- [lang.pt-BR.json](Z:\projetos\Codex\MouseTool\Resources\Lang\lang.pt-BR.json)
- [lang.fr.json](Z:\projetos\Codex\MouseTool\Resources\Lang\lang.fr.json)
- [lang.es.json](Z:\projetos\Codex\MouseTool\Resources\Lang\lang.es.json)
- [lang.de.json](Z:\projetos\Codex\MouseTool\Resources\Lang\lang.de.json)
- [lang.it.json](Z:\projetos\Codex\MouseTool\Resources\Lang\lang.it.json)
- [lang.ru.json](Z:\projetos\Codex\MouseTool\Resources\Lang\lang.ru.json)

## Help system

MouseTool generates localized help files in the output folder under `help`.

Supported help files currently include:

- `HELP.en.html`
- `HELP.pt-BR.html`
- `HELP.fr.html`
- `HELP.es.html`
- `HELP.de.html`
- `HELP.it.html`
- `HELP.ru.html`

The app opens the help file that matches the currently selected language when possible, with fallback to English when necessary.

## Configuration

The app writes `mousekeeper.config.json` next to the executable on first run.

Important fields include:

- `Enabled`
- `LoggingEnabled`
- `StartWithWindows`
- `SelectedLanguage`
- `AllowMouseOnTouchscreen`
- `PrimaryMonitorDeviceName`
- `TouchMonitorDeviceName`
- `RestoreImmediatelyOnTouchRelease`
- `LastPrimaryMousePosition`

## Startup behavior

MouseTool can register itself in:

- `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

This is controlled by the `Start with Windows` option in the app.

## Logging

Logging is manual and disabled by default.

The diagnostic log file is written next to the executable as:

- `mousekeeper.log`

Use logging only when you need to investigate behavior during testing.

## Build

```powershell
$env:DOTNET_CLI_HOME = "$PWD\\.dotnet-home"
$env:NUGET_PACKAGES = "$PWD\\.nuget\\packages"
$env:NUGET_CONFIG_FILE = "$PWD\\NuGet.Config"
& "C:\Program Files\dotnet\dotnet.exe" build
```

## Run locally

```powershell
& "C:\Program Files\dotnet\dotnet.exe" run
```

## Publish release

Use:

```powershell
powershell -ExecutionPolicy Bypass -File Z:\projetos\Codex\MouseTool\publish-release.ps1
```

This generates:

- release folder
- zip package
- setup installer executable

Current release outputs:

- [MouseTool.exe](Z:\projetos\Codex\MouseTool\release\MouseTool\MouseTool.exe)
- [MouseTool-win-x64.zip](Z:\projetos\Codex\MouseTool\release\MouseTool-win-x64.zip)
- [MouseTool-Setup.exe](Z:\projetos\Codex\MouseTool\release\MouseTool-Setup.exe)

## Installer

The installer is generated with `Inno Setup`.

It currently:

- installs MouseTool into `%LocalAppData%\Programs\MouseTool`
- creates application shortcuts
- launches the app after installation
- registers uninstall information in Windows
- includes uninstall support through Windows settings and the Start Menu

Installer source files:

- [publish-release.ps1](Z:\projetos\Codex\MouseTool\publish-release.ps1)
- [MouseTool.iss](Z:\projetos\Codex\MouseTool\Installer\MouseTool.iss)
- [install.cmd](Z:\projetos\Codex\MouseTool\Installer\install.cmd)
- [install-mousetool.ps1](Z:\projetos\Codex\MouseTool\Installer\install-mousetool.ps1)

## Uninstaller

The project includes an uninstaller that:

- stops the running app
- removes shortcuts
- removes startup registration
- removes uninstall registry entries
- deletes the installed application folder

Uninstaller source files:

- [uninstall.cmd](Z:\projetos\Codex\MouseTool\Installer\uninstall.cmd)
- [uninstall-mousetool.ps1](Z:\projetos\Codex\MouseTool\Installer\uninstall-mousetool.ps1)

## Project structure

Main files:

- [App.xaml](Z:\projetos\Codex\MouseTool\App.xaml)
- [App.xaml.cs](Z:\projetos\Codex\MouseTool\App.xaml.cs)
- [MainWindow.xaml](Z:\projetos\Codex\MouseTool\MainWindow.xaml)
- [MainWindow.xaml.cs](Z:\projetos\Codex\MouseTool\MainWindow.xaml.cs)
- [CloseChoiceWindow.xaml](Z:\projetos\Codex\MouseTool\CloseChoiceWindow.xaml)
- [CloseChoiceWindow.xaml.cs](Z:\projetos\Codex\MouseTool\CloseChoiceWindow.xaml.cs)
- [Program.cs](Z:\projetos\Codex\MouseTool\Program.cs)
- [TrayIconHost.cs](Z:\projetos\Codex\MouseTool\TrayIconHost.cs)
- [MonitorManager.cs](Z:\projetos\Codex\MouseTool\MonitorManager.cs)
- [NativeMethods.cs](Z:\projetos\Codex\MouseTool\NativeMethods.cs)
- [MouseTool.csproj](Z:\projetos\Codex\MouseTool\MouseTool.csproj)

Resources:

- [Resources\Lang](Z:\projetos\Codex\MouseTool\Resources\Lang)
- [Installer](Z:\projetos\Codex\MouseTool\Installer)

## Technical notes

- target framework: `net10.0-windows`
- UI framework: `WPF`
- tray implementation: native shell icon via Win32 interop
- execution model: tray app running in the logged-in desktop session
- monitor detection uses native monitor enumeration with manual selection support
- touchscreen behavior may vary depending on hardware driver behavior
- fallback logic exists for systems where touch input is exposed differently

## References

Behavior and workflow inspiration:

- [Touch Mouse Tools FAQ](https://touchmousetools.com/faqs/)
- [Touch Mouse Tools User Manual](https://touchmousetools.com/user-manual/)

Installer packaging references:

- [Inno Setup](https://jrsoftware.org/isinfo.php)
