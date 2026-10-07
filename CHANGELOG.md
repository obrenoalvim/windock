# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
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
