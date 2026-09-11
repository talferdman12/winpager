# Makes the WinPager bell sit on the taskbar itself instead of hiding behind the
# little arrow. Windows 11 stores this as a per-icon "IsPromoted" flag.
#
# Run WinPager at least once before this, so Windows has created its entry.
#
# This registry location is not documented by Microsoft. If it stops working on a
# future Windows build, use the Settings app instead:
#   Settings > Personalization > Taskbar > Other system tray icons > WinPager > On

$root = 'HKCU:\Control Panel\NotifyIconSettings'

if (-not (Test-Path $root)) {
    Write-Host "Windows has not created any tray icon settings yet." -ForegroundColor Yellow
    Write-Host "Run WinPager first, then run this again."
    exit 1
}

$matched = 0
foreach ($key in Get-ChildItem $root) {
    $path = (Get-ItemProperty $key.PSPath -ErrorAction SilentlyContinue).ExecutablePath
    if ($path -and $path -like '*WinPager.exe') {
        Set-ItemProperty -Path $key.PSPath -Name 'IsPromoted' -Value 1 -Type DWord
        Write-Host "Pinned: $path" -ForegroundColor Green
        $matched++
    }
}

if ($matched -eq 0) {
    Write-Host "No WinPager tray icon found in the registry." -ForegroundColor Yellow
    Write-Host "Run WinPager first, then run this again."
    exit 1
}

Write-Host ""
Write-Host "Restarting Explorer so the change takes effect..."
Stop-Process -Name explorer -Force
Start-Sleep -Seconds 2
if (-not (Get-Process -Name explorer -ErrorAction SilentlyContinue)) { Start-Process explorer }

Write-Host "Done. The bell should now be on the taskbar." -ForegroundColor Green
