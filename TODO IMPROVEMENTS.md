# TODO IMPROVEMENTS

> Last updated: 2026-10-12

## Pending Changes

### Dock on the left or right edge
- **Category:** Feature
- **Flagged by:** PO, UX
- **What:** The dock only sits at the bottom. `LensMath` is axis-agnostic, but `LensPanel`, the appbar edge, auto-hide and the wide widget tiles assume a horizontal bar.
- **Where:** `src/WinDock/Views/LensPanel.cs`, `DockWindow.xaml(.cs)`
- **Why:** Doca offers bottom, left and right; wide screens benefit from a side dock.
- **Risk:** Widgets need a square layout when vertical.
- **Effort:** Medium

### Real blur behind the glass
- **Category:** UI-UX
- **Flagged by:** UI
- **What:** The bar is a translucent gradient, not acrylic. Acrylic via `SetWindowCompositionAttribute` blurs the whole window rectangle, including the headroom the lens uses above the bar, so it needs a second window sized to the bar.
- **Where:** `src/WinDock/Views/DockWindow.xaml.cs`
- **Why:** Closer to the Mac and to the "barra de vidro" look of the references.
- **Risk:** Undocumented API; must follow the bar width while the lens animates.
- **Effort:** Medium

### Now playing and weather widgets
- **Category:** Feature
- **Flagged by:** PO
- **What:** Music controls need `GlobalSystemMediaTransportControlsSessionManager` (WinRT), which means targeting `net10.0-windows10.0.19041.0` and shipping the Windows SDK projection. Weather needs a location and a free API such as Open-Meteo.
- **Where:** `src/WinDock/ViewModels/Widgets.cs`, `WinDock.csproj`
- **Why:** PrimoDock lists both among its most visible widgets.
- **Risk:** Larger single-file exe; network access for weather.
- **Effort:** Medium

### Tray icons inside the dock
- **Category:** Feature
- **Flagged by:** UX
- **What:** With the taskbar replaced, tray icons are reached through the `^` button, which shows the real taskbar for a moment. Re-hosting them needs the notification area UI Automation tree (Windows 11) or the toolbar of `TrayNotifyWnd` (Windows 10).
- **Where:** `src/WinDock/ViewModels/DockTiles.cs` (`SystemTile`)
- **Why:** One less trip to the hidden taskbar.
- **Risk:** Fragile across Windows builds.
- **Effort:** High

### Drag icons inside the dock to reorder or remove
- **Category:** UI-UX
- **Flagged by:** UX
- **What:** Reordering is done in the settings window. Dragging a dock icon sideways should move it, and dragging it out should unpin it.
- **Where:** `src/WinDock/Views/DockWindow.xaml.cs`, `LensPanel.cs`
- **Why:** It is the gesture people try first on a dock.
- **Risk:** Must not fight the lens and the click-to-launch.
- **Effort:** Medium

### Settings folder override for tests and harnesses
- **Category:** Test
- **Flagged by:** QA
- **What:** `Settings.Dir` is fixed to `%APPDATA%\WinDock`, so anything that saves settings (including `FolderStack` on first sight of a folder) writes the real profile. An environment variable such as `WINDOCK_HOME` would let tests and scratch tools use a temp folder.
- **Where:** `src/WinDock/Models/Settings.cs`
- **Why:** A test harness once replaced a real settings file with defaults.
- **Risk:** Low.
- **Effort:** Low

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
- **What:** Only matters for the optional taskbar mode now (the dock switches without touching Explorer). Apply in that mode restarts `explorer.exe`, which closes open File Explorer windows and blinks the desktop icons. The `IPinnedList3` COM interface (`CLSID {90AA3A4E-1CBA-4233-B8BB-535773D48449}`) can pin and unpin live, but Windows 11 24H2 and later accept `Modify` without error and pin nothing (checked on build 26200; unpinning may still work). A hybrid is possible: unpin live and restart only when pins must be added, on Windows 10 and 11 before 24H2 pin live as well.
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
