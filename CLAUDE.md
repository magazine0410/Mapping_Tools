# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Mapping Tools is a collection of ~20 tools that edit osu! beatmaps in ways the in-game
editor cannot. It is a WPF desktop program, currently being split so that the logic runs
on Linux without WINE. Read [LINUX_PORT.md](LINUX_PORT.md) before you touch anything that
crosses the Windows boundary — it holds the porting plan and the current state.

## Projects

| Project | Target | Role |
|---|---|---|
| `Mapping_Tools.Core` | `net10.0` | Portable logic. Beatmap parser, math, hitsounds, tool algorithms. |
| `Mapping_Tools` | `net10.0-windows` | WPF host. Views, view models, Windows-only services. |
| `Mapping_Tools.Avalonia` | `net10.0` | Avalonia host, for Linux. Shell, shared parts, and Map Cleaner. |
| `Mapping_Tools.Core.Tests` | `net10.0` | Runs on any operating system. |
| `Mapping_Tools.Avalonia.Tests` | `net10.0` | Draws the Avalonia controls with no screen. Runs on Linux. |
| `Mapping_Tools_Tests` | `net10.0-windows` | Snapping Tools and global hotkeys only. |

`Mapping_Tools.Core` keeps the original `Mapping_Tools.Classes.*`,
`Mapping_Tools.Components.*` and `Mapping_Tools.Viewmodels` namespaces, so the folder
name and the namespace do not agree. Do not "fix" this — it keeps the `using` lines of
hundreds of files unchanged.

The view models are shared. 11 of them live in `Mapping_Tools.Core/Viewmodels/`, and
both hosts use the same copy. The other 10 are still in `Mapping_Tools/Viewmodels/`,
held there by WPF types or by a view they name. Section 3.1 of
[LINUX_PORT.md](LINUX_PORT.md) lists which, and why.

One trap: XAML `clr-namespace:Mapping_Tools.Viewmodels` now spans two assemblies. A
XAML file that names a moved view model needs `;assembly=Mapping_Tools.Core` on the
declaration. `MainWindow.xaml` needs both, so it declares two prefixes.

## Commands

Build the portable core, and run the tests. These work on Linux:

```bash
dotnet build Mapping_Tools.Core/Mapping_Tools.Core.csproj
```

```bash
dotnet test Mapping_Tools.Core.Tests/Mapping_Tools.Core.Tests.csproj
```

Run the Avalonia host, and its tests. Both work on Linux:

```bash
dotnet run --project Mapping_Tools.Avalonia/Mapping_Tools.Avalonia.csproj
```

```bash
dotnet test Mapping_Tools.Avalonia.Tests/Mapping_Tools.Avalonia.Tests.csproj
```

Run one test, or one test class:

```bash
dotnet test Mapping_Tools.Core.Tests/Mapping_Tools.Core.Tests.csproj --filter "FullyQualifiedName~BeatmapHelperTests"
```

Build everything, including the WPF project. On Linux you must add the flag:

```bash
dotnet build Mapping_Tools.sln -p:EnableWindowsTargeting=true
```

The WPF program **compiles** on Linux but **cannot start**: it needs the
`Microsoft.WindowsDesktop.App` runtime, which does not exist for Linux. Use the core
build and the core tests to check your work. Ask a person with Windows to test the
user interface.

## WINE smoke test — does the WPF program still start?

WINE is not the goal for this project, but it is the only way to find out from Linux if a
change stops the Windows program from starting. Use it after you change the platform
boundary, the startup path, or anything in `MainWindow`.

Publish a self-contained Windows build. This takes a few minutes the first time, and
seconds after that:

```bash
dotnet publish Mapping_Tools/Mapping_Tools.csproj -c Release -r win-x64 --self-contained true -p:EnableWindowsTargeting=true -o /tmp/mt-win
```

Start it in its own WINE prefix, so it cannot touch the real one. Keep the prefix under
your home directory. WINE refuses to build a prefix directly under `/tmp`, because you
do not own `/tmp`.

```bash
WINEPREFIX=~/.cache/mt-wine WINEDEBUG=-all wine "/tmp/mt-win/Mapping Tools.exe" > /tmp/mt-wine.log 2>&1 &
```

Wait about 20 seconds. Wait longer on the first run, because WINE must build the prefix
first. Then ask WINE if the main window exists:

```bash
WINEPREFIX=~/.cache/mt-wine wine winedbg --command "info wnd" 2>/dev/null | grep "Mapping Tools"
```

