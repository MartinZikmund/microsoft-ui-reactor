# Microsoft.UI.Reactor on Uno Platform (Skia)

A port of [Microsoft.UI.Reactor](../../README.md) — the declarative, hooks-based,
component MVU framework for WinUI 3 — to **Uno Platform Skia** targets, so the
exact same Reactor C# can run on **desktop and WebAssembly** (and, theoretically,
mobile) instead of only Windows / Windows App SDK.

Reactor renders *real `Microsoft.UI.Xaml` controls*. Uno Platform reimplements
that same `Microsoft.UI.Xaml` API surface across Skia heads. So this port is
fundamentally a **retargeting**: the Reactor reconciler, hooks, DSL, layout
engine, charting, data grid, etc. are shared *verbatim* from `../Reactor`,
recompiled against Uno's `Microsoft.UI.Xaml`, with a small Uno-specific hosting
layer replacing the Windows-only windowing/shell stack.

## Status

| Target | TFM | State |
|---|---|---|
| Desktop (Win32 / X11 / macOS / Framebuffer) | `net10.0-desktop` | ✅ Builds **and runs** — interactive |
| WebAssembly | `net10.0-browserwasm` | ✅ Builds **and runs** in the browser |
| Android | `net10.0-android` | ✅ Library **compiles** (a mobile head is needed to deploy) |
| iOS / Mac Catalyst | — | Not wired, but the framework is pure `Microsoft.UI.Xaml`, so it should compile similarly |

Verified end-to-end: `UseState` → `Button` click → reconciler diff → patched
`TextBlock`, and a controlled `TextBox` with two-way binding, both on Skia
desktop; the same app renders in the browser via WASM.

## Quick start — a file-based, single-file Reactor app

