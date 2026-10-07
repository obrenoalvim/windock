# TODO IMPROVEMENTS

> Last updated: 2026-10-07

## Pending Changes

### Verify on a real Windows 10 machine
- **Category:** Test
- **Flagged by:** QA, Engenheiro de Software
- **What:** The synthesized pin entries (Favorites, FavoritesResolve, .lnk files) were validated against the real shell on Windows 11 only. Windows 10 uses the same registry key and folder, and CI runs the unit tests on `windows-2022`, but nobody has applied a preset on Windows 10 yet.
- **Where:** `src/WinDock/Services/TaskbarBlob.cs`, `PinBuilder.cs`
- **Why:** Confirms the Windows 10 claim in the README before a release.
- **Risk:** If Windows 10 rejects an entry it silently drops that pin. "Restaurar original" recovers the previous taskbar.
- **Effort:** Low

### Pin without restarting explorer
- **Category:** Feature
- **Flagged by:** UX, Dev Sr
- **What:** Apply currently restarts `explorer.exe`, which closes open File Explorer windows and blinks the desktop icons. The `IPinnedList3` COM interface (`CLSID {90AA3A4E-1CBA-4233-B8BB-535773D48449}`) can pin and unpin live, but Windows 11 24H2 and later accept `Modify` without error and pin nothing (checked on build 26200; unpinning may still work). A hybrid is possible: unpin live and restart only when pins must be added, on Windows 10 and 11 before 24H2 pin live as well.
- **Where:** `src/WinDock/Services/TaskbarService.cs`
- **Why:** Switching would be instant and would not touch open windows.
- **Risk:** Undocumented interface that changes between Windows builds.
- **Effort:** High

### Drag and drop to reorder pins
- **Category:** UI-UX
- **Flagged by:** UX, UI
- **What:** Reordering is a right-click menu (move left/right). Dragging the icons inside the card would be the natural gesture.
- **Where:** `src/WinDock/Views/ConfigWindow.xaml`, `ViewModels/PresetViewModel.cs`
- **Why:** The icon strip already looks like the taskbar, so people will try to drag.
- **Risk:** WPF drag and drop inside an `ItemsControl` needs adorner and insertion-index code.
- **Effort:** Medium

### More game launchers
- **Category:** Feature
- **Flagged by:** PO
- **What:** Steam and Epic Games are read from their manifests. GOG Galaxy, EA app, Ubisoft Connect and Battle.net could be added the same way (each has its own install registry or manifest).
- **Where:** `src/WinDock/Services/GameLibraries.cs`, `Catalog.cs`
- **Why:** Same picker experience for every game library.
- **Risk:** Launch URIs differ per launcher and need checking against a real install.
- **Effort:** Medium

### Tests write to the real %APPDATA%
- **Category:** Test
- **Flagged by:** QA
- **What:** `Importing_a_built_snapshot_gives_the_same_programs_back` writes a shortcut into `%APPDATA%\WinDock\links`. Make the library directory injectable so tests can use a temp folder.
- **Where:** `src/WinDock/Services/PinBuilder.cs`, `tests/WinDock.Tests/BlobTests.cs`
- **Why:** Tests should not leave files in a user profile.
- **Risk:** Touches a public static; low.
- **Effort:** Low

### Code signing for the release zip
- **Category:** Dependency
- **Flagged by:** Engenheiro de Software
- **What:** The unsigned exe will trigger SmartScreen, and an app that kills and restarts `explorer.exe` is the kind of thing antivirus flags.
- **Why:** Trust on first run.
- **Risk:** Needs a certificate, which costs money.
- **Effort:** Medium
