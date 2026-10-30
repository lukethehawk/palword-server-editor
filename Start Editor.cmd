@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0PalworldServerEditor.ps1"
if errorlevel 1 pause
