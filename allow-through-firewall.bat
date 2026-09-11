@echo off
REM Run once per PC, as Administrator, if pages aren't getting through.
REM Opens UDP 45654 on private (office) networks only.

net session >nul 2>&1
if errorlevel 1 (
    echo Right-click this file and choose "Run as administrator".
    pause
    exit /b 1
)

netsh advfirewall firewall delete rule name="WinPager" >nul 2>&1
netsh advfirewall firewall add rule name="WinPager" dir=in action=allow protocol=UDP localport=45654 profile=private
netsh advfirewall firewall add rule name="WinPager" dir=out action=allow protocol=UDP localport=45654 profile=private

echo.
echo Firewall rule added for UDP port 45654 on private networks.
pause
