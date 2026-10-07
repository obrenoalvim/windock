<div align="center">

<img src=".github/logo.svg" alt="WinDock logo" width="120" height="120">

# WinDock

**One Windows PC. Several taskbars.**<br>
Swap the pinned apps on your real Windows taskbar by preset and by schedule: work apps from 9 to 6, games after hours.

[![Download](https://img.shields.io/badge/Download-WinDock.zip-FFB547?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/obrenoalvim/windock/releases/latest/download/WinDock-win-x64.zip)

[![License: MIT](https://img.shields.io/badge/license-MIT-FFB547)](LICENSE)
[![GitHub stars](https://img.shields.io/github/stars/obrenoalvim/windock?style=flat&logo=github&color=3b9eff)](https://github.com/obrenoalvim/windock/stargazers)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](src/WinDock/WinDock.csproj)
[![Windows 10 | 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows&logoColor=white)](#faq)

**English** · [Português](README.pt-BR.md)

[How it works](#how-it-works) · [Usage](#usage) · [Presets](#presets) · [Hotkeys](#hotkeys) · [Build](#build) · [FAQ](#faq)

</div>

---

WinDock does not draw a second dock. The icons stay exactly where Windows puts them. WinDock is the orchestrator behind them: it keeps one **preset** per context (work, games, study...), each one a saved copy of your taskbar pins, and swaps them on a schedule or with one click.

Inspired by [PrimoDock](https://dock.oprimo.dev/) for macOS.

## How it works

Windows stores taskbar pins in the `Taskband` registry key plus the shortcuts in `%APPDATA%\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar`. A preset is a snapshot of both. Applying a preset closes the shell, restores the snapshot and starts `explorer.exe` again, which takes a couple of seconds.

1. Arrange the taskbar the normal Windows way (pin, unpin, drag to reorder).
2. In WinDock, hit **Capturar taskbar atual** on a preset.
3. Set the days and hours it should be active.

From then on WinDock swaps the real taskbar by itself. Nothing is installed into Windows, nothing runs in the background besides a tray icon.

## Usage

- **Window** (tray icon, or `Ctrl+Alt+W`): one card per preset with its name, the pinned shortcuts it holds, and its schedule. Per card: **Aplicar**, **Capturar taskbar atual**, **Excluir**.
- **Schedule**: toggle the weekdays, type the start and end time. Add as many time slots as you want.
- **+ Novo preset** creates a preset from the taskbar you have right now.
- **Troca automática** turns the schedule on or off. **Avisar trocas** shows a tray notification when it swaps.
- **Restaurar original** puts back the taskbar you had before WinDock ever touched it (saved on the first apply).

## Presets

Starter presets are installed on first run: **Trabalho** (Mon-Fri 09:00-18:00), **Jogos** (Mon-Fri 18:00-09:00), **Estudos** (manual) and **Fim de semana** (Sat-Sun). The first run saves your current taskbar into the preset that matches the current time, so nothing changes until you capture the others.

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
  ]
}
```

`days` accepts `Mon-Fri`, `Sat,Sun`, `Fri-Mon`. `end` is exclusive and `24:00` means midnight, so 18:00 to 09:00 is two slots. The first preset (by file name) that matches the current time wins. A captured preset also stores a `snapshot` block.

## Hotkeys

| Keys | Action |
|---|---|
| `Ctrl+Alt+W` | Open or hide the window |
| `Ctrl+Alt+1` ... `9` | Apply the Nth preset |

## Build

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows.

```powershell
dotnet run --project src/WinDock
dotnet test
dotnet publish src/WinDock -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Layout: `Models/` (presets, schedule, snapshot), `Services/` (taskbar, schedule resolver, presets store, tray), `ViewModels/` (MVVM Toolkit), `Views/` and `Themes/` (XAML).

## FAQ

**Why does the screen flicker when it switches?** Windows only reads taskbar pins when the shell starts, so WinDock restarts `explorer.exe`. Open File Explorer windows are closed by that. Turn off **Troca automática** if you do not want it to happen on its own.

**Does it close my apps?** No. Only the shell restarts. Running programs keep running.

**Can it break my taskbar?** The original state is saved before the first change and **Restaurar original** brings it back. Snapshots are copies of what Windows itself wrote.

**Which apps does a preset hold?** Everything pinned: classic shortcuts and Store apps. The card lists the classic shortcuts by name.

**Does it work on Windows 10?** The same keys exist, so it should, but it has only been tested on Windows 11.

## License

[MIT](LICENSE). Bundled fonts (Michroma, Space Mono) are under the SIL Open Font License.
