# Linux Native Port — Work Assessment

Goal: run Mapping Tools natively on Linux, with identical function, and without WINE.

Date of assessment: 2026-08-19
Assessed version: 1.12.30 (`net10.0-windows`, WPF and WinForms)

## Status

**The core split is done.** See "Current state" below. The rest of this document is the
plan, and it still holds. Effort figures for finished items now say DONE.

## How this assessment was made

A probe project compiled the core against plain `net10.0`, with no Windows target
platform. The compiler found the dependencies. The lists below come from those
compiler errors, not from a text search.

### Correction to the first measurement

The first probe said that 223 of 255 files were clean. **That number was too good.**
Roslyn binds declarations first. If declarations have errors, it does not bind the
method bodies at all. So the first probe found the Windows types in fields, properties
and signatures, but it never looked inside the methods. Calls like `MessageBox.Show(...)`
were invisible.

The true figures came out only after the declaration errors were fixed:

| Measurement | First probe | True |
|---|---|---|
| Files with Windows dependencies | 32 | 49 |
| Kind found | Declarations only | Declarations and method bodies |

The conclusion did not change. The extra 17 files failed on `MessageBox`, `MainWindow`,
`SettingsManager` and `IOHelper` — the layer violations of section 2.2, which are cheap
to fix. **Treat any "clean file" count from a failing build as a floor, not a total.**

---

## Current state

Three build paths work. None needs WINE for the core.

```bash
dotnet build Mapping_Tools.Core/Mapping_Tools.Core.csproj
```
```bash
dotnet test Mapping_Tools.Core.Tests/Mapping_Tools.Core.Tests.csproj
```

| Path | Result |
|---|---|
| `Mapping_Tools.Core` on Linux, target `net10.0` | Builds. 0 errors. |
| `Mapping_Tools.Core.Tests` on Linux | 25 tests, all pass. |
| Full solution with `-p:EnableWindowsTargeting=true` | Builds. 0 errors. |

### The projects

| Project | Target | Contents |
|---|---|---|
| `Mapping_Tools.Core` | `net10.0` | 219 files, 47,977 lines. Portable. |
| `Mapping_Tools` | `net10.0-windows` | 222 files, 26,265 lines. WPF and Windows only. |
| `Mapping_Tools.Core.Tests` | `net10.0` | 12 files. Runs anywhere. |
| `Mapping_Tools_Tests` | `net10.0-windows` | 3 files. Snapping Tools and hotkeys. |

**65% of the C# is now portable and proven to build on Linux.**

### What stayed in the Windows project

78 files in `Mapping_Tools/Classes/`. 61 of them are Snapping Tools (group 4). The other
17 are:

```
GenericExtensions.cs                     bitmap bridge (gdi32)
SystemTools/ActionHotkey.cs              WPF input enums
SystemTools/Hotkey.cs                    WPF input enums
SystemTools/IHaveExtraProjectMenuItems.cs  returns WPF MenuItem
SystemTools/IOHelper.cs                  Win32 file dialogs, osu! memory
SystemTools/ListenerManager.cs           global hotkeys, user32
SystemTools/Settings.cs                  holds WPF hotkeys and window bounds
SystemTools/SettingsManager.cs           registry, window geometry
SystemTools/ShowSelectedInFileExplorer.cs  shell32
ToolHelpers/EditorReaderStuff.cs         reads the osu! process memory
ToolHelpers/WpfCoreBridge.cs             new: joins core to WPF
Tools/ComboColourStudio/SpecialColourDragAndDropListBox.cs   WPF ListBox
Tools/PatternGallery/OsuPatternDetailsVm.cs   bound to a WPF dialog
Tools/PatternGallery/PatternCodeImportVm.cs   bound to a WPF dialog
Tools/PatternGallery/PatternFileImportVm.cs   bound to a WPF dialog
Tools/SlideratorStuff/SliderPicturator.cs     System.Drawing.Bitmap
Tools/TumourGenerating/Domain/TumourTemplateToIconConverter.cs   WPF converter
```