This mirrors the [xaml.dev "file-based WinUI apps with Microsoft.UI.Reactor"](https://xaml.dev/post/file-based-winui-apps-with-microsoft-ui-reactor)
post, but cross-platform. The whole app is one `.cs` file
([`samples/Uno/file-based/Counter.cs`](../../samples/Uno/file-based/Counter.cs)):

```csharp
#:sdk Uno.Sdk@6.7.0-dev.93
#:project ../../../src/Reactor.Uno/Reactor.Uno.csproj
#:property TargetFramework=net10.0-desktop
#:property OutputType=Exe
#:property UnoSingleProject=true
#:property UnoFeatures=SkiaRenderer
#:property PublishAot=false
#:property PackAsTool=false

using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

ReactorApp.Run<CounterApp>("Single-File Reactor on Uno", width: 520, height: 400);

class CounterApp : Component
{
    public override Element Render()
    {
        var (name, setName) = UseState("Uno");
        var (count, setCount) = UseState(0);

        return VStack(12,
            Heading($"Hello, {name}!"),
            TextBox(name, setName, placeholderText: "Your name"),
            Heading($"Count: {count}"),
            HStack(8,
                Button("-", () => setCount(count - 1)),
                Button("+", () => setCount(count + 1))
            )
        ).Padding(24);
    }
}
```

Run it:

```bash
cd samples/Uno/file-based
dotnet run Counter.cs
```

> **Versus the WinUI version.** The WinUI file-based app uses
> `#:property TargetFramework=net10.0-windows10.0.22621.0` +
> `#:property WindowsAppSDKSelfContained=true` and `dotnet run App.cs -a x64`.
> Here we swap to `#:sdk Uno.Sdk`, `TargetFramework=net10.0-desktop`, and add the
> Reactor.Uno reference. The Reactor code itself is **identical**.

### Run on WebAssembly instead

A single file-based app targets one TFM at a time. For WASM, set
`#:property TargetFramework=net10.0-browserwasm` and use the async entry point
(`await ReactorApp.RunAsync<CounterApp>(...)`) because the browser thread can't
block. See [`samples/Uno/ReactorUnoCounter`](../../samples/Uno/ReactorUnoCounter) for a normal
multi-targeted project that does both from one source via `#if __WASM__`:

```bash
cd samples/Uno/ReactorUnoCounter
dotnet run -f net10.0-desktop          # desktop window
dotnet run -f net10.0-browserwasm      # prints a localhost URL — open it
```

## Architecture

The port lives under the repo's conventional `src/` and `samples/` folders, but
**each Uno location carries its own build-isolation files** so it never inherits
the Windows / Windows App SDK build infrastructure — the repo-root
`Directory.Build.props` imports `Reactor.targets` and pins WinAppSDK, and the
root `Directory.Packages.props` turns Central Package Management on. MSBuild
stops walking up at the nearest match, so these shadow files pin the `Uno.Sdk`
and turn CPM off for the Uno projects only; the Windows framework build is
untouched (and the Uno projects stay out of the main `Reactor.slnx`, so CI never
restores them).

```
src/Reactor.Uno/            # the port library (assembly `Reactor`, ns Microsoft.UI.Reactor)
  Reactor.Uno.csproj        # shares ../Reactor source, targets Uno TFMs
  GlobalUsings.cs           # disambiguation aliases (see below)
  Hosting/
    ReactorApp.cs           # ReactorApp.Run / RunAsync + the static surface the core needs
    UnoBootstrap.cs         # UnoPlatformHostBuilder wiring + the code-only Application
    ReactorHost.cs          # the render loop (reconcile → Window.Content → effects)
    Windowing.cs            # minimal ReactorWindow + windowing types for the shared hooks
  global.json               # pins Uno.Sdk 6.7.0-dev.93
  nuget.config              # nuget.org (self-contained restore)
  Directory.Build.props     # stops inheritance of the repo-root Windows build infra
  Directory.Packages.props  # CPM off for this project

samples/Uno/                # Uno samples — one shared isolation set for all three
  global.json · nuget.config · Directory.Build.props · Directory.Packages.props
  ReactorUnoCounter/        # normal Uno single-project app (desktop + wasm)
  ReactorUnoShowcase/       # larger showcase app
  file-based/Counter.cs     # the single-file app

Reactor.Uno.slnx            # dedicated solution (kept out of the main Reactor.slnx)
```

### Source sharing

`Reactor.Uno.csproj` pulls the portable framework folders from `../Reactor`
via explicit `<Compile Include>` globs: `Core`, `Hooks`, `Elements`, `Input`,
`Accessibility`, `Animation`, `Yoga`, `Flex`, `Data`, `Controls`, `Markdown`,
`Charting`, `Diagnostics`, plus a few portable `Hosting/*` files (the charting
bridge, render stats, hot-reload service, XAML interop). The wrapper
source-generator (`Reactor.Wrappers.Generator`) runs as an analyzer to emit the
control descriptors, exactly as in the Windows build.

The assembly is named **`Reactor`** with root namespace
**`Microsoft.UI.Reactor`** — identical to the Windows package — so consumer
`using Microsoft.UI.Reactor;` and `ReactorApp.Run<T>` work unchanged.

### What's replaced or excluded

The Windows `Hosting/` stack is Win32/DWM/AppWindow/shell P/Invoke-heavy, so it
is **not** shared; this project provides a slim Uno hosting layer instead:

- **`ReactorApp`** — `Run` (sync, desktop) / `RunAsync` (async, WASM) build an
  `UnoPlatformHostBuilder` and a code-only `Application`. Platform providers are
  selected per-TFM (`UseX11`/`UseWin32`/`UseMacOS`/`UseLinuxFrameBuffer` on
  desktop, `UseWebAssembly` on WASM).
- **`ReactorHost`** — a trimmed render loop (keeps coalescing, reconcile, effects
  flush, perf stats, and the charting-activation seam; drops the system-backdrop,
  dev-overlay, focus-revalidation, and in-render accessibility machinery).
- **`ReactorWindow` + windowing types** — minimal Uno-backed equivalents so the
  shared windowing hooks (`UseWindow`, `UseWindowSize`, `UseDpi`, …) compile and
  degrade gracefully.

**Excluded subsystems:** `Docking/` (native tab tear-off / floating windows via
Win32 — the dock event-fluent region in `ElementExtensions.Events.cs` is guarded
with `#if !REACTOR_UNO`), the Windows shell integration (tray/jumplist/taskbar),
window persistence, and the in-app devtools menu.

### Surgical shared-source edits

The port touches only **two** shared files, and just one of them carries a
`REACTOR_UNO` conditional (the symbol is defined only by this project, so the
Windows build is **unaffected** — verified green):

- `Elements/ElementExtensions.Events.cs` — the Docking event fluents (`#if !REACTOR_UNO`),
  since `Docking/` is excluded from the port.
- `Core/Reconciler.Mount.cs` — `return null` → `return default` in a generic
  helper. Not a conditional: semantically identical on both builds (`T` is
  constrained to a reference type); it just satisfies Uno's nullable analysis.

> A second conditional used to guard `Hyperlink.UnderlineStyle` (Uno declared the
> `UnderlineStyleProperty` DP identifier `internal`). That was fixed upstream in
> [unoplatform/uno#23652](https://github.com/unoplatform/uno/issues/23652), so as of
> `Uno.Sdk 6.7.0-dev.117` the guard is gone and the shared DP-based updater compiles
> unchanged.

### Name disambiguation (`GlobalUsings.cs`)

Uno's implicit `Microsoft.UI.Xaml.Controls` global using collides with Reactor's
own `Controls`/`Core` types and with `Microsoft.UI.Xaml`. The shared source
already carries its own `using` directives, so global aliases pin the few bare
names that would otherwise be ambiguous (`SelectionMode` → Reactor's;
`ElementFactoryGetArgs`/`RecycleArgs` → `Microsoft.UI.Xaml`).

## Feature support on Uno Skia

Legend: ✅ works · 🟡 partial / unverified · ❌ not supported (compiles, but no-ops or throws at runtime).

| Area | Status | Notes |
| --- | :---: | --- |
| MVU core: hooks, state, effects, memo, reconciler | ✅ | Full parity with the Windows framework. |
| Layout: Grid, Stack, panels, Yoga/Flex | ✅ | |
| Core controls: Button, TextBlock, TextBox, ToggleSwitch, Slider, ProgressBar, CheckBox, ComboBox, ScrollView | ✅ | Sample-verified (desktop + wasm). |
| Theming (Light/Dark) + live theme-change re-render | ✅ | |
| Implicit animations & transitions (Scale/Rotation/Opacity/Translation transitions, spring / natural-motion, `ImplicitAnimationCollection`) | ❌ | Not implemented in Uno — the calls no-op. |
| RichTextBlock / rich inline text / Markdown rendering | 🟡 | Most `RichTextBlock` members are not implemented in Uno; rich text and Markdown render incompletely. |
| RichEditBox | ❌ | Not implemented in Uno. |
| Custom TitleBar (drag regions, back button, panes) | ❌ | Not implemented in Uno. |
| Specialty controls: ParallaxView, MapControl, SemanticZoom, LinedFlowLayout, AnnotatedScrollBar | ❌ | Not implemented in Uno. |
| Gesture/access flags: `IsTapEnabled` / `IsHoldingEnabled` / `IsDoubleTapEnabled` / `IsRightTapEnabled`, `AccessKey`, `CharacterReceived` | 🟡 | Those specific flags no-op; basic pointer/click still works. |
| High-contrast / forced-colors detection | ❌ | `AccessibilitySettings.HighContrast` not implemented — charts don't adapt to high contrast. |
| Single window + render loop + error fallback | ✅ | |
| Multi-window (`OpenWindow` / `UseOpenWindow`) | ✅ desktop | Real secondary windows on every desktop head (X11 / Win32 / macOS / FrameBuffer), each with its own `ReactorHost`, render loop and state. Android/iOS throw `InvalidOperationException` (Uno doesn't support secondary windows there) — `UseOpenWindow` catches it and degrades to a null handle. No OS windows in the browser (wasm). Demoed in `samples/Uno/ReactorUnoShowcase`. |
| DPI (`UseDpi`) | ✅ | Per-window DPI from `XamlRoot.RasterizationScale`, and live changes (window dragged to a monitor with a different scale, or the display scale changed) re-render via `XamlRoot.Changed`. Correctly per-window, since each window has its own `XamlRoot`. |
| File / folder pickers (`UseFilePickerAsync` / `UseFolderPickerAsync`) | ✅ desktop | Uno implements the WinRT pickers. The shared hook's `WindowNative.GetWindowHandle` + `InitializeWithWindow` association is exactly the initialization Uno's own docs prescribe for Uno.WinUI apps — it is not a Windows-only path. On **wasm** it depends on the browser: Uno uses the File System Access API where available (Chromium) and falls back to download/upload pickers; `FolderPicker` needs the File System Access API. Demoed in `samples/Uno/ReactorUnoShowcase`. |
| Tray icons / shell (jump list, taskbar) | ❌ | Stub no-ops. |
| Window persistence (placement save/restore) | ❌ | Not shared. |
| System backdrop / Mica / DWM effects | ❌ | Not shared. |
| Multi-monitor / display enumeration | ❌ | `ReactorDisplay.Displays` returns empty. |
| Window drag-move, aspect-ratio lock, closing guards | ❌ | No-op stubs on Skia. |
| Docking (dock manager, tab tear-off, floating windows, splitters) | ❌ | Excluded from the port entirely. |
| In-app devtools | ❌ | `DevtoolsEnabled` is `false`. |
| Charting / DataGrid / PropertyGrid | 🟡 | Compile and share the WinUI render path; not yet runtime-verified on Skia. |

> The ❌ / 🟡 runtime rows line up with the `Uno0001` *not-implemented* build warnings: the code compiles, but those specific APIs no-op (or throw) on Uno. They don't affect the ✅ rows.

## Known limitations / notes

- **Uno version.** The port requires Uno **6.7** APIs (`DispatcherQueueSynchronizationContext`,
  the `TitleBar` drag-region APIs) that are absent from the current 6.5 GA, so it
  pins the 6.7 preview — `Uno.Sdk 6.7.0-dev.117`, set in `global.json` (repo root,
  `src/Reactor.Uno/`, `samples/Uno/`, and the file-based `Counter.cs` header).
  Switch to 6.7 GA when it ships. The Skia runtime packages take their version from
  `$(UnoVersion)`, which **Uno.Sdk supplies**, so they track the SDK automatically —
  don't hardcode a version for them (it drifts and trips `NU1605`).
- **Don't name a file-based app's root component `App`** — Uno.Sdk generates an
  `App` type for single-project EXEs, which would clash. Use any other name
  (`CounterApp`, `MyApp`, …); `ReactorApp.Run<T>` doesn't care.
- See the **Feature support on Uno Skia** matrix above for what works, what's
  partial, and what's unsupported. The unsupported/partial rows surface as
  `Uno0001` *not-implemented* build warnings — runtime no-ops in Uno, not build
  breaks — and only affect those specific features.

## Mobile

`Reactor.Uno` already **compiles for `net10.0-android`**. To actually deploy to
Android/iOS you'd add a thin mobile *head* project (Uno single-project EXE) that
references `Reactor.Uno`, wires the native entry point (Activity / AppDelegate),
and mounts a root `Component` via a `ReactorHost`. The framework code itself is
platform-agnostic `Microsoft.UI.Xaml`, so no Reactor changes are expected.
