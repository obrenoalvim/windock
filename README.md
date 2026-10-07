<div align="center">

<img src=".github/logo.svg" alt="WinDock logo" width="120" height="120">

# WinDock

**One Windows PC. Several taskbars.**<br>
Swap the pinned apps on your real Windows taskbar by preset and by schedule: work apps from 9 to 6, games after hours. One click or one hotkey.

[![Download WinDock.exe](https://img.shields.io/badge/Download-WinDock.exe-FFB547?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/obrenoalvim/windock/releases/latest/download/WinDock.exe)

[![License: MIT](https://img.shields.io/badge/license-MIT-FFB547)](LICENSE)
[![GitHub stars](https://img.shields.io/github/stars/obrenoalvim/windock?style=flat&logo=github&color=3b9eff)](https://github.com/obrenoalvim/windock/stargazers)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](src/WinDock/WinDock.csproj)
[![Windows 10 | 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows&logoColor=white)](#faq)

**English** · [Português](README.pt-BR.md)

[Install](#install) · [Quick start](#quick-start) · [Shortcuts](#keyboard-shortcuts) · [How it works](#how-it-works) · [Presets](#presets) · [FAQ](#faq) · [Build](#build)

</div>

---

WinDock does not draw a second dock. The icons stay exactly where Windows puts them. WinDock is the orchestrator behind them: it keeps one **preset** per context (work, games, study...), each one a list of programs shown with their real icons, and swaps the taskbar on a schedule or with one click.

Inspired by [PrimoDock](https://dock.oprimo.dev/) for macOS.

## Install

You do not need to install .NET or anything else. It is a single file.

1. **Download** [`WinDock.exe`](https://github.com/obrenoalvim/windock/releases/latest/download/WinDock.exe) (about 65 MB, everything included).
2. **Double-click it.** If Windows shows a blue "Windows protected your PC" screen, click **More info**, then **Run anyway**. That screen appears because the file is not code-signed yet, and every new app gets it.
3. **WinDock opens** and puts an icon in the system tray, next to the clock (click the small arrow if you do not see it). Closing the window does not quit the app, it keeps running there.
4. Optional: right-click the tray icon and turn on **Iniciar com o Windows** so it starts with your PC.

To remove it: right-click the tray icon, pick **Restaurar original** in the window if you want your old taskbar back, then **Sair** and delete `WinDock.exe` and the folder `%APPDATA%\WinDock`.

## Quick start

1. Open the window (tray icon, or `Ctrl+Alt+W`). You see one card per preset: **Trabalho**, **Jogos**, **Estudos** and **Fim de semana**. Your current taskbar was copied into the one that matches the current time.
2. Click **+ Programa** on a card and pick what should be pinned. The picker has tabs for your programs, Microsoft Store apps (WhatsApp, Calculator...), and the Steam and Epic Games you have installed.
3. Under **Agenda**, pick the days and hours the preset should be active. Or skip it and switch by hand.
4. Click **Aplicar**, or just wait for the schedule. The taskbar changes in about half a second for a preset you have used before.

Programs in the **Sempre fixos** card at the top (say Brave, File Explorer, Spotify) go first in every preset, so each preset only needs the extra ones.

## Keyboard shortcuts

| Keys | What it does |
|---|---|
| `Ctrl+Alt+W` | Open or hide the WinDock window |
| `Ctrl+Alt+1` ... `9` | Apply the 1st ... 9th preset (the order of the cards) |
| `Esc` | Close the window (the app keeps running in the tray) |

| Mouse | What it does |
|---|---|
| Tray icon, left click | Open or hide the window |
| Tray icon, right click | Preset list, switches and exit |
| Right-click an app icon on a card | Move left, move right, remove |
| **Aplicar** on a card | Put that preset on the taskbar |
| **Usar taskbar atual** on a card | Replace the card's programs with what is pinned right now |
| **+ Novo preset** | Create an empty preset |
| **Restaurar original** | Bring back the taskbar you had before WinDock touched it |

The tray menu also has **Troca automática por horário**, **Avisar trocas** and **Iniciar com o Windows**. Opening WinDock a second time just brings the window forward.

## How it works

Windows stores taskbar pins in the `Taskband` registry key plus the shortcuts in `%APPDATA%\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar`. A preset is a list of programs. Applying one writes those two places for exactly that list, then restarts `explorer.exe` so Windows reads them. WinDock covers that restart with a picture of the taskbar so it does not look like a flicker.

- Switching never closes your programs. Only the Windows shell restarts.
- The original taskbar is saved before the first change, so **Restaurar original** always works.
- If the pins are already the ones you asked for, nothing restarts.
- If you ask for another preset in the middle of a switch, the latest one wins.

## Presets

Starter presets are installed on first run: **Trabalho** (Mon-Fri 09:00-18:00), **Jogos** (Mon-Fri 18:00-09:00), **Estudos** (manual) and **Fim de semana** (Sat-Sun). Programs that are not installed on your machine are left out.

Presets live in `%APPDATA%\WinDock\presets` as JSON, and the window edits them for you:

```json
{
  "id": "jogos",
  "name": "Jogos",
  "glyph": "🎮",
  "accent": "#7C5CFF",
  "schedule": [
    { "days": "Mon-Fri", "start": "18:00", "end": "24:00" },
    { "days": "Mon-Fri", "start": "00:00", "end": "09:00" }
  ],
  "items": [
    { "name": "Steam", "target": "%ProgramData%\\Microsoft\\Windows\\Start Menu\\Programs\\Steam\\Steam.lnk" },
    { "name": "Spotify", "target": "%APPDATA%\\Spotify\\Spotify.exe" }
  ]
}
```

`days` accepts `Mon-Fri`, `Sat,Sun`, `Fri-Mon`. `end` is exclusive and `24:00` means midnight, so 18:00 to 09:00 is two slots. The first preset (by file name) that matches the current time wins. `target` is an `.exe` or `.lnk` (environment variables work). Store apps and games are saved as a launcher (`target` plus `args`, `icon` and an `appId`), for example `steam://rungameid/1243130`.

## Build

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows.

```powershell
dotnet run --project src/WinDock
dotnet test
dotnet publish src/WinDock -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

The last command produces the single `publish/WinDock.exe` that is attached to each release. Layout: `Models/` (presets, schedule, snapshot), `Services/` (taskbar, blob builder, catalog, tray), `ViewModels/` (MVVM Toolkit), `Views/` and `Themes/` (XAML).

## FAQ

**Why is there a pause when it switches?** Windows only reads taskbar pins when the shell starts, so WinDock restarts `explorer.exe`, which takes about two seconds. To keep it subtle, WinDock covers the restart with pictures of the taskbar: it remembers how each preset looked, so a preset you have used before appears in about half a second while Windows catches up behind it, and then fades into the real one. The first switch to a preset never seen before holds the old picture instead. You can pick another preset right away: the latest choice wins. Open File Explorer windows are still closed by the restart, and desktop icons blink. Turn off **Troca automática** if you do not want it to happen on its own. Windows 11 24H2 and later block pinning from other programs, so there is no restart-free way to add pins.

**Does it close my apps?** No. Only the shell restarts. Running programs keep running.

**Can it break my taskbar?** The original state is saved before the first change and **Restaurar original** brings it back. If writing the new taskbar fails, the previous one is put back.

**Why did an icon show up blank?** Windows keeps one entry per program. The same program listed twice, even through different shortcuts, used to produce a blank icon. WinDock now removes duplicates for you.

**Which programs can a preset hold?** Any `.exe` or `.lnk`, Microsoft Store apps, and installed Steam and Epic Games games.

**Does it work on Windows 10?** It is built on the same registry key and shortcut folder that Windows 10 uses, with no Windows 11 only API, but it has only been tested on Windows 11. Reports from Windows 10 are welcome.

**Is it safe? Why does Windows warn me?** The code is all in this repository. The warning is Windows being cautious with an unsigned file. WinDock writes only the taskbar pin storage of your own user and restarts the shell, nothing else.

## License

[MIT](LICENSE). Bundled fonts (Michroma, Space Mono) are under the SIL Open Font License.
