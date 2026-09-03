@echo off
setlocal
set "PROJECT_ROOT=%~dp0"
pwsh -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_ROOT%scripts\start-monitor.ps1"
if errorlevel 1 (
    echo.
    echo Failed to start Ori Precision Grapple Monitor.
    pause
)
