@echo off
REM Double-click this to keep the WinPager bell visible on the taskbar.
REM Run WinPager at least once first.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0pin-to-taskbar.ps1"
pause
