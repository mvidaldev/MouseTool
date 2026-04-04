@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-mousetool.ps1"
exit /b %errorlevel%
