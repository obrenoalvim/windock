# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- The dock shows on every screen by default, or on the one you pick in the **Dock** tab, and follows monitors as they are plugged in or out.
- When the Windows taskbar is out of sight (replaced, or set to hide automatically) the dock shows the Windows basics: Start, the internet status (Wi-Fi name and signal, Ethernet, no internet, offline), the volume, the battery with time left, the clock and calendar, the tray icons and show desktop.
- Sound control: scroll on the speaker to change the volume, middle-click to mute, click for a panel with the output device, a slider, the volume mixer and the sound settings.
- Claude usage widget: the 5-hour session and the 7-day week of your Claude plan, red from 90%, with a panel for the reset times, the weekly pace and a refresh, and a notice at 80% and 95%. It uses the sign-in Claude Code keeps on the PC and never renews or stores it.
- Music widget: what plays in Spotify, the browser or any player, with the cover, play and pause on the tile, and a panel with every player side by side (Spotify, each browser tab, any app), each with previous, play and next. The tile hides while nothing plays.
- Seven new dock themes next to Dark and Light: Midnight, Neon, Canarinho, Aqua, Sunset, Paper and Floating, each with its own border, corners and indicator color, picked from a list with a preview of each in the **Dock** tab.
- Weather widget: temperature and condition on the tile (blue by day, indigo at night), and a panel with feels like, high and low and the next hours. Type the city on the environment card or leave it empty to use the Windows location. Data from Open-Meteo, free and without an account.
- Network speed widget, note widget, and the Pomodoro counts the cycles done today.
- Start with Windows is on for new installs and sits in the **Dock** tab and in the dock menu too.
- Unread badges on dock icons, from the counts apps put on the Windows taskbar (WhatsApp, Teams, Mail) and from window titles that start with a count.
- The `^` button lists the apps with a tray icon, to open them or quit them without bringing back the Windows taskbar.
- Microphone: a button next to the sound to mute it, and a microphone widget.
- The sound panel lists every output device and switches the Windows default with one click, on the screen it was opened on.
- Compact widgets, on by default: each widget is a square the size of an icon; Claude shows two rings (session outside, week inside) and the note keeps its wide tile.
- The weather refreshes every 9 seconds while it is on the dock, just under the free Open-Meteo limit of 10,000 calls a day, and waits an hour if Open-Meteo says the limit was reached.
- Each screen can have its own dock, with the windows open on it and an environment of its own.

### Changed
- The tray, network, sound, microphone, battery and the clock sit together at the right end of the dock; network, sound, microphone and battery share one pill. Start stays at the left end.
- Shortcuts that start another exe (Discord) light up the pinned icon instead of adding a second one, and programs split over several exes with the same icon (Google Play Games) take one slot.
- The Claude widget asks again as soon as Claude Code renews its sign-in.
- The dock text uses the Windows font (Segoe UI Variable), a little larger in the clock, the date and the note.
- Widgets that are not on the dock (other environments, or the dock turned off) stop refreshing: no Claude or weather requests and no processor, memory or network sampling for them. A running Pomodoro or timer still rings on time.
- Icons that only come in small sizes (many games and older programs) fill their tile like the others, without the faint square frame Windows draws around them.
- The clock widget steps aside while the dock already shows the Windows clock.
- The environment dots left the dock; switch with the mouse wheel, `Ctrl+Alt+1..9` or the dock menu.
- The minutes of the timer and the period of the progress widget are remembered.
- Panels (folders, sound, Claude, music, note) share one look and close on Esc or a click outside.
- Needs Windows 10 version 2004 or newer, for the media controls.

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
