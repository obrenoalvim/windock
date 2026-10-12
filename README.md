<div align="center">

<img src=".github/logo.svg" alt="WinDock logo" width="120" height="120">

# WinDock

**One Windows PC. A dock for every moment.**<br>
A glass dock for Windows with environments, live widgets and a Mac-style magnifying lens. Work apps from 9 to 6, games after hours, each with its own dock. It can even replace the taskbar.

[![Download WinDock.exe](https://img.shields.io/badge/Download-WinDock.exe-FFB547?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/obrenoalvim/windock/releases/latest/download/WinDock.exe)

[![License: MIT](https://img.shields.io/badge/license-MIT-FFB547)](LICENSE)
[![GitHub stars](https://img.shields.io/github/stars/obrenoalvim/windock?style=flat&logo=github&color=3b9eff)](https://github.com/obrenoalvim/windock/stargazers)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](src/WinDock/WinDock.csproj)
[![Windows 10 | 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows&logoColor=white)](#faq)

**English** · [Português](README.pt-BR.md)

[Install](#install) · [Quick start](#quick-start) · [The dock](#the-dock) · [Widgets](#widgets) · [Shortcuts](#shortcuts) · [Environments](#environments) · [FAQ](#faq) · [Build](#build)

</div>

---

WinDock draws its own dock at the bottom of the screen: your apps, folders and widgets on a glass bar, with the icon under the pointer growing like on a Mac. Each **environment** (Work, Games, Study, Weekend...) has its own apps, widgets and color, and switches on a schedule, with the mouse wheel or with a hotkey. Switching is instant.

Inspired by [PrimoDock](https://dock.oprimo.dev/) for macOS. Free and open source.

## Install

You do not need to install .NET or anything else. It is a single file.

1. **Download** [`WinDock.exe`](https://github.com/obrenoalvim/windock/releases/latest/download/WinDock.exe) (about 65 MB, everything included).
2. **Double-click it.** If Windows shows a blue "Windows protected your PC" screen, click **More info**, then **Run anyway**. That screen appears because the file is not code-signed yet.
3. **The dock appears** above the taskbar, and the settings window opens. WinDock also puts an icon in the system tray.
4. Optional: right-click the tray icon and turn on **Start with Windows**.

To remove it: right-click the dock and pick **Exit**, then delete `WinDock.exe` and the folder `%APPDATA%\WinDock`.

## Quick start

1. Open the settings (right-click the dock, the tray icon, or `Ctrl+Alt+W`). The **Environments** tab has one card per environment.
2. Click **+ Program** on a card and pick apps, Microsoft Store apps, Steam and Epic games, or a **Folder...** to stack in the dock (Downloads is a good one).
3. Turn on the **widgets** you want for that environment: clock, Pomodoro, stopwatch, timer, countdown, progress, activity, battery.
4. Under **Schedule**, pick the days and hours the environment is active. Or skip it and switch by hand: scroll the mouse wheel over the dock, click the dots at its right end, or press `Ctrl+Alt+1..9`.

Programs in the **Always pinned** card show in every environment, before the others.

## The dock

- **Magnifying lens.** The icon under the pointer grows from its base, past the edge of the glass, and pushes its neighbors. Pick the effect (none, scale, magnify), the magnification, the lens width and the icon size in the **Dock** tab.
- **Open apps.** A dot under an icon means the app is open; the colored bar marks the window in front. Apps you have open but did not pin show after a divider, so the dock also works as a taskbar.
- **Live previews.** Rest the pointer on an open app to see each of its windows live, with a close button.
- **Folder stacks.** A pinned folder opens as a grid of its newest files. Downloads shows a badge with how many new files arrived. Drag a file out of the stack into any app.
- **Environments.** Each one has its apps, widgets and accent color. The dock changes color when you switch, and a label shows the new environment.
- **Windows follow the environment** (optional). When you switch, the windows of the previous environment step aside and come back when you return. Programs that are always pinned stay visible everywhere.
- **Replace the taskbar** (optional). The Windows taskbar hides and the dock gains Start, the clock and quick settings (network, sound, battery). The `^` button shows the taskbar for a moment, with its tray icons.
- **Safe by design.** The taskbar, the reserved screen space and any hidden window come back when WinDock closes. If WinDock crashes, a small watchdog process puts everything back.
- **Reserve screen space** so maximized windows stop above the dock, or **hide automatically** so it slides away until the pointer touches the bottom edge.
- **Full screen aware.** The dock steps aside while a game or video runs in full screen.
- Dark and light glass, any monitor, drag files or programs onto the dock to pin them, and too many icons shrink to fit.

## Widgets

| Widget | What it shows | Click | Other |
|---|---|---|---|
| Clock | Time and date | Opens the calendar and notifications | |
| Pomodoro | 25 min focus, 5 min break, 15 min every fourth | Start or pause | Right-click to reset; notifies at the end |
| Stopwatch | Elapsed time | Start or pause | Right-click to reset |
| Timer | Countdown with a ring | Start or pause | Scroll to set the minutes; notifies at the end |
| Countdown | Days left to a date | | Set the date and name on the environment card; Christmas by default |
| Progress | How much of the day has passed | Cycles day, week, month, year | |
| Activity | Processor and memory use | | |
| Battery | Charge, or plugged in | | |

Widgets keep running when you switch environments, so a Pomodoro started at work still finishes on time.

## Shortcuts

| Keys | What it does |
|---|---|
| `Ctrl+Alt+W` | Open or hide the settings window |
| `Ctrl+Alt+1` ... `9` | Switch to the 1st ... 9th environment |
| `Esc` | Close the settings window (the app keeps running) |

| Mouse on the dock | What it does |
|---|---|
| Click an app | Open it, bring its window to the front, or minimize it if it is already in front. Several windows: shows them to pick one |
| `Shift`+click or middle click | Open a new window |
| `Ctrl`+click | Show the program in File Explorer |
| Right-click an app | New window, pin or unpin (this environment or all), show in Explorer, close windows |
| Rest on an open app | Live preview of its windows |
| Scroll the wheel | Switch to the previous or next environment |
| Right-click the glass | Environments, replace taskbar, hide automatically, windows follow, settings, exit |
| Drop files or programs | Pin them to the current environment |

## Environments

Starter environments are installed on first run: **Work** (Mon-Fri 09:00-18:00), **Games** (Mon-Fri 18:00-09:00), **Study** (manual) and **Weekend** (Sat-Sun). Programs that are not installed on your machine are left out.

Environments live in `%APPDATA%\WinDock\presets` as JSON, and the settings window edits them for you:

```json
{
  "id": "jogos",
  "name": "Games",
  "glyph": "🎮",
  "accent": "#7C5CFF",
  "schedule": [
    { "days": "Mon-Fri", "start": "18:00", "end": "24:00" },
    { "days": "Mon-Fri", "start": "00:00", "end": "09:00" }
  ],
  "items": [
    { "name": "Steam", "target": "%ProgramData%\\Microsoft\\Windows\\Start Menu\\Programs\\Steam\\Steam.lnk" },
    { "name": "Downloads", "target": "%USERPROFILE%\\Downloads" }
  ],
  "widgets": [
    { "kind": "clock" },
    { "kind": "timer", "minutes": 10 },
    { "kind": "countdown", "date": "12-25", "label": "Christmas" }
  ]
}
```

`days` accepts `Mon-Fri`, `Sat,Sun`, `Fri-Mon`. `end` is exclusive and `24:00` means midnight. The first environment (by file name) that matches the current time wins. `target` is an `.exe`, `.lnk` or folder (environment variables work). Store apps and games are saved as a launcher (`target` plus `args`, `icon` and an `appId`), for example `steam://rungameid/1243130`. Widget kinds: `clock`, `pomodoro`, `stopwatch`, `timer`, `countdown`, `progress`, `activity`, `battery`. A countdown `date` is `yyyy-MM-dd` for a fixed day or `MM-dd` to repeat every year.

### The old taskbar mode

Before the dock, WinDock swapped the pinned apps of the real Windows taskbar. That still works: turn on **Also swap the Windows taskbar pins** in the **Dock** tab. Each switch then rewrites the taskbar pin storage (the `Taskband` registry key and the `User Pinned\TaskBar` shortcuts) and restarts `explorer.exe` behind a picture of the taskbar. **Restore original taskbar** brings back the pins you had before WinDock touched them.

## FAQ

**Does it replace the taskbar?** Only if you turn it on. By default the dock sits above the taskbar. With **Replace the Windows taskbar** on, the taskbar hides, and the dock brings Start, the clock and quick settings. The `^` button shows the real taskbar for its tray icons. Close WinDock and the taskbar is back exactly as it was, even after a crash.

**What does the watchdog do?** When WinDock hides the taskbar or a window, it starts a tiny copy of itself (`WinDock.exe --watchdog`) that only waits. If WinDock ends for any reason, the watchdog shows the taskbar and every hidden window again. If both are killed, the next start of WinDock cleans up.

**Are environments virtual desktops?** No, they work on the desktop you are on. With **Windows follow the environment** on, the windows of other environments are hidden instead, and they return when you switch back. Windows that run as administrator (like Task Manager) cannot be hidden by a normal app and stay where they are.

**Why did the dock hide while I play?** WinDock steps aside when the window in front covers the whole screen, like a game or a video in full screen.

**Does it slow the PC down?** At rest it uses under 1% of one processor core and around 170 MB of memory, most of it the .NET runtime packed inside the single exe. It listens to Windows window events instead of polling.

**Which programs can it pin?** Any `.exe`, `.lnk` or folder, Microsoft Store apps, and installed Steam and Epic Games games. A running game lights up its pinned icon.

**Does it work on Windows 10?** It uses no Windows 11 only API, but it has only been tested on Windows 11. Reports from Windows 10 are welcome.

**Is it safe? Why does Windows warn me?** The code is all in this repository. The warning is Windows being cautious with an unsigned file. WinDock changes only your own user's settings: the taskbar visibility and auto-hide while replacing it, and, in the old taskbar mode, the taskbar pins.

## Build

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows.

```powershell
dotnet run --project src/WinDock
dotnet test
dotnet publish src/WinDock -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

`WinDock.exe --exit` closes a running WinDock cleanly, which is handy before rebuilding. Layout: `Models/` (environments, schedule, settings), `Services/` (window tracking, taskbar and watchdog, launchers, taskbar pins, tray), `ViewModels/` (dock tiles, widgets, MVVM Toolkit), `Views/` (dock window, lens panel, previews, folder stacks, settings) and `Themes/` (XAML).

## License

[MIT](LICENSE). Bundled fonts (Michroma, Space Mono) are under the SIL Open Font License.
