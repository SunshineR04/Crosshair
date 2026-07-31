@echo off
setlocal
chcp 65001 >nul
title Crosshair Overlay Setup

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup.ps1"
set "EXITCODE=%ERRORLEVEL%"

if not "%EXITCODE%"=="0" (
    echo.
    echo Installation failed with exit code %EXITCODE%.
    pause
)

exit /b %EXITCODE%
