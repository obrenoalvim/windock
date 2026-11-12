# TODO PERFORMANCE

> Last updated: 2026-11-12

## Context
- **Stack:** .NET 10 (SDK 10.0.400), WPF + Windows Forms (tray icon), `net10.0-windows10.0.19041.0`, CommunityToolkit.Mvvm 8.4.0. Released as a self-contained single-file `win-x64` exe.
- **Kind:** Desktop (always-running dock, started at login with `--tray`), plus two short-lived modes of the same exe: `--watchdog <pid>` and `--claude-hook`.
- **How to measure:** always a Release publish with the CI flags, never a Debug build.
  - Build: `dotnet publish src/WinDock -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o <dir>`
  - Startup: stop the running dock with `WinDock.exe --exit`, start `<dir>\WinDock.exe --tray`, time until a visible window titled `WinDock Dock` has left its off-screen start position (`Left > -30000`). One warm-up run discarded, then 5 runs, or 8 interleaved A/B pairs when the gain is small. Compare medians.
  - Memory: private bytes and working set of that process 10 s after start.
  - Hook mode: wall time of `WinDock.exe --claude-hook` with `{}` on stdin (no file is written), 10 runs.
  - Watchdog: private bytes of `WinDock.exe --watchdog <pid of a dummy process>` after 4 s.
  - Profile: `dotnet-trace collect -- <exe> --tray`, then `dotnet-trace convert --format Speedscope`. The watchdog child inherits the diagnostic port and hangs suspended, so kill it after the trace or the taskbar stays hidden.
  - Back up `%APPDATA%\WinDock` first: the app under test reads and writes the real settings.

## Baseline
| Metric | Value | Measured with | Date |
|---|---|---|---|
| Dock on screen after start | 1,986 to 2,058 ms (two sessions) | window poll, median of 5 | 2026-11-12 |
| CPU time until dock on screen | 1,703 to 1,719 ms | process CPU time, median of 5 | 2026-11-12 |
| Private memory 10 s after start | 228 to 229 MB | process counters, median of 5 | 2026-11-12 |
| Working set 10 s after start | 384 to 386 MB | process counters, median of 5 | 2026-11-12 |
| Dock after 3 h of use | 300 MB private, 427 MB working set, 7.7 MB managed heap | process counters, `dotnet-gcdump report` | 2026-11-12 |
| Idle CPU | 47 ms per 30 s (0.16% of one core) | process CPU time delta | 2026-11-12 |
| Watchdog process | 41.6 MB private, 92 MB working set | process counters, median of 5 | 2026-11-12 |
| `--claude-hook` run | 165 to 175 ms wall | stopwatch, median of 10 | 2026-11-12 |
| Release exe size | 72.7 MB | file size | 2026-11-12 |

## Applied
| Change | Where | Before → After | Source |
|---|---|---|---|
| Settings window is created on first open instead of at startup; the global hotkeys moved to a message-only window owned by `App` | `App.xaml.cs`, `Views/ConfigWindow.xaml.cs`, `Services/TrayService.cs` | Dock on screen 1,986 → 1,814 ms; CPU until then 1,703 → 1,484 ms; private memory 229.0 → 220.1 MB (medians of 5, same session) | https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/application-startup-time |
| Pin icons of the settings list load on first read instead of in the constructor | `ViewModels/PinViewModel.cs` | Dock on screen 1,816 → 1,723 ms; CPU until then 1,500 → 1,453 ms (8 interleaved pairs, faster in 6, ranges 1,769 to 1,886 vs 1,711 to 1,814) | https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/application-startup-time |

Side effects to know: the first time the settings window opens it now pays its own construction (about 0.25 to 0.5 s, once). The icon style previews are built at that first open, when the active environment is already known, so they can include its icons from the start.

## Tried, no gain
- Nothing yet.

## Pending Changes

