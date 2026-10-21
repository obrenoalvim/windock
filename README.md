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

WinDock draws its own dock at the bottom of every screen: your apps, folders and widgets on a glass bar, with the icon under the pointer growing like on a Mac. Each **environment** (Work, Games, Study, Weekend...) has its own apps, widgets and color, and switches on a schedule, with the mouse wheel or with a hotkey. Switching is instant.

Inspired by [PrimoDock](https://dock.oprimo.dev/) for macOS. Free and open source.

## Install

You do not need to install .NET or anything else. It is a single file.

1. **Download** [`WinDock.exe`](https://github.com/obrenoalvim/windock/releases/latest/download/WinDock.exe) (about 75 MB, everything included). It needs Windows 10 version 2004 or newer.
2. **Double-click it.** If Windows shows a blue "Windows protected your PC" screen, click **More info**, then **Run anyway**. That screen appears because the file is not code-signed yet.
3. **The dock appears** on every screen, and the settings window opens. A short tour on the dock shows the gestures once. WinDock also puts an icon in the system tray.
4. It starts with Windows from then on. Turn that off in the **Dock** tab, the dock menu or the tray menu if you prefer.

To remove it: right-click the dock and pick **Exit**, then delete `WinDock.exe` and the folder `%APPDATA%\WinDock`.

## Quick start

1. Open the settings (right-click the dock, the tray icon, or `Ctrl+Alt+W`). The **Environments** tab has one card per environment.
2. Click **+ Program** on a card and pick apps, Microsoft Store apps, Steam and Epic games, or a **Folder...** to stack in the dock (Downloads is a good one).
3. Turn on the **widgets** you want for that environment: clock, weather, music, Claude usage, Pomodoro, stopwatch, timer, countdown, progress, activity, network speed, battery, microphone, note, to-do, RigDeck, Steam games, Epic games.
4. Under **Schedule**, pick the days and hours the environment is active. Or skip it and switch by hand: scroll the mouse wheel over the dock, press `Ctrl+Alt+1..9`, or pick one in the menu of the dock (right-click the glass).

Programs in the **Always pinned** card show in every environment, before the others.

## The dock

- **Magnifying lens.** The icon under the pointer grows from its base, past the edge of the glass, and pushes its neighbors. Pick the effect (none, scale, magnify), the magnification, the lens width and the icon size in the **Dock** tab.
- **Open apps.** A dot under an icon means the app is open; the colored bar marks the window in front. Apps you have open but did not pin show after a divider, so the dock also works as a taskbar. A program split over several exes with the same icon (Google Play Games) takes one slot, and shortcuts like Discord's light up the icon you pinned.
- **Unread badges.** A red count on the icon of WhatsApp, Teams, Mail and any app that puts a badge on the Windows taskbar, and on windows whose title starts with a count, like "(3) WhatsApp" in the browser.
- **Live previews.** Rest the pointer on an open app to see each of its windows live, with a close button.
- **Folder stacks.** A pinned folder opens as a grid of its newest files. Downloads shows a badge with how many new files arrived. Drag a file out of the stack into any app.
- **Environments.** Each one has its apps, widgets and accent color. The dock changes color when you switch, and a label shows the new environment.
- **Windows follow the environment** (optional). When you switch, the windows of the previous environment step aside and come back when you return. Programs that are always pinned stay visible everywhere.
- **Every screen.** The dock shows on each monitor by default, or on the one you pick, and follows monitors as they come and go. With **Each screen has its own dock**, every screen lists the windows open on it and can sit on an environment of its own (scroll or right-click the dock of that screen).
- **The Windows basics stay.** When the Windows taskbar is out of sight (replaced, or set to hide automatically), the dock brings Start, the internet status (Wi-Fi name and signal, Ethernet, no internet), the volume, the battery with time left, the clock and calendar, the tray icons and show desktop. Start sits at the left end, like on Windows, and the rest together at the right. Network, sound, microphone and battery share one pill like on Windows 11; click the microphone to mute it. The `^` button lists the apps of the tray, to open or quit them without the Windows taskbar.
- **Sound.** Scroll on the speaker to change the volume, middle-click to mute, click for a panel with a volume slider, every output device to switch to with one click (speakers, headphones, monitor), the mixer and the sound settings.
- **Replace the taskbar** (optional). The Windows taskbar hides for good while WinDock runs. The `^` button shows it for a moment, with its tray icons.
- **Safe by design.** The taskbar, the reserved screen space and any hidden window come back when WinDock closes. If WinDock crashes, a small watchdog process puts everything back.
- **Reserve screen space** so maximized windows stop above the dock, or **hide automatically** so it slides away until the pointer touches the bottom edge.
- **Full screen aware.** The dock steps aside while a game or video runs in full screen.
- Nine themes in the **Dock** tab: Dark, Light, Midnight, Neon, Canarinho, Aqua, Sunset, Paper and Floating (no bar, icons over the desktop). Any monitor, drag files or programs onto the dock to pin them, and too many icons shrink to fit.

## Widgets

| Widget | What it shows | Click | Other |
|---|---|---|---|
| Clock | Time and date | Opens the calendar and notifications | Steps aside when the dock already shows the Windows clock |
| Weather | Temperature and condition, blue by day and indigo at night | Panel with feels like, high and low, and the next hours | Type the city on the environment card, or leave it empty to use the Windows location; data from Open-Meteo |
| Music | What plays in Spotify, the browser or any player: square, the cover; wide, the cover, title, artist and progress with play and pause, or with every control (like PrimoDock), or over the blurred cover | Panel with every player that has media, side by side, each with previous, play and next | Hides while nothing plays |
| Claude usage | The 5-hour session and 7-day week of your Claude plan | Panel with the reset times, the weekly pace and a refresh | Turns red at 90%; notifies at 80% and 95%; needs Claude Code signed in on the PC |
| Pomodoro | 25 min focus, 5 min break, 15 min every fourth | Start or pause | Counts the cycles done today; right-click to reset; notifies at the end |
| Stopwatch | Elapsed time | Start or pause | Right-click to reset |
| Timer | Countdown with a ring | Start or pause | Scroll to set the minutes, which are remembered; notifies at the end |
| Countdown | Days left to a date | | Set the date and name on the environment card; Christmas by default |
| Progress | How much of the day has passed | Cycles day, week, month, year | |
| Activity | Processor and memory use | | |
| Network speed | Download and upload right now | | |
| Battery | Charge, or plugged in | | Time left in the tooltip |
| Note | A short note that stays in the dock; square, a small post-it with the text | Opens an editor | Saved with the environment |
| Microphone | Whether the microphone is on | Mutes or unmutes it | Red while muted |
| To-do | How many tasks are left and the next one; square, the count with a ring of what is done | Panel to add (type and Enter), check off and remove tasks, and clear the done ones | Saved with the environment |
| RigDeck | The RigDeck mark and how many shortcuts you have | Panel with PC controls (mic, volume, media, lock, screenshot) and your shortcuts: one app, a site, a command, a key combo, a sound, or a combo that does several at once, each app on the monitor you pick or in full screen | [RigDeck](https://github.com/obrenoalvim/rigdeck) as a widget, no server needed. Right-click a shortcut to close what it opened; a shortcut to a folder lists its files; saved with the environment |
| Steam games | The Steam logo and how many games are installed | Panel with a search box; click a game to launch it | **Installed** or **All**: All also lists the games you own but have not installed (click installs; needs your Steam game details public) |
| Epic games | The Epic logo and how many games are installed | Panel with a search box; click a game to launch it | Installed games only, because Epic keeps the rest of the library behind its login |

**Square or wide, per widget.** Right-click a widget on the dock and pick **Square** or **Wide**. Square is the size of an icon: the main value, a ring for progress and timers, two rings for Claude (the 5-hour session outside, the week inside), the cover for music. Wide shows more detail. Music has three wide looks: **Wide, pause only**, **Wide with controls** and **Cover as background**. Widgets you never sized follow **Square widgets by default** in the **Dock** tab (on by default; the note stays wide unless you pick square). The environment card lists the size of each widget too.

**Rearrange.** Drag a widget along the dock to move it, or right-click it and pick **Move left** or **Move right**. The order is saved with the environment.

Widgets keep running when you switch environments, so a Pomodoro started at work still finishes on time.

## Shortcuts

| Keys | What it does |
|---|---|
| `Ctrl+Alt+W` | Open or hide the settings window |
| `Ctrl+Alt+1` ... `9` | Switch to the 1st ... 9th environment |
| `Win+1` ... `9` | With the taskbar replaced: open the 1st ... 9th app of the dock, like a click (`Win+Shift`: a new window) |
| `Esc` | Close the settings window (the app keeps running) |

| Mouse on the dock | What it does |
|---|---|
| Click an app | Open it, bring its window to the front, or minimize it if it is already in front. Several windows: shows them to pick one |
| `Shift`+click or middle click | Open a new window |
| `Ctrl`+click | Show the program in File Explorer |
| Right-click an app | New window, pin or unpin (this environment or all), show in Explorer, close windows |
| Drag an icon sideways | Move it; drag it up and let go to remove it (with **Undo**) |
| Rest on an open app | Live preview of its windows |
| Scroll the wheel | Switch to the previous or next environment |
| Right-click the glass | Environments, replace taskbar, hide automatically, windows follow, start with Windows, settings, exit |
| Drop files on an app | Open them with that app |
| Drop files or programs on the glass | Pin them to the current environment |

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

`days` accepts `Mon-Fri`, `Sat,Sun`, `Fri-Mon`. `end` is exclusive and `24:00` means midnight. The first environment (by file name) that matches the current time wins. `target` is an `.exe`, `.lnk` or folder (environment variables work). Store apps and games are saved as a launcher (`target` plus `args`, `icon` and an `appId`), for example `steam://rungameid/1243130`. Widget kinds: `clock`, `weather` (`label` is the city), `music`, `claude`, `pomodoro`, `stopwatch`, `timer`, `countdown`, `progress`, `activity`, `network`, `battery`, `mic`, `note` (`text` is the note), `todo` (`text` holds one task per line, `[x] ` in front of the done ones), `rigdeck` (`text` holds its shortcuts as JSON, made in the panel), `steam` (`text` set to `all` also lists the games you have not installed) and `epic`. `size` is `square` or `wide` (music also takes `full` and `backdrop`); leave it out to follow the default. A countdown `date` is `yyyy-MM-dd` for a fixed day or `MM-dd` to repeat every year.

### The old taskbar mode

Before the dock, WinDock swapped the pinned apps of the real Windows taskbar. That still works: turn on **Also swap the Windows taskbar pins** in the **Dock** tab. Each switch then rewrites the taskbar pin storage (the `Taskband` registry key and the `User Pinned\TaskBar` shortcuts) and restarts `explorer.exe` behind a picture of the taskbar. **Restore original taskbar** brings back the pins you had before WinDock touched them.

## FAQ

**Does it replace the taskbar?** Only if you turn it on. By default the dock sits above the taskbar. With **Replace the Windows taskbar** on, the taskbar hides, and the dock brings Start, the clock and quick settings. The `^` button shows the real taskbar for its tray icons. Close WinDock and the taskbar is back exactly as it was, even after a crash.

**What does the watchdog do?** When WinDock hides the taskbar or a window, it starts a tiny copy of itself (`WinDock.exe --watchdog`) that only waits. If WinDock ends for any reason, the watchdog shows the taskbar and every hidden window again. If both are killed, the next start of WinDock cleans up.

**Does the Claude usage widget read my chats?** No. It reads only the two usage percentages and their reset times, from the same Anthropic endpoint Claude Code uses, with the sign-in Claude Code already keeps on this PC. WinDock asks every five minutes at most. Claude Code renews that sign-in only while it runs, so when it has expired (after the PC was off for the night) WinDock renews it the same way Claude Code does and saves the new one in the same file, so Claude Code keeps working. It sends the sign-in nowhere but Anthropic (api.anthropic.com and platform.claude.com).

**What does the weather widget send?** Only the city you typed, to Open-Meteo (free, no account), and then its coordinates to get the forecast, every 10 minutes while the widget is on the dock (far below the free limit of 10,000 calls a day). With the city empty it asks Windows for the location, which works only if desktop apps may use it in the Windows privacy settings.

**Are environments virtual desktops?** No, they work on the desktop you are on. With **Windows follow the environment** on, the windows of other environments are hidden instead, and they return when you switch back. Windows that run as administrator (like Task Manager) cannot be hidden by a normal app and stay where they are.

**Why did the dock hide while I play?** WinDock steps aside when the window in front covers the whole screen, like a game or a video in full screen.

**Do the unread badges read my messages?** No. Windows keeps the number each app puts on its taskbar icon in its notification database; WinDock reads only those numbers, read only, and never the text of a notification.

**Does it check for updates?** Every few hours it asks GitHub (api.github.com) for the tag of the latest release, and nothing else. A newer version shows in the tray menu and the **Dock** tab with a download link; nothing is downloaded or installed by itself. Turn it off with **Tell me when a new version is out**.

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