### The platform boundary

`Mapping_Tools.Core/Platform/` holds the interfaces that the core asks the host for.
`CorePlatform` is a service locator with defaults that do nothing, so tests and a
command-line host need no setup.

| Interface | Replaces | Windows host | Linux default |
|---|---|---|---|
| `IDialogService` | `MessageBox.Show` | `MessageBox` | does nothing |
| `INotificationService` | `MainWindow.MessageQueue` | snackbar | does nothing |
| `IAppPaths` | `MainWindow.AppDataPath` | main window | local app data |
| `ICoreSettings` | `SettingsManager` | settings file | safe defaults |
| `IFileDialogService` | `IOHelper` dialogs | Win32 dialogs | returns null |
| `IEditorReaderService` | `EditorReaderStuff` | reads osu! memory | reads the file on disk |

`Mapping_Tools/Platform/WpfPlatformServices.Register()` supplies the Windows versions.
The main window calls it first, before anything else touches the core.

**Behaviour on Windows does not change.** Every dialog, path and editor call still goes
to the same code as before. Only the route changed.

### Other changes made

- `NAudio` metapackage replaced by `NAudio.Core` and `NAudio.Midi` in the core.
  The metapackage pulls in `NAudio.WinForms`, which blocks any build that is not Windows.
  The WPF project still uses the metapackage, for `WasapiOut`.
- New `PortableAudioReader` replaces `MediaFoundationReader`. Ogg uses NAudio.Vorbis,
  MP3 uses NLayer, WAV and AIFF use NAudio. (Section 2.5: DONE.)
- `System.Windows.Media.Color` replaced by `System.Drawing.Color` in `ComboColour`,
  `SpecialColour` and `ComboColourProject`. `System.Drawing.Color` is in
  System.Drawing.Primitives, which is portable. Only `System.Drawing.Common` is not.
  `WpfCoreBridge` converts at the boundary.
- `GraphState` and `AnchorState` are now plain classes, not WPF `Freezable`.
  `Freeze()` and `CanFreeze` keep their meaning: a frozen object throws if you change it.
  (Section 2.4: DONE.)
- The graph math of `AnchorCollection` moved to `AnchorMath` in the core, 499 lines.
  The WPF collection keeps its static methods, which now call `AnchorMath`.
- `CommandImplementation` moved to the core. `CommandRequery` connects it to WPF
  `CommandManager`, so requery behaviour on Windows is unchanged.
- `ColourPoint` no longer builds a WPF context menu. The host sets `ShowAddColourMenu`.
- `LayerImportArgs` returns `bool` instead of WPF `Visibility`. No XAML used these.
- Internal types stay visible to the old callers through `InternalsVisibleTo`.
- Three XAML files now name the core assembly in their `clr-namespace` declarations.

### A path fault that only Linux shows

Five of the 25 tests failed on the first Linux run. The test code wrote
`"Resources\\EmptyTestMap.osu"`. On Windows the backslash is a separator. On Linux it
is part of the file name. `Path.Combine` fixed it, and all 25 tests then passed.

This is a **path separator** fault, not the case-sensitivity fault of section 2.8.
They are different, and both are real. This one is easy: the compiler cannot see it,
but the first test run does.

**Section 2.8 is still untested.** Nothing here read a real beatmap folder with real
hitsound files, so the case-sensitivity hazard has not been tried yet. It stays open,
and it stays the most likely source of quiet faults.

### What is not done

- The Avalonia user interface (3.1). Nothing started.
- EditorReader on Linux (3.2). The interface is there, and `NullEditorReaderService`
  reads the file on disk instead. No Linux reader exists.
- Global hotkeys (3.3), image processing (3.4), the updater (3.6), the release job (3.7).
- Case-insensitive path lookup (2.8).
- Audio output on Linux (2.6). The core has no output device.
- Snapping Tools (group 4). Still deferred, still 61 files in the Windows project.

---

## 1. No changes necessary

