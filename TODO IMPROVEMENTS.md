# TODO IMPROVEMENTS

> Last updated: 2026-10-13

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

### More widgets from PrimoDock
- **Category:** Feature
- **Flagged by:** PO, UX
- **Source:** https://dock.oprimo.dev/
- **What:** Color picker (pick any pixel, copy the hex, keep the last colors), world clock (another city, analog face), calendar with the next events (needs Outlook or Google access), shelf (drop files mid-drag and pick them up later), favorite sites, stopwatch laps, clipboard history with pins, latest screenshots to drag out, unit converter, water goal, GitHub PRs waiting for review.
- **Where:** `src/WinDock/ViewModels/Widgets.cs`, `Views/DockWindow.xaml`
- **Why:** PrimoDock lists 31 widgets; WinDock has 12.
- **Risk:** Calendar and color picker need extra permissions or a full-screen capture overlay.
- **Effort:** Medium each

### Tray icons inside the dock
- **Category:** Feature
- **Flagged by:** UX
- **What:** The `^` panel lists the tray apps (open or quit) from `NotifyIconSettings`, but not their real icons, menus or state (Wi-Fi of a VPN, a sync spinner). Re-hosting them needs the notification area UI Automation tree (Windows 11) or the toolbar of `TrayNotifyWnd` (Windows 10).
- **Where:** `src/WinDock/ViewModels/DockTiles.cs` (`SystemTile`)
- **Why:** One less trip to the hidden taskbar.
- **Risk:** Fragile across Windows builds.
- **Effort:** High

### Feedback while an app opens
- **Category:** UI-UX
- **Flagged by:** UX, UI, Dev Sr
- **Source:** https://dock.oprimo.dev/ (opening animation is a theme option), macOS Dock bounce
- **What:** After a click the icon does nothing until the window shows, which takes seconds for games and Electron apps. Pulse the icon (or blink its dot) from the click until a matching window appears, at most ~10 s, and ignore a second click on the same tile meanwhile.
- **Where:** `AppTile.Launch` in `ViewModels/DockTiles.cs`, `OnTileUp` in `Views/DockWindow.xaml.cs`, `LensPanel`
- **Why:** Without feedback people click again and get two instances (Steam, Discord, Play Games). The most felt gap in daily use.
- **Risk:** Apps that open no window (tray only) must stop pulsing on the timeout.
- **Effort:** Low

### Recent files in the app menu
- **Category:** Feature
- **Flagged by:** UX
- **What:** Right-click an app to see its recent files (like the taskbar jump list) and open one. Start with `%APPDATA%\Microsoft\Windows\Recent` shortcuts filtered by the extensions the app opens; real jump lists live in `AutomaticDestinations` (undocumented format).
- **Where:** `AppMenu` in `Views/DockWindow.xaml.cs`
- **Why:** The taskbar jump list is the other thing people miss after replacing it.
- **Risk:** The Recent folder may be off by privacy settings; keep it read only.
- **Effort:** Medium

### Screen readers and keyboard on the dock
- **Category:** UI-UX
- **Flagged by:** UI, UX, QA
- **Source:** https://learn.microsoft.com/en-us/archive/blogs/jaimer/wpf-discussion-090522 , https://learn.microsoft.com/en-us/archive/blogs/gautamg/how-to-get-automation-working-properly-on-data-bound-wpf-list-or-combo-box
- **What:** The dock is a plain `ItemsControl` of `Grid` tiles: Narrator sees nothing (no automation peer, no names) and the dock cannot be reached with the keyboard. Give the strip an automation peer (or a `ListBox` restyled flat) with each tile named (app name, widget tip), and a key to focus the dock and walk it with the arrows.
- **Where:** `Views/DockWindow.xaml` (`Strip`), `ViewModels/DockTiles.cs`
- **Why:** The taskbar it replaces is fully accessible; replacing it should not lock out screen reader users.
- **Risk:** The window is `WS_EX_NOACTIVATE`; keyboard focus needs it to take activation while walking, and give it back after.
- **Effort:** Medium

### The Win+n hook can be dropped silently
- **Category:** Bug
- **Flagged by:** Dev Sr
- **What:** `WinKeys` hooks the keyboard on the UI thread. If that thread stalls longer than `LowLevelHooksTimeout` (a big folder stack, a slow COM call), Windows 10+ removes the hook without telling, and Win+1..9 go back to the hidden taskbar until WinDock restarts. Run the hook on a thread of its own with its own message loop, and post the action to the dispatcher.
- **Where:** `Services/WinKeys.cs`
- **Why:** A shortcut that stops working at random reads as a broken app.
- **Risk:** Low; the callback already only decides and hands off.
- **Effort:** Low

### An empty environment says what to do
- **Category:** UI-UX
- **Flagged by:** UX, PM
- **Source:** https://nngroup.com/articles/empty-state-interface-design
- **What:** An environment with nothing pinned and no widgets shows a bare strip of glass. Show one ghost tile ("+" — "Drop apps here or open the settings") that opens the settings on click and goes away with the first pin.
- **Where:** `DockViewModel.Rebuild`, a template in `DockWindow.xaml`
- **Why:** A new environment looks broken instead of empty.
- **Risk:** Low.
- **Effort:** Low

### Tell when something went wrong
- **Category:** UX
- **Flagged by:** Dev Sr, UX, Engenheiro de Software
- **What:** `App.OnCrash` writes `error.log` and swallows every UI exception, so a failing feature just does nothing. Show one notice per session ("Something went wrong — open the log") with a link to the log, and add "Open log folder" to the tray menu.
- **Where:** `App.xaml.cs`, `Services/TrayService.cs`
- **Why:** Silent failures cannot be reported; the user cannot tell a bug from a missing feature.
- **Risk:** A loop of errors must not flood notices.
- **Effort:** Low

### Drop files on a folder stack to move them there
- **Category:** Feature
- **Flagged by:** UX, PO
- **What:** Dropping on an app opens the files with it, dropping on a pinned folder still pins the files. Moving (Shift: copying) them into the folder, like File Explorer, is what people expect from a folder.
- **Where:** `OnDragOver` / `OnDrop` in `Views/DockWindow.xaml.cs`
- **Why:** Consistent drop targets: app opens, folder receives, glass pins.
- **Risk:** Moving files is destructive if aimed wrong; show the name label ("Move to Downloads") and use the shell's own copy (`IFileOperation`) for undo and conflicts.
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
