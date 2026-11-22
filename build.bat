@echo off
title NetPulse Dual v2.1 - Auto Builder

echo ========================================================
echo   NetPulse Dual v2.1 (by Le_morries) - One-Click Builder
echo ========================================================

:: 1. Check / Fetch Ookla Speedtest CLI
if exist "speedtest.exe" (
    echo [1/4] Found speedtest.exe in current folder.
) else (
    echo [1/4] Searching or fetching Ookla Speedtest CLI...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "$f = Get-ChildItem -Path $env:USERPROFILE,$env:LOCALAPPDATA -Filter 'speedtest.exe' -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Length -gt 1000000 } | Select-Object -First 1; if ($f) { Copy-Item $f.FullName 'speedtest.exe' -Force }"
    if not exist "speedtest.exe" (
        winget install --id Ookla.Speedtest.CLI -e --accept-package-agreements --accept-source-agreements --silent >nul 2>&1
        powershell -NoProfile -ExecutionPolicy Bypass -Command "$f = Get-ChildItem -Path $env:LOCALAPPDATA -Filter 'speedtest.exe' -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Length -gt 1000000 } | Select-Object -First 1; if ($f) { Copy-Item $f.FullName 'speedtest.exe' -Force }"
    )
    if not exist "speedtest.exe" (
        echo [Notice] Please download Windows 64-bit CLI zip from https://speedtest.net and extract speedtest.exe here.
        pause & exit /b 1
    )
)

:: 2. Auto-fetch Open-Source LibreSpeed CLI
if exist "librespeed-cli.exe" (
    echo [2/4] Found librespeed-cli.exe in current folder.
) else (
    echo [2/4] Searching or fetching LibreSpeed CLI...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "$f = Get-ChildItem -Path $env:USERPROFILE,$env:LOCALAPPDATA -Filter 'librespeed-cli.exe' -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Length -gt 1000000 } | Select-Object -First 1; if ($f) { Copy-Item $f.FullName 'librespeed-cli.exe' -Force }"
    if not exist "librespeed-cli.exe" (
        winget install --id LibreSpeed.SpeedtestCLI -e --accept-package-agreements --accept-source-agreements --silent >nul 2>&1
        powershell -NoProfile -ExecutionPolicy Bypass -Command "$f = Get-ChildItem -Path $env:LOCALAPPDATA -Filter 'librespeed-cli.exe' -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Length -gt 1000000 } | Select-Object -First 1; if ($f) { Copy-Item $f.FullName 'librespeed-cli.exe' -Force }"
    )
    if not exist "librespeed-cli.exe" (
        echo [Notice] Please download Windows 64-bit zip from: https://github.com
        echo Extract 'librespeed-cli.exe' into this folder, then run build.bat again!
        pause & exit /b 1
    )
)

:: 3. Locate .NET Framework 4.x Compiler
set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [Error] .NET Framework 4.x compiler not found!
    pause & exit /b 1
)


:: 4. Compile & Embed Both Engines into NetPulseDual_v2.1.exe
if exist "NetPulseDual.exe" del /f /q "NetPulseDual.exe"
echo [3/4] Compiling NetPulse Dual v2.0 and embedding dual engines...
"%CSC%" /nologo /target:winexe /win32icon:app.ico /optimize+ /r:System.dll,System.Core.dll,System.Windows.Forms.dll,System.Drawing.dll,System.Web.Extensions.dll,System.Net.Http.dll /resource:"speedtest.exe",speedtest.exe /resource:"librespeed-cli.exe",librespeed-cli.exe /out:"NetPulseDual_v2.1.exe" "Program.cs"

if errorlevel 1 (
    echo [Failed] Compilation failed. Check error messages above.
    pause & exit /b 1
)

echo [4/4] Success! Generated standalone executable: NetPulseDual_v2.1.exe
echo ========================================================
start "" "NetPulseDual_v2.1.exe"
pause