These parts are already portable. They now build on Linux, and the tests pass.

### Core logic — 219 files, now in `Mapping_Tools.Core`

- `Classes/BeatmapHelper/` — the `.osu` and `.osb` parser and writer, hit objects,
  timing, storyboards. This is the heart of the program.
- `Classes/MathUtil/` — vectors, matrices, Bézier and spline math, solvers.
  (`Vector2.cs` is one exception. See section 2.)
- `Classes/Tools/` — the algorithms of almost every tool: Map Cleaner, Slider Merger,
  Slider Completionator, Property Transformer, Timing Helper, Rhythm Guide,
  Metadata Manager, Timing Copier, Mapset Merger, Tumour Generator.
- `Classes/HitsoundStuff/` — hitsound layers, sample schema, SoundFont (SF2) reading,
  MIDI export, Vorbis encoding, the audio effects.
- `Classes/ToolHelpers/`, `Classes/JsonConverters/`, `Classes/Exceptions/`.
- `Components/Graph/Interpolation/` — all interpolator math.

### Libraries that are already cross-platform

- `Newtonsoft.Json`
- `NAudio.Core`, `NAudio.Midi`, `NAudio.Vorbis`, `NVorbis`
- `OggVorbisEncoder`

### Files and paths

Almost all path code uses `Path.Combine`. There are very few hard-coded Windows paths.

---

## 2. Some work necessary

These parts fail, but the cause is a name or a type, not an algorithm. Avalonia UI
supplies almost all of the replacements under nearly the same names.

### 2.1 One package reference — DONE

The `NAudio` 2.0.0 metapackage pulls in `NAudio.WinForms`. That drags in the whole
Windows Desktop framework reference and stops the build (`NETSDK1136`).

Fix: use `NAudio.Core` and `NAudio.Midi` instead of `NAudio`.

### 2.2 Layer violations — DONE

The logic layer reaches up into the user interface. This is the largest single group
of errors, and it is not a Windows problem.

| Symbol | Call sites | Files |
|---|---|---|
| `MessageBox.` | 17 | 8 |
| `MainWindow.` (mostly `MainWindow.AppDataPath`) | 34 | 10 |

Fix: add two interfaces, one for dialogs and one for application paths. Inject them.
Do this work even if the port stops. It improves the program on Windows too.

### 2.3 WPF types used as plain data — about 3 to 5 days

The logic uses these WPF types only to hold values:

`Color`, `Colors`, `Point`, `Rect`, `Vector`, `Pen`, `Brush`, `SolidColorBrush`,
`DashStyle`, `DrawingContext`, `Key`, `ModifierKeys`, `IValueConverter`,
`DispatcherTimer`, `TextWrapping`, `Visibility`.

Avalonia has an equivalent for each one. `Visibility` is the only true difference.
Avalonia uses a `bool IsVisible` property instead.

Files affected: `Classes/BeatmapHelper/ComboColour.cs`,
`Classes/BeatmapHelper/SpecialColour.cs`, `Classes/MathUtil/Vector2.cs`,
`Classes/SystemTools/Hotkey.cs`, `Classes/SystemTools/Settings.cs`,
`Classes/SystemTools/IHaveExtraProjectMenuItems.cs`,
`Classes/HitsoundStuff/LayerImportArgs.cs`.

### 2.4 Data classes that inherit WPF `Freezable` — DONE

`Components/Graph/GraphState.cs` and `AnchorState` are pure data holders. They inherit
`Freezable` and use `DependencyProperty` for no structural reason.

The probe replaced both with plain classes in about 15 lines. No other code noticed
the change. The Tumour Generator and the Sliderator use these classes.

Files affected: `Classes/Tools/TumourGenerating/Options/TumourLayer.cs`,
`Classes/Tools/TumourGenerating/Options/ITumourLayer.cs`,
`Classes/Tools/SlideratorStuff/GraphStateValueGetter.cs`.

### 2.5 Audio input — DONE

