# Reactor on Uno Platform — samples

Samples that run **Microsoft.UI.Reactor** on Uno Platform Skia targets
(desktop, WebAssembly, Android), backed by the
[`Reactor.Uno`](../../src/Reactor.Uno) port library.

| Sample | What it shows | Targets |
| --- | --- | --- |
| [`ReactorUnoCounter`](ReactorUnoCounter) | Minimal counter — the smallest Reactor-on-Uno app | desktop, wasm |
| [`ReactorUnoShowcase`](ReactorUnoShowcase) | ToggleSwitch, Slider, ProgressBar, CheckBox, ComboBox, pickers, multi-window | desktop, wasm |
| [`ReactorUnoDroid`](ReactorUnoDroid) | The same counter component on **Android**, bootstrapped from an `Activity` | android |
| [`file-based/Counter.cs`](file-based/Counter.cs) | A whole WinUI-style Reactor app in **one `.cs` file** (`dotnet run Counter.cs`) | desktop |

## Prerequisites

- **.NET 10 SDK** (`10.0.100`+ — see [`global.json`](global.json))
- The **`wasm-tools`** workload for the WebAssembly target: `dotnet workload install wasm-tools`
- The **`android`** workload for `ReactorUnoDroid`: `dotnet workload install android`

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

### ReactorUnoDroid (Android)

Android has no console entry point, so this head bootstraps from an `Activity`
and asks Reactor for the `Application` rather than calling `ReactorApp.Run`:

```csharp
public class Application : Microsoft.UI.Xaml.NativeApplication
{
    public Application(IntPtr javaReference, JniHandleOwnership transfer)
        : base(() => ReactorApp.CreateApplication<CounterApp>("Reactor Counter (Android)"),
               javaReference, transfer) { }
}
```

The component itself is byte-for-byte the same as the desktop counter.

```bash
cd samples/Uno/ReactorUnoDroid

# Build only (produces bin/Debug/net10.0-android/dev.reactor.unodroid-Signed.apk)
dotnet build

# Deploy + launch on a connected device or running emulator
dotnet build -t:Run
```

Check the device is visible first with `adb devices`. To install the APK by hand:

```bash
adb install -r bin/Debug/net10.0-android/dev.reactor.unodroid-Signed.apk
```

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

> Two samples are deliberately outside the solution: the file-based `Counter.cs`
> (not a project — run it with `dotnet run Counter.cs`) and `ReactorUnoDroid`
> (android-only, so a per-TFM slnx build would fail it with `NETSDK1005` — build
> it directly with `cd samples/Uno/ReactorUnoDroid && dotnet build`).

## Hot Reload

Both app samples enable `HotReload` in `UnoFeatures`, so `dotnet watch` (desktop)
and the Uno Dev Server (wasm / Android / iOS) both re-render edits to a
`Component.Render()` body live, preserving `UseState`. One caveat: from the CLI,
`dotnet watch -f <tfm>` only works against a **single-targeted** head — see
[the Hot Reload section in the port README](../../src/Reactor.Uno/README.md#hot-reload).
