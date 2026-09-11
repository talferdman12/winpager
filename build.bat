@echo off
REM Builds a single ChabadOfficePager.exe into the publish\ folder.
REM Requires the .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0

setlocal
cd /d "%~dp0"

echo Building Chabad Office Pager...
dotnet publish src\ChabadOfficePager.csproj -c Release -r win-x64 --self-contained false -o publish
if errorlevel 1 (
    echo.
    echo BUILD FAILED. Make sure the .NET 8 SDK is installed.
    pause
    exit /b 1
)

echo.
echo Done. Your app is at: %cd%\publish\ChabadOfficePager.exe
pause