`Classes/HitsoundStuff/SampleImporter.cs` uses `MediaFoundationReader` at 2 places to
open all samples that are not `.ogg`. Media Foundation is Windows only.

Fix: use `WaveFileReader` for `.wav` and `NLayer` for `.mp3`.

### 2.6 Audio output — about 2 days

`Views/HitsoundStudio/HitsoundStudioView.xaml.cs` uses `WasapiOut` at 1 place, to play
a sample preview. NAudio has no Linux output device.

Fix: use `Silk.NET.OpenAL`, SDL, or a small PipeWire wrapper.

### 2.7 Shell and system calls — about 1 day

| Item | Sites | Replacement |
|---|---|---|
| `Process.Start("explorer.exe", ...)` | 9 | `xdg-open` |
| `Classes/SystemTools/ShowSelectedInFileExplorer.cs` (shell32 P/Invoke, 252 lines) | 1 file | D-Bus `org.freedesktop.FileManager1`, or delete |
| Registry read, to find the osu! folder (`SettingsManager.cs`) | 1 | Scan the WINE prefix, or let the user set the path |
| `CommonOpenFileDialog`, `OpenFileDialog`, `SaveFileDialog` (`IOHelper.cs`) | 1 file | Avalonia `IStorageProvider` |

### 2.8 Case-sensitive file systems — about 2 to 3 days

**Do not underestimate this item.** Linux file systems are case-sensitive. Beatmaps
name their hitsound and image files with inconsistent case. Windows always resolves
those names. ext4 and btrfs do not.

Fix: add a case-insensitive path resolver in the file-read layer. This affects many
tools. No compiler will find this problem. Only a test with real beatmaps will find it.

---

## 3. Much work, or a complete overhaul

### 3.1 The WPF user interface — 2 to 4 months

This is the largest cost of the whole project. WPF has no Linux renderer, and it will
never have one. You must move to Avalonia UI. Avalonia is close to WPF, but it is not
source-compatible.

| Item | Size |
|---|---|
| XAML to convert | 6,402 lines, 46 files |
| View and view-model C# to adjust | 20,213 lines |
| Custom-drawn control: `Components/Graph/` | 3,915 lines |
| Custom-drawn controls: `TimeLine`, `ObjectVisualiser`, `GIFImageControl` | about 1,100 lines |

Additional interface problems:

- **MaterialDesignInXamlToolkit** is used in 37 of the 46 XAML files.
  `Material.Avalonia` exists, but it is not a drop-in replacement.
- **Extended WPF Toolkit** is used for a colour picker at 2 places only. This is easy.
- **VirtualizingWrapPanel** has an Avalonia equivalent.
- **`WindowChrome`** builds a custom title bar in `MainWindow.xaml`. Avalonia does this
  in a different way.
- **`Properties/Settings.settings` and `App.config`** use the old .NET Framework
  settings system. Both need a replacement.

### 3.2 EditorReader — a full rewrite, or remove the function

`lib/EditorReader.dll` reads the memory of the running `osu!.exe` process. 36 files
refer to it. `Classes/ToolHelpers/EditorReaderStuff.cs` (480 lines) is the single
wrapper, so you can replace it at one place.

The problem is not the code. On Linux, osu! stable itself runs in WINE. To read its
memory from a native Linux program, you must write a new reader against
`/proc/<pid>/mem`, and then find the data in the WINE address space. This is research
work with no guaranteed result.

`OsuMemoryDataProvider` in `IOHelper.cs` has the same problem.

**This makes identical function impossible in the first release.** These functions stop:

- Quick Run hotkeys (10 tools use them: Map Cleaner, Slider Merger,
  Slider Completionator, Slider Picturator, Timing Helper, Pattern Gallery,
  Sliderator, Tumour Generator, Auto-Fail Detector, Hitsound Preview Helper)
- "Use the selected hit objects"
- Automatic reload of the editor after a tool runs
- Detection of the current beatmap

The tools themselves still work. The user must select the `.osu` file by hand. Almost
every tool already supports that path.

