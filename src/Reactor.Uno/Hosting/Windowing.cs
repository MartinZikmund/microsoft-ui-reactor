// Minimal windowing layer for the Uno port.
//
// The Windows framework's windowing stack (src/Reactor/Hosting/ReactorWindow.cs,
// ReactorDisplay.cs, WindowSpec.cs, …) is heavily Win32/AppWindow/DWM coupled and
// is excluded from the Uno build. The shared core (Core/RenderContext.cs windowing
// hooks, Core/Element.cs title-bar wiring) still references these *types*, so this
// file provides Uno-friendly equivalents with the exact member surface the shared
// source touches. Single-window apps (the file-based Reactor-on-Uno target) use
// only a small slice; multi-window / tray / picker hooks are present so the core
// compiles and degrade gracefully on Skia targets.

using System;
using System.Collections.Generic;
using Microsoft.UI.Reactor.Core;
using WinUIWindow = Microsoft.UI.Xaml.Window;

namespace Microsoft.UI.Reactor;

/// <summary>Window display state. Mirrors the Windows framework's enum.</summary>
public enum WindowState
{
    Normal,
    Minimized,
    Maximized,
}

/// <summary>Stable identity key for <c>UseOpenWindow</c> de-duplication.</summary>
public readonly record struct WindowKey(string Name);

/// <summary>
/// Declarative description of a window's chrome. Only the members the shared
/// core reads are modelled; the rest of the Windows <c>WindowSpec</c> surface
/// (backdrop, embed, persistence, splitters, …) is intentionally omitted.
/// </summary>
public sealed record WindowSpec
{
    public string Title { get; init; } = "Reactor App";
    public double Width { get; init; } = 1024;
    public double Height { get; init; } = 768;
    public bool FullScreen { get; init; }
    public WindowKey? Key { get; init; }
    /// <summary>Null = framework default; true/false = explicit opt-in/out.</summary>
    public bool? ExtendsContentIntoTitleBar { get; init; }
}

/// <summary>Tray-icon spec stub — tray icons are a Windows shell feature.</summary>
public sealed record TrayIconSpec(WindowKey Key);

/// <summary>Live tray-icon handle stub.</summary>
public sealed class ReactorTrayIcon
{
    internal ReactorTrayIcon(TrayIconSpec spec) => Spec = spec;
    public TrayIconSpec Spec { get; private set; }
    public void Update(TrayIconSpec spec) => Spec = spec;
    public void Close() { }
}

/// <summary>Snapshot of a single display. Minimal shape used by <c>UseDisplays</c>.</summary>
public readonly record struct DisplayInfo(
    string Name,
    double X,
    double Y,
    double Width,
    double Height,
    double Scale,
    bool IsPrimary);

/// <summary>
/// Display enumeration. Skia heads don't expose a portable multi-monitor query,
/// so this returns an empty snapshot and never raises layout-change events.
/// </summary>
public static class ReactorDisplay
{
    public static IReadOnlyList<DisplayInfo> Displays { get; } = Array.Empty<DisplayInfo>();

#pragma warning disable CS0067 // event is part of the API surface; never raised on Skia
    public static event EventHandler? DisplayLayoutChanged;
#pragma warning restore CS0067
}

/// <summary>Event args for <see cref="ReactorWindow.PositionChanged"/>.</summary>
public sealed class WindowDipPositionChangedEventArgs : EventArgs
{
    public WindowDipPositionChangedEventArgs((double X, double Y) position) => Position = position;
    public (double X, double Y) Position { get; }
}

/// <summary>Event args for <see cref="ReactorWindow.ZOrderChanged"/>.</summary>
public sealed class WindowZOrderChangedEventArgs : EventArgs
{
    public WindowZOrderChangedEventArgs(bool movedToTop, bool isCovered)
    {
        MovedToTop = movedToTop;
        IsCovered = isCovered;
    }
    public bool MovedToTop { get; }
    public bool IsCovered { get; }
}

