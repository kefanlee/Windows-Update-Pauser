@echo off
chcp 65001 >nul
title Windows Update Pauser - Build
setlocal enabledelayedexpansion
cd /d "%~dp0"

echo.
echo ============================================
echo   Windows Update Pauser C# build
echo ============================================
echo.

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [ERROR] csc.exe not found. Please enable/install .NET Framework 4.x.
    pause
    exit /b 1
)

if not exist "WinFreeze.cs" (
    echo [ERROR] WinFreeze.cs not found.
    pause
    exit /b 1
)

if not exist "icon.ico" (
    echo [WARN] icon.ico not found. The exe will use default icon.
    set "ICON_ARG="
) else (
    set "ICON_ARG=/win32icon:icon.ico"
)

if not exist "dist" mkdir "dist"

echo [1/2] Compiling Windows Update Pauser.exe...
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /utf8output /win32manifest:app.manifest !ICON_ARG! /out:"dist\Windows Update Pauser.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Security.dll WinFreeze.cs
if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Build failed.
    pause
    exit /b 1
)

echo.
echo [2/2] Build completed.
for %%F in ("dist\Windows Update Pauser.exe") do (
    echo Output: %%~fF
    echo Size: %%~zF bytes
)
certutil -hashfile "dist\Windows Update Pauser.exe" SHA256
echo.
pause