### 3.3 Global hotkeys — 1 to 2 weeks

`Classes/SystemTools/ListenerManager.cs` uses `NonInvasiveKeyboardHookLibrary` and
`user32.dll`. It also uses `SendKeys` and `KeyInterop`.

On X11 you can use `XGrabKey`. On Wayland you need the XDG global-shortcuts portal,
and the compositor must support it. The value is low until EditorReader works, because
the hotkeys exist to drive Quick Run.

### 3.4 Image processing — 3 to 5 days

`System.Drawing.Common` is Windows only since .NET 7. It throws at run time on Linux.

| File | Work |
|---|---|
| `Classes/Tools/SlideratorStuff/SliderPicturator.cs` | Rewrite the `LockBits` pixel loops with ImageSharp or SkiaSharp |
| `Classes/GenericExtensions.cs` | Remove the `HBITMAP` bridge and the gdi32 P/Invoke |
| `Viewmodels/SliderPicturatorVm.cs` | Replace `InteropBitmap` with an Avalonia bitmap |
| `Components/ObjectVisualiser/OsuPatternToThumbnailConverter.cs` | Same |
| `Components/GIFImageControl.cs` | Rewrite, or use an Avalonia GIF control |

### 3.5 UI objects inside the logic layer — 2 to 3 days

Four places hold real interface objects in the logic layer. These need a true
refactor, not a type swap.

| File | Problem |
|---|---|
| `Classes/Tools/ComboColourStudio/ColourPoint.cs` | Builds `Button` and `PackIcon` objects |
| `Classes/Tools/ComboColourStudio/ComboColourProject.cs` | Reads `Keyboard.Modifiers` |
| `Classes/Tools/ComboColourStudio/SpecialColourDragAndDropListBox.cs` | Extends a WPF `ListBox` |
| `Classes/Tools/PatternGallery/*Vm.cs` (3 files) | Bound to the custom dialog control |

### 3.6 The automatic updater — 2 to 3 days, or remove it

`Updater/UpdateManager.cs` uses Onova with a GitHub release resolver and a ZIP
extractor. The update flow assumes a Windows layout.

Better: remove it and ship a Flatpak, an RPM, or an AUR package. Let the package
manager handle updates.

### 3.7 Build and release — 2 to 3 days

`.github/workflows/release.yml` runs on `windows-latest` and builds two Inno Setup
installers. A Linux release needs a new job and new package formats.

---

## 4. Deferred — review again later

### Snapping Tools / Geometry Dashboard

**Status: postponed. Not needed now. Review again after the main port works.**

This tool is wanted eventually, but it is not important yet. Do not let it block the
rest of the work.

Size: 11 of the 32 blocked files belong to this tool. If you postpone it, the blocked
set falls from 32 files to 21 files.

What it does: it draws a transparent overlay on top of the osu! editor window, and it
shows snapping guides in real time.

Why it is hard:

- `Views/SnappingTools/SnappingToolsOverlay.cs` uses **Overlay.NET**. That package is
  .NET Framework only (`NU1701`) and it is not portable.
- It depends on **Process.NET**, which is also .NET Framework only.
- It needs **EditorReader** (see 3.2) to know the editor state.
- `Classes/Tools/SnappingTools/CoordinateConverter.cs` uses `Screen.PrimaryScreen` and
  `PresentationSource` to map screen coordinates and DPI.
- The overlay must track a **WINE window** from a native Linux program.
- On **Wayland**, a program cannot position a window over another program's window,
  and it cannot read the global pointer position. X11 or XWayland can do this.

Questions to answer at the review:

1. Can the new EditorReader (3.2) supply the editor state at 60 Hz?
2. Is an X11-only build acceptable, or must Wayland work as well?
3. Can an Avalonia transparent window replace Overlay.NET?
4. Is a different design better? For example, a separate small overlay program.

Files in this group:

