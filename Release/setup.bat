@echo off
chcp 65001 >nul
title Crosshair Overlay v1.1.0 Setup
echo.
echo  ============================================
echo    Crosshair Overlay v1.1.0 Setup
echo  ============================================
echo.
echo [1/3] Installing certificate...
certutil -addstore Root "%~dp0CrosshairOverlayWidget.cer" >nul 2>&1
if %errorlevel% neq 0 (
    echo   [ERROR] Certificate install failed. Right-click - Run as administrator.
    pause
    exit /b 1
)
echo   [OK] Certificate installed.
echo.
echo [2/3] Installing Game Bar Widget...
powershell -NoProfile -Command "Add-AppxPackage -Path '%~dp0CrosshairOverlayWidget.msix'" >nul 2>&1
if %errorlevel% neq 0 (
    echo   [INFO] Retrying with admin privileges...
    powershell -NoProfile -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile -Command Add-AppxPackage -Path \"%~dp0CrosshairOverlayWidget.msix\"'"
) else (
    echo   [OK] Widget installed.
)
echo.
echo [3/3] Launching desktop app...
start "" "%~dp0CrosshairOverlay\CrosshairOverlay.exe"
echo   [OK] Desktop app started.
echo.
echo  ============================================
echo    Done!
echo  ============================================
echo.
echo  Usage:
echo    Desktop: Alt+X toggle crosshair, Alt+` open settings
echo    Widget:  Win+G - Crosshair - Pin - Enable click-through
echo.
pause
