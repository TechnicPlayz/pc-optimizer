@echo off
setlocal
title Velora PC - Builder
where dotnet >nul 2>nul
if errorlevel 1 (
    echo ERROR: .NET 8 SDK is not installed. Get it from https://dotnet.microsoft.com/download/dotnet/8.0
    pause & exit /b 1
)
echo Building Velora PC...
dotnet publish src\VeloraPC\VeloraPC.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
if errorlevel 1 ( echo BUILD FAILED. & pause & exit /b 1 )
echo.
echo EXE: publish\VeloraPC.exe
where iscc >nul 2>nul
if not errorlevel 1 (
    echo Building installer...
    iscc installer\VeloraPC.iss
    echo Installer: dist\
) else (
    echo Inno Setup not found - skipping installer. See README.md.
)
start "" explorer.exe "%CD%\publish"
pause
