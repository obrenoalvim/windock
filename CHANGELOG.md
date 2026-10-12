# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [0.3.0] - 2026-10-12

### Added
- WinDock draws its own dock: a glass bar with your apps, folders and widgets, and a Mac-style magnifying lens that grows the icon under the pointer from its base and pushes its neighbors. The effect (none, scale, magnify), the magnification, the lens width and the icon size are adjustable.
- Presets are now environments of the dock. Switching is instant: by schedule, with the mouse wheel over the dock, with the dots at its right end, or with `Ctrl+Alt+1..9`. The dock takes the color of the environment and shows its name for a moment.
- Open apps show a dot under their icon and a colored bar for the window in front; open apps that are not pinned show after a divider. Clicking opens, brings to front or minimizes; `Shift` or middle click opens a new window; `Ctrl` click shows the program in File Explorer. Running Steam and Epic games light up their pinned icon.
- Live window previews when the pointer rests on an open app, with a close button.
- Widgets per environment: clock, Pomodoro, stopwatch, timer, countdown, progress of the day, week, month or year, processor and memory activity, and battery. They keep running across environments and notify when a Pomodoro or timer ends.
- Folder stacks: a pinned folder opens as a grid of its newest files, Downloads shows a badge with the new ones, and files can be dragged out. The program picker has a **Folder...** button.
- Optional: replace the Windows taskbar. The taskbar hides and the dock gains Start, the clock and quick settings; the `^` button shows the taskbar for its tray icons.
- Optional: windows follow the environment. The windows of the previous environment are hidden on a switch and come back when you return.
- A watchdog process restores the taskbar and every hidden window if WinDock ends unexpectedly, and the next start cleans up if both were killed.
- Reserve screen space or hide the dock automatically; the dock steps aside for full screen games and videos; dark and light glass; choice of monitor; icons shrink to fit when there are too many.
- Right-click menus on the dock and on each app; files and programs dropped on the dock are pinned to the current environment.
- A **Dock** tab in the settings window, widget choices on each environment card, and `WinDock.exe --exit` to close a running instance cleanly.

### Changed
- Switching environments no longer rewrites the Windows taskbar or restarts Explorer. The old behavior is still available with **Also swap the Windows taskbar pins**.

## [0.2.1] - 2026-10-07

### Fixed
- Opening a pinned Epic Games or Steam game could open a File Explorer window ("Documents") instead of the game, mostly when the launcher was not running yet. Game shortcuts now open their link through `rundll32 url.dll,FileProtocolHandler`, which hands it straight to the launcher. Microsoft Store apps keep using the shell namespace. Presets pinned with the old shortcuts refresh themselves the next time they are applied.

## [0.2.0] - 2026-10-07

### Added
- The window, picker and tray menu are available in English and Portuguese. English is the default; EN/PT buttons at the top of the window and a tray submenu switch it on the fly and the choice is remembered. Starter presets are named in English.
- One-file, self-contained `WinDock.exe` for each release (no .NET install needed), with install steps, shortcuts and usage in both READMEs.
- Presets that are a list of programs, each shown with its real icon, written to the native taskbar by restarting the shell.
- Program picker: Start Menu apps with search and multi-select, or any `.exe`/`.lnk`; reorder and remove from the card.
- "Usar taskbar atual" copies what is pinned now, Store apps included.
- The picker lists Microsoft Store apps (WhatsApp, Calculator...), installed Steam games and installed Epic Games games, with category tabs. Each pin gets its own icon and app id, so games and Store apps group with their running window.
- Schedule per preset (weekday toggles and time slots), with automatic switching.
- Configuration window: apply, delete, rename, edit the schedule, create presets.
- Backup of the original taskbar before the first change, with a "Restaurar original" button.
- Tray icon with preset list, auto-switch toggle, notifications and start with Windows.
- Global hotkeys: `Ctrl+Alt+W` opens the window, `Ctrl+Alt+1..9` applies a preset.
- Four starter presets: Trabalho, Jogos, Estudos, Fim de semana.
- "Sempre fixos": programs that are pinned first in every preset.
- Opening WinDock a second time brings the existing window forward.
- CI runs the tests on `windows-2022` (Windows 10 era) and `windows-latest`.

### Changed
- Switching a preset is smoother: the shell restart is covered by pictures of the taskbar. WinDock remembers how each preset looked, so a preset used before shows up in about half a second and fades into the real taskbar once Windows has finished. The restart itself dropped from about 6 seconds to about 2, and it is skipped when the pins are already the requested ones.

- You can switch presets again right away. The app is free as soon as the shell has restarted instead of after the fade, a request made meanwhile is queued, and the taskbar picture on screen retargets to the newest preset instead of starting over.

### Fixed
- Listing the same program twice in a preset (even through different shortcuts) left blank icons on the taskbar, because Windows drops the duplicate entry but keeps a placeholder. Duplicates are now removed when a preset loads, refused when adding, and skipped when applying.
- The program picker no longer lists shortcuts from the Startup folder.
- Applying a preset no longer opens a File Explorer window.
- A schedule change that arrived while a switch was already running was skipped for good.
- Apply now rolls back to the previous taskbar if writing the new one fails, and always starts the shell again.
- The same program can no longer be added twice to a preset.
- Deleting a preset asks for confirmation.