```
Classes/Tools/SnappingTools/CoordinateConverter.cs
Classes/Tools/SnappingTools/DataStructure/RelevantObject/IRelevantDrawable.cs
Classes/Tools/SnappingTools/DataStructure/RelevantObject/RelevantDrawable.cs
Classes/Tools/SnappingTools/DataStructure/RelevantObject/RelevantObjectPreferences.cs
Classes/Tools/SnappingTools/DataStructure/RelevantObject/RelevantObjects/RelevantCircle.cs
Classes/Tools/SnappingTools/DataStructure/RelevantObject/RelevantObjects/RelevantLine.cs
Classes/Tools/SnappingTools/DataStructure/RelevantObject/RelevantObjects/RelevantPoint.cs
Classes/Tools/SnappingTools/DataStructure/RelevantObjectGenerators/GeneratorGroupComparer.cs
Classes/Tools/SnappingTools/DataStructure/RelevantObjectGenerators/RelevantObjectsGenerator.cs
Classes/Tools/SnappingTools/Serialization/SnappingToolsPreferences.cs
Classes/Tools/SnappingTools/Serialization/SnappingToolsSaveSlot.cs
Views/SnappingTools/                                    (1,216 lines)
Viewmodels/SnappingToolsVm.cs
```

---

## Summary of effort

| Group | Effort |
|---|---|
| 1. No changes | 0 |
| 2. Some work | 2 to 3 weeks |
| 3. Much work — user interface | 2 to 4 months |
| 3. Much work — all other items | 3 to 5 weeks |
| 4. Snapping Tools | Not estimated. Review later. |

Identical function is **not** possible while EditorReader stays broken (3.2). Everything
else can reach identical function.

A cheaper option: 88% of the core is already portable. A command-line front end, or a
local web front end, gives most of the tools for a small part of the cost of the
Avalonia port.

---

## The 32 blocked files

This is the full list from the probe. 11 of them belong to Snapping Tools (group 4).

```
Classes/BeatmapHelper/ComboColour.cs
Classes/BeatmapHelper/SpecialColour.cs
Classes/GenericExtensions.cs
Classes/HitsoundStuff/LayerImportArgs.cs
Classes/MathUtil/Vector2.cs
Classes/SystemTools/Hotkey.cs
Classes/SystemTools/IHaveExtraProjectMenuItems.cs
Classes/SystemTools/IOHelper.cs
Classes/SystemTools/ListenerManager.cs
Classes/SystemTools/Settings.cs
Classes/Tools/ComboColourStudio/ColourPoint.cs
Classes/Tools/ComboColourStudio/ComboColourProject.cs
Classes/Tools/ComboColourStudio/SpecialColourDragAndDropListBox.cs
Classes/Tools/PatternGallery/OsuPatternDetailsVm.cs
Classes/Tools/PatternGallery/PatternCodeImportVm.cs
Classes/Tools/PatternGallery/PatternFileImportVm.cs
Classes/Tools/SlideratorStuff/GraphStateValueGetter.cs
Classes/Tools/SlideratorStuff/SliderPicturator.cs
Classes/Tools/TumourGenerating/Domain/TumourTemplateToIconConverter.cs
Classes/Tools/TumourGenerating/Options/ITumourLayer.cs
Classes/Tools/TumourGenerating/Options/TumourLayer.cs
```

Plus the 11 Snapping Tools files listed in section 4.

## How to repeat the probe

Install the .NET 10 SDK:

```bash
sudo dnf install dotnet-sdk-10.0
```

Confirm that the WPF program compiles, but cannot run:

```bash
dotnet build Mapping_Tools/Mapping_Tools.csproj -p:EnableWindowsTargeting=true
```

To repeat the core probe, make a project that targets `net10.0`, add
`Classes/**/*.cs` and `Properties/Annotations.cs` as compile items, and reference
`NAudio.Core`, `NAudio.Midi`, `NAudio.Vorbis`, `Newtonsoft.Json`, `NVorbis`,
`OggVorbisEncoder`, and `Process.NET`. Do **not** reference the `NAudio` metapackage.
