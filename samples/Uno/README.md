# Reactor on Uno Platform — samples

Three samples that run **Microsoft.UI.Reactor** on Uno Platform Skia targets
(desktop + WebAssembly), backed by the [`Reactor.Uno`](../../src/Reactor.Uno)
port library.

| Sample | What it shows | Targets |
| --- | --- | --- |
| [`ReactorUnoCounter`](ReactorUnoCounter) | Minimal counter — the smallest Reactor-on-Uno app | desktop, wasm |
| [`ReactorUnoShowcase`](ReactorUnoShowcase) | ToggleSwitch, Slider, ProgressBar, CheckBox, ComboBox, counter | desktop, wasm |
| [`file-based/Counter.cs`](file-based/Counter.cs) | A whole WinUI-style Reactor app in **one `.cs` file** (`dotnet run Counter.cs`) | desktop |

## Prerequisites

- **.NET 10 SDK** (`10.0.100`+ — see [`global.json`](global.json))
- The **`wasm-tools`** workload for the WebAssembly target: `dotnet workload install wasm-tools`

The Uno projects resolve `Uno.Sdk` from the nearby `global.json` and restore from
`nuget.org` only (see [`nuget.config`](nuget.config)); they do **not** inherit the
repo-root Windows/WinAppSDK build infrastructure.

## Run

### ReactorUnoCounter / ReactorUnoShowcase

```bash
# Desktop (X11 / Win32 / macOS / FrameBuffer — picked at runtime)
cd samples/Uno/ReactorUnoCounter
dotnet run -f net10.0-desktop

# WebAssembly (opens a localhost URL in your browser)
dotnet run -f net10.0-browserwasm
```

Swap `ReactorUnoCounter` for `ReactorUnoShowcase` to run the control showcase.

### file-based/Counter.cs

A single-file app — no project, no `.csproj`. Reactor owns the Uno `Application`,
so the file's root component must **not** be named `App` (Uno.Sdk generates its own
`App`).

```bash
cd samples/Uno/file-based
dotnet run Counter.cs
```

## Build the whole Uno set

From the repo root:

```bash
dotnet build Reactor.Uno.slnx
```

> The file-based `Counter.cs` is not a project and is not part of the solution;
> run it directly with `dotnet run Counter.cs`.
