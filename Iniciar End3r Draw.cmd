@echo off
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Start-End3rDraw.ps1"
if errorlevel 1 pause