Stop it:

```bash
pkill -f "Mapping Tools.exe"; WINEPREFIX=~/.cache/mt-wine wineserver -k
```

### What a pass looks like

| Check | Pass |
|---|---|
| Window list | a line with `HwndWrapper[Mappi...]` and the caption `Mapping Tools` |
| `/tmp/mt-wine.log` | no managed exception, no stack trace |
| `.../AppData/Local/Mapping Tools/crash-log.txt` | not written |
| `.../AppData/Local/Mapping Tools/config.json` | written, and holds settings |

The last row is the useful one. `SettingsManager.LoadConfig()` runs **after**
`WpfPlatformServices.Register()` in the `MainWindow` constructor. So a `config.json`
proves that the constructor got past the platform registration without throwing.

A `DirectoryNotFoundException` for `osu!.<user>.cfg` in the log is normal. There is no
osu! install in the prefix, and the program handles it.

### What this test does not prove

- **WINE is not Windows.** A pass here is not proof that Windows is well. A crash here is
  strong evidence that something is broken.
- **A start is not a working program.** This test never opens a tool, a dialog, or a
  project. Quick Run, the tool views, and the Combo Colour Studio add-colour menu still
  need a person with Windows.
- **It says nothing about how the window looks.** On a Wayland session the window is not
  reachable with `xdotool`, so there is no screenshot. The window list is the only proof
  that a window exists.

## The platform boundary

The core must not use WPF, WinForms, `System.Drawing.Common`, the registry, P/Invoke, or
anything else that only Windows has. When the core needs the host, it asks through an
interface in `Mapping_Tools.Core/Platform/`:

| Interface | Replaces the old static call |
|---|---|
| `IDialogService` | `MessageBox.Show(...)` |
| `INotificationService` | `MainWindow.MessageQueue.Enqueue(...)` |
| `IAppPaths` | `MainWindow.AppDataPath` |
| `ICoreSettings` | `SettingsManager.Get...` |
| `IFileDialogService` | `IOHelper` dialogs, current beatmap |
| `IEditorReaderService` | `EditorReaderStuff` |

`CorePlatform` is a static locator that holds them. Every default does nothing, or reads
the file on disk, so tests and a command-line host need no setup.
`Mapping_Tools/Platform/WpfPlatformServices.Register()` puts the Windows versions in
place. The main window calls it first, before anything else touches the core.

**If you add a call to a dialog, a path, or the editor from core code, add it to an
interface. Do not reach back into the WPF project.**

`Mapping_Tools/Classes/ToolHelpers/WpfCoreBridge.cs` converts between the two sides:
`System.Drawing.Color` to WPF `Color`, `Editor_Reader.ControlPoint` to `TimingPoint`,
`AnchorState` to the `Anchor` control.

### Things that look portable but are not

- `System.Drawing.Color` **is** portable (System.Drawing.Primitives). `System.Drawing.Bitmap`
  is **not** (System.Drawing.Common is Windows only since .NET 7).
- The `NAudio` metapackage pulls in `NAudio.WinForms`, which blocks any build that is not
  Windows (`NETSDK1136`). The core uses `NAudio.Core` and `NAudio.Midi` instead. Never add
  plain `NAudio` to the core.