/// <summary>
/// Uno wrapper over a <see cref="Microsoft.UI.Xaml.Window"/>. Bridges the
/// shared windowing hooks to the live Skia window. Members the core never reads
/// are best-effort/no-op on Skia.
/// </summary>
public sealed class ReactorWindow
{
    private readonly object _titleBarLock = new();
    private bool _titleBarControlPresent;

    internal ReactorWindow(WinUIWindow nativeWindow, WindowSpec spec)
    {
        NativeWindow = nativeWindow;
        Spec = spec;

        // Bridge the WinUI window's activation events to the simple EventHandler
        // shape the shared hooks expect.
        nativeWindow.Activated += (_, args) =>
        {
            bool deactivated =
                args.WindowActivationState == global::Windows.UI.Core.CoreWindowActivationState.Deactivated;
            if (deactivated)
            {
                IsActive = false;
                Deactivated?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                IsActive = true;
                Activated?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    /// <summary>The underlying WinUI/Uno window.</summary>
    public WinUIWindow NativeWindow { get; }

    /// <summary>The spec this window was opened with.</summary>
    public WindowSpec Spec { get; private set; }

    public bool IsActive { get; private set; } = true;
    public WindowState State { get; private set; } = WindowState.Normal;
    public (double X, double Y) Position { get; private set; }

    /// <summary>Per-window persisted-state scope (backs <c>UsePersisted(PersistedScope.Window)</c>).</summary>
    public Microsoft.UI.Reactor.Core.IPersistedStateScope PersistedScope { get; }
        = new Microsoft.UI.Reactor.Core.WindowPersistedScope();

    /// <summary>
    /// Per-window DPI as a raw dots-per-inch value (96 = 100%). Derived from the
    /// window's <c>XamlRoot.RasterizationScale</c> when available.
    /// </summary>
    public uint Dpi
    {
        get
        {
            try
            {
                var scale = NativeWindow.Content?.XamlRoot?.RasterizationScale ?? 1.0;
                return (uint)Math.Round(96.0 * (scale <= 0 ? 1.0 : scale));
            }
            catch { return 96; }
        }
    }

#pragma warning disable CS0067 // Skia heads don't surface all of these; events are API surface.
    public event EventHandler<WindowDipPositionChangedEventArgs>? PositionChanged;
    public event EventHandler<WindowZOrderChangedEventArgs>? ZOrderChanged;
    public event EventHandler<WindowState>? StateChanged;
    public event EventHandler<uint>? DpiChanged;
    public event EventHandler? Activated;
    public event EventHandler? Deactivated;
#pragma warning restore CS0067

    /// <summary>Lifetime-bound aspect-ratio lock. No-op on Skia; returns a disposable token.</summary>
    public IDisposable RegisterAspectRatioOverride(double? widthOverHeight) => NoopDisposable.Instance;

    /// <summary>Stacks a "can close?" guard. No-op token on Skia.</summary>
    public IDisposable RegisterClosingGuard(Func<bool> canClose) => NoopDisposable.Instance;

    /// <summary>Starts a framework-managed window drag/move loop. No-op on Skia.</summary>
    public void BeginDragMove() { }

    /// <summary>Records that a WinUI TitleBar control is mounted in this window.</summary>
    public void MarkTitleBarControlPresent()
    {
        lock (_titleBarLock) { _titleBarControlPresent = true; }
    }

    internal bool TitleBarControlPresent
    {
        get { lock (_titleBarLock) { return _titleBarControlPresent; } }
    }

    /// <summary>Applies a changed spec to the live window (title only on Skia).</summary>
    public void Update(WindowSpec spec)
    {
        Spec = spec;
        try { NativeWindow.Title = spec.Title; } catch { /* best effort */ }
    }

    /// <summary>Closes the underlying window.</summary>
    public void Close()
    {
        try { NativeWindow.Close(); } catch { /* best effort */ }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();
        public void Dispose() { }
    }
}