### Stop compressing the single-file exe
- **Layer:** Startup / Memory
- **Source:** https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview (compression "comes with a performance cost", assemblies are "decompressed into memory" on every start)
- **What:** Publish with `-p:EnableCompressionInSingleFile=false`. To keep the download small, ship the exe inside a zip or an installer instead of the bare exe.
- **Where:** `.github/workflows/ci.yml` (release job), the local publish command, and `Services/Updates.cs` if the release asset name changes.
- **Expected gain (measured on the baseline build, same machine):** dock on screen 2,058 → 1,834 ms; private memory 228.2 → 139.6 MB; working set 383.8 → 218.3 MB; watchdog 41.6 → 8.4 MB private; `--claude-hook` 165 → 67 ms.
- **How to measure:** publish both variants, run the startup, memory, hook and watchdog procedures above.
- **Risk:** the exe grows from 72.7 MB to 173.2 MB. A bare exe that size is a slower download; a zip changes how people install and update.
- **Effort:** Low

### Taskbar hide blocks the first frame of the dock
- **Layer:** Startup
- **Source:** startup trace of 2026-11-12
- **What:** `ShellGuard.Enforce()` runs on the UI thread while the first dock window loads and waits on Explorer: about 255 ms in `ABM_GETSTATE` and 198 ms in `ShowWindow(SW_HIDE)` on the taskbars (455 ms of a 2,255 ms traced startup). Moving it off the UI thread, or using `ShowWindowAsync`, would put the dock on screen sooner.
- **Where:** `Services/ShellGuard.cs` (`HideTaskbar`, `Enforce`), `Views/DockWindow.xaml.cs` (`ApplyOptions`, `ApplyShell`)
- **Expected gain:** up to about 450 ms sooner on screen (traced time, the real gain is smaller).
- **How to measure:** startup procedure above.
- **Risk:** the dock would show before the taskbar is gone, so it can overlap the taskbar and jump when the work area changes. `Peek` could also race with a hide that is still queued.
- **Effort:** Medium

### Tray menu built at startup
- **Layer:** Startup
- **Source:** startup trace of 2026-11-12
- **What:** `new ContextMenuStrip()` costs about 85 ms at startup (75 ms resolving `ToolStripManager.DefaultFont`). Create it on the first right click.
- **Where:** `Services/TrayService.cs`
- **Expected gain:** under 85 ms; likely inside the noise of the startup measurement.
- **How to measure:** startup procedure above with 8 interleaved pairs.
- **Risk:** `NotifyIcon` only opens a menu that is already assigned when the click arrives, so the menu has to be attached on mouse down.
- **Effort:** Low

### Media session and running-app icons during the first rebuild
- **Layer:** Startup
- **Source:** startup trace of 2026-11-12
- **What:** Inside `DockViewModel.Rebuild()` at startup: `MediaSessions.Start()` plus its static constructor take about 117 ms (WinRT activation) and `RunningTile` takes about 108 ms loading icons of running apps. The media session could start after the dock is on screen.
- **Where:** `ViewModels/DockViewModel.cs`, `ViewModels/Widgets.cs` (`MusicWidget`), `Services/MediaSessions.cs`
- **Expected gain:** up to about 100 ms sooner on screen.
- **How to measure:** startup procedure above.
- **Risk:** the music tile would appear a moment after the rest of the dock.
- **Effort:** Medium

### ReadyToRun for WinDock.dll
- **Layer:** Startup
- **Source:** https://github.com/dotnet/runtime/issues/101866
- **What:** `-p:PublishReadyToRun=true` precompiles the app's own code, which is JIT compiled on every start today.
- **Where:** `.github/workflows/ci.yml` or `WinDock.csproj`
- **Expected gain:** not measured. The linked issue reports a startup regression when ReadyToRun is combined with single-file compression, so measure it only together with the compression change above.
- **How to measure:** startup procedure above, with and without the flag, compression off in both.
- **Risk:** larger exe.
- **Effort:** Low

### Native memory of the long-running dock
- **Layer:** Memory
- **Source:** memory map of the running process, 2026-11-12
- **What:** After 3 h the dock holds 268 MB of private memory with only 7.7 MB of managed heap. About 86 MB is executable private memory (the decompressed assemblies, covered by the compression change), about 95 MB is write-combined memory from the GPU driver (two transparent dock windows, both rendered by hardware), about 86 MB is native heaps. Private memory also grew from 228 MB at start to 300 MB.
- **Where:** `Views/DockWindow.xaml` (`AllowsTransparency`), whatever allocates during the day
- **Expected gain:** not measured.
- **How to measure:** sample private bytes of the running dock every hour for a day to see whether the growth levels off; compare the memory map at start and after hours.
- **Risk:** software rendering would cut GPU memory but cost CPU during the lens animation.
- **Effort:** High
