@echo off
chcp 65001 >nul
title Claude Usage Overlay — Build
color 0A
echo.
echo  ╔══════════════════════════════════════╗
echo  ║   Claude Usage Overlay — Builder    ║
echo  ╚══════════════════════════════════════╝
echo.

REM ── Verify .NET 8 SDK ──────────────────────────────────────────────────────
where dotnet >nul 2>&1
if errorlevel 1 (
    echo  [ERROR] .NET SDK not found.
    echo  Download: https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    pause & exit /b 1
)

for /f "tokens=*" %%v in ('dotnet --version 2^>nul') do set DOTNET_VER=%%v
echo  .NET SDK: %DOTNET_VER%
echo.

cd /d "%~dp0"

REM ── Restore packages ───────────────────────────────────────────────────────
echo  [1/2] Restoring packages...
dotnet restore ClaudeUsageOverlay.csproj --nologo -v q
if errorlevel 1 (
    echo  [ERROR] Restore failed.
    pause & exit /b 1
)

REM ── Publish single EXE ─────────────────────────────────────────────────────
echo  [2/2] Publishing single EXE (self-contained, win-x64)...
echo        This may take ~60 seconds on first run.
echo.

dotnet publish ClaudeUsageOverlay.csproj ^
    --nologo ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -p:DebugType=embedded ^
    -p:DebugSymbols=false ^
    -o "%~dp0dist"

if errorlevel 1 (
    echo.
    echo  [ERROR] Build failed. See output above.
    pause & exit /b 1
)

REM ── Remove PDB if somehow present ──────────────────────────────────────────
if exist "%~dp0dist\ClaudeUsage.pdb" del /q "%~dp0dist\ClaudeUsage.pdb"

echo.
echo  ════════════════════════════════════════════
echo   SUCCESS!
echo   %~dp0dist\ClaudeUsage.exe
echo  ════════════════════════════════════════════
echo.
echo  First run: a login window will open — sign in once.
echo  After that the overlay auto-refreshes every 5 min.
echo.
explorer "%~dp0dist"
pause
