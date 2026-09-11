# WinPager

A small Windows system tray app for paging desks around the office. Click the tray
icon, click a name, and that person gets a Windows notification saying who paged them.

No server, no accounts, no internet. Each PC finds the others on the office network
by itself.

## What the user sees

- **Tray icon** — a bell in the notification area. Left-click opens the pager.
- **The pop-up** — one big button per desk that's currently online, plus an optional
  note box ("call on line 2").
- **Being paged** — a Windows notification plus a large red on-screen alert naming
  whoever paged you. Both are optional per person in Settings.
- **Confirmation** — the sender gets a small "they got your page" notification, or a
  warning if that PC didn't answer.

## Install on each PC

1. Install the **.NET 8 Desktop Runtime** (one-time, free):
   https://dotnet.microsoft.com/download/dotnet/8.0 — pick *Desktop Runtime, x64*.
2. Copy `WinPager.exe` anywhere, e.g. `C:\WinPager\`.
3. Double-click it. On first run it asks what to call that desk — use the desk or
   person's name ("Front Desk", "Rabbi's Office"), because that's the button label
   everyone else sees.
4. When Windows asks about network access, tick **Private networks** and allow it.
5. Repeat on every PC. They find each other within a few seconds.

"Start automatically when Windows starts" is on by default, so it comes back after
a reboot.

## Building from source

Needs the .NET 8 **SDK** (not just the runtime). Then:

```
build.bat
```

The finished app lands in `publish\WinPager.exe`.

## How it works

Every copy broadcasts a small "I'm here" packet on **UDP port 45654** every 5 seconds,
so each PC keeps a live list of who's online. Desks that stop broadcasting drop off
the list after 20 seconds.

A page is sent directly to that one PC, which replies with a confirmation. If no
confirmation arrives the page is retried once, then reported as undelivered. Repeated
pages carrying the same id are only shown once, so a retry never buzzes a desk twice.

Everything stays on the local network. Nothing leaves the building.

## Updates

Right-click the tray icon and choose **Check for updates**. If a newer release exists,
WinPager offers to open the download page; it never installs anything by itself.

It also checks quietly once a day in the background and only speaks up when there is
something newer. Turn that off in Settings if you'd rather it stayed off the internet
entirely. The check contacts `api.github.com` and sends nothing but the request itself,
no names, no machine details.

To update a PC: close WinPager from the tray, replace the exe, start it again. Settings
and the desk name are kept.

## Keeping the bell on the taskbar

By default Windows 11 hides new tray icons behind the small arrow. To keep WinPager
visible, pick either:

- **Drag it.** Click the arrow, then drag the bell down onto the taskbar. It stays.
- **Settings.** Settings > Personalization > Taskbar > Other system tray icons, then
  turn **WinPager** on.
- **Script.** Run WinPager once, then double-click `pin-to-taskbar.bat`. It sets the
  flag for you and restarts Explorer. This one uses an undocumented registry value,
  so treat it as a convenience rather than something to rely on.

There is no way for the app to claim a permanent taskbar slot on its own. Windows
treats it as the user's choice.

## Settings

Right-click the tray icon and choose **Settings**:

| Setting | What it does |
| --- | --- |
| Desk name | The button label everyone else sees |
| Play a sound | Plays the Windows alert sound when paged |
| Large on-screen alert | Shows the red alert window, not just the notification |
| Start with Windows | Adds a login entry for the current user |
| Check for updates | Asks GitHub once a day whether a newer release exists |

Settings live in `%APPDATA%\WinPager\config.json`. Two extra values in that
file are not in the Settings window:

- `Port` — the UDP port. Change it only if something else needs 45654, and then
  change it on **every** PC.
- `GroupKey` — a shared label. PCs only see each other when this matches, which is
  handy if you ever want two independent pager groups on one network.

## If someone doesn't show up in the list

1. **Both PCs on the same network?** Wi-Fi and Ethernet on the same office router is
   fine. A guest network or a VPN is not.
2. **Firewall.** Right-click `allow-through-firewall.bat` → Run as administrator, on
   the PC that isn't appearing.
3. **Is it running there?** Look for the bell in that PC's tray. It hides in the
   overflow arrow by default; drag it onto the visible part of the taskbar.
4. **Check the log** at `%APPDATA%\WinPager\pager.log`. It records every
   discovery, page, and error.

Some business networks block broadcast traffic between wired and wireless segments.
If wired PCs see each other but wireless ones don't, that's the cause, and it's a
setting on the router or access point.

## Notes and limits

- Windows only. It needs the Windows notification area and toast notifications.
- Pages reach PCs that are awake. A sleeping or shut-down PC drops off the list, and
  the sender is told the page wasn't delivered.
- Anyone on the office network running this app can page anyone else. There's no
  password; it assumes the network itself is the office.