- `Overlay.NET` and `Process.NET` are .NET Framework only. They belong to Snapping Tools.
- Use `Path.Combine`, never a `\` in a path string. Linux treats `\` as part of the name.

## Porting a view from WPF to Avalonia

Read section 3.1 of [LINUX_PORT.md](LINUX_PORT.md) first. It holds the plan, the
measurements and what is left.

The Avalonia host uses the **Fluent** theme, not Material. `Styles/MaterialCompat.axaml`
keeps the Material Design key names alive over Fluent, so the colours and the styles of
a WPF view need no rewrite, only the changes in this table:

| WPF | Avalonia |
|---|---|
| `Style="{StaticResource X}"` | `Theme="{StaticResource X}"` — Avalonia has no keyed `Style` |
| `materialDesign:HintAssist.Hint="X"` | `PlaceholderText="X"` — **not** `Watermark`, which Avalonia 12 marks obsolete |
| `Visibility="{Binding ...}"` | `IsVisible="{Binding ...}"` — a boolean, so the visibility converters now give booleans |
| `materialDesign:PackIcon` | `icons:MaterialIcon`, same `Kind` values |
| `materialDesign:PopupBox` | `c:PopupBox`, same `ToggleContent` and `StaysOpen` |
| `materialDesign:DialogHost` | `c:DialogHost`, same `ShowDialog` and `CloseDialogCommand` |
| `Binding.ValidationRules` | `c:ValidatedTextBox`, and bind `Value`, not `Text` |
| `DependencyProperty.Register` | `AvaloniaProperty.Register<TOwner, T>` |
| `OnRender(DrawingContext)` | `Render(DrawingContext)` |
| `ToolTip="X"` | `ToolTip.Tip="X"` |

Two rules that cost time when they are missed:

- **Do not write your own `InitializeComponent`.** The Avalonia name generator writes
  one, and it fills the fields for every `x:Name`. A hand-written parameterless version
  wins the overload and leaves every one of those fields null.
- **A converter cannot report a bad value.** Throwing out of `ConvertBack` says nothing
  to the user and writes zero into the source. That is measured, and the table is in
  section 3.1. Use `ValidatedTextBox`.

**Measure a view model by compiling it alone.** Compiling several at once hides the
answer: when one file fails to bind its declarations, Roslyn never binds the method
bodies of the others, and their faults stay invisible. A count taken that way is a
floor, never a total.

Every new control needs a test in `Mapping_Tools.Avalonia.Tests`. The tests draw with
headless Skia, which is the only way on Linux to catch a missing resource key, a
template that does not build, or a binding that refuses a value. A build that succeeds
proves none of those.

## How tools are registered

There is no registry or list of tools. `Views/ViewCollection.cs` finds them by reflection
at run time. A tool is a class that:

1. extends `MappingTool` (or `SingleRunMappingTool`, which adds a `BackgroundWorker`,
   `Progress` and `CanRun`),
2. declares `public static readonly string ToolName` and `ToolDescription`,
3. lives in `Views/<ToolName>/`.

Optional interfaces and attributes change how the host treats it:

- `IQuickRun` — the tool can run from a global hotkey against the open editor.
- `SmartQuickRunUsageAttribute` — which selection states the hotkey applies to.
- `ISavable<T>` — the tool saves and loads a project. `ProjectManager` drives this, and
  the tool supplies `AutoSavePath` and `DefaultSaveFolder`.
- `IHaveExtraProjectMenuItems`, `HiddenTool`, `DontShowTitle` — host behaviour.

So a new tool needs no registration step, but it does need the exact static field names.

## Layer shape

- `Classes/BeatmapHelper/` — the `.osu` and `.osb` parser and writer. `Beatmap`,
  `HitObject`, `TimingPoint`, `Timing`, `Storyboard`. `BeatmapEditor` reads and writes
  a file. This is the heart of the program; almost everything else builds on it.
- `Classes/MathUtil/` — `Vector2`, matrices, Bézier and spline math, solvers.
- `Classes/Tools/<ToolName>/` — the algorithm of each tool, with no user interface.
- `Classes/HitsoundStuff/` — sample import, SoundFont, MIDI, Vorbis encoding.
  `PortableAudioReader` opens audio files on any operating system.
- `Viewmodels/<Tool>Vm.cs` — state bound to the view. Views hold the run logic; view
  models hold the settings that `ISavable` serializes.
- `Components/Graph/` — split on purpose. `GraphState`, `AnchorState`, `IGraphAnchor`,
  `AnchorMath` and the interpolators are in the core. The controls stay in WPF.

`GraphState` and `AnchorState` were WPF `Freezable` and are now plain classes. `Freeze()`
still means what it meant: after a freeze the object throws if you change it.

## Editor Reader

`EditorReader.dll` reads the memory of the running `osu!.exe`. It powers Quick Run,
"use the selected hit objects", auto-reload and current-beatmap detection. It cannot work
on Linux, because osu! stable itself runs in WINE there.

Core code reaches it only through `CorePlatform.EditorReader`. On Linux
`NullEditorReaderService` reads the `.osu` from disk and reports nothing selected, so the
tools still work when the user picks the file by hand.

## Deferred

Snapping Tools / Geometry Dashboard is postponed, not abandoned. Its 61 files stay in
`Mapping_Tools/Classes/Tools/SnappingTools/` and `Mapping_Tools/Views/SnappingTools/`.
Do not spend effort porting it. See section 4 of [LINUX_PORT.md](LINUX_PORT.md).

## Release

`.github/workflows/release.yml` runs on `windows-latest` when a `v*.*.*` tag is pushed.
It writes the version into both Inno Setup scripts and the `.csproj`, then builds the x86
and x64 installers. There is no Linux release job yet.
