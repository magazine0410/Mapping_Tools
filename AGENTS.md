# Repository Guidelines

## Project Structure & Module Organization

`Mapping_Tools.Core/` contains portable beatmap parsing, math, hitsound, and tool logic targeting `net10.0`. `Mapping_Tools/` is the Windows-only WPF host (`net10.0-windows`), while `Mapping_Tools.Avalonia/` is the cross-platform UI host. Tests mirror these boundaries in `Mapping_Tools.Core.Tests/`, `Mapping_Tools.Avalonia.Tests/`, and `Mapping_Tools_Tests/` (Windows-only). UI assets live under each host's `Data/`, `Styles/`, `Components/`, and `Views/` folders. Read `LINUX_PORT.md` before changing shared UI or platform services.

Keep core code platform-neutral. Calls to dialogs, settings, file pickers, or the osu! editor must go through interfaces in `Mapping_Tools.Core/Platform/`; do not reference WPF, WinForms, the registry, P/Invoke, or `System.Drawing.Bitmap` from the core. Existing core namespaces intentionally retain `Mapping_Tools.*` names even when they differ from folder names.

## Build, Test, and Development Commands

- `dotnet build Mapping_Tools.Core/Mapping_Tools.Core.csproj` builds portable logic.
- `dotnet run --project Mapping_Tools.Avalonia/Mapping_Tools.Avalonia.csproj` launches the native cross-platform host.
- `dotnet test Mapping_Tools.Core.Tests/Mapping_Tools.Core.Tests.csproj` runs portable algorithm tests.
- `dotnet test Mapping_Tools.Avalonia.Tests/Mapping_Tools.Avalonia.Tests.csproj` runs headless Avalonia UI tests.
- `dotnet build Mapping_Tools.sln -p:EnableWindowsTargeting=true` compiles the full solution from Linux; the WPF application still requires Windows to run.

Use `--filter "FullyQualifiedName~ValidationTests"` with `dotnet test` for a focused run.

## Coding Style & Naming Conventions

Follow the existing C# style: four-space indentation, braces on the declaration line, `PascalCase` for types and public members, and `camelCase` for locals, parameters, and private fields. Keep XAML/AXAML code-behind beside its markup (`CleanerView.axaml` and `CleanerView.axaml.cs`). Nullable reference types are disabled; preserve explicit null handling. No repository-wide formatter is configured, so avoid unrelated formatting churn.

## Testing Guidelines

Tests use MSTest with `[TestClass]` and `[TestMethod]`. Name test files and classes with the `Tests` suffix and give methods behavior-focused names. Add portable tests to `Mapping_Tools.Core.Tests`; use Avalonia headless tests for controls. Use `Path.Combine` for fixtures so tests remain cross-platform. No coverage threshold is configured, but new behavior and regressions should receive focused tests.

## Commit & Pull Request Guidelines

Recent commits use short, sentence-case, imperative summaries, such as `Add the Avalonia shell, the Linux host`. Keep each commit scoped to one coherent change. Pull requests should explain user-visible behavior, identify affected hosts, list test/build commands run, and link related issues. Include screenshots for WPF or Avalonia visual changes and call out any Windows-only verification still required.
