// Uno hosting entry point for Reactor.
//
// Replaces the Windows framework's src/Reactor/Hosting/ReactorApp.cs (which is
// built on Application.Start + Win32/DWM/shell P/Invoke). Here, startup goes
// through Uno's UnoPlatformHostBuilder (Uno 6 unified Skia hosting) and a
// code-only Application subclass. Only the static surface the shared core reads
// (ActiveHostInternal, UIDispatcher, …) plus the public Run entry points are
// provided; multi-window / tray / persistence are minimal single-window stubs.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hosting;

namespace Microsoft.UI.Reactor;

/// <summary>Startup configuration captured before the Uno host bootstraps.</summary>
internal sealed record ReactorAppOptions(
    Func<Component>? RootFactory = null,
    Func<RenderContext, Element>? RootRenderFunc = null,
    Action<ReactorHost>? Configure = null,
    string WindowTitle = "Reactor App",
    double WindowWidth = 1024,
    double WindowHeight = 768,
    bool FullScreen = false);

/// <summary>
/// Application entry point for Reactor apps running on Uno Platform Skia targets
/// (desktop + WebAssembly, and theoretically mobile).
/// </summary>
public static partial class ReactorApp
{
    private static ReactorAppOptions _options = new();
    internal static ReactorAppOptions Options
    {
        get => Volatile.Read(ref _options);
        set => Volatile.Write(ref _options, value);
    }

    private static ReactorHost? _activeHost;

    /// <summary>The host of the active Reactor window (single-window model).</summary>
    public static ReactorHost? ActiveHost => Volatile.Read(ref _activeHost);

    internal static ReactorHost? ActiveHostInternal
    {
        get => Volatile.Read(ref _activeHost);
        set => Volatile.Write(ref _activeHost, value);
    }

    private static DispatcherQueue? _uiDispatcher;

    /// <summary>The UI dispatcher captured at launch (null before the window exists).</summary>
    public static DispatcherQueue? UIDispatcher
    {
        get => Volatile.Read(ref _uiDispatcher);
        internal set => Volatile.Write(ref _uiDispatcher, value);
    }

    /// <summary>Optional process-wide logger snapshotted by each host at construction.</summary>
    public static ILogger? AppLogger { get; set; }

    /// <summary>Devtools are not available in the Uno port.</summary>
    public static bool DevtoolsEnabled => false;

    // ── single-window topology stubs (multi-window is a Windows feature) ──
    private static readonly List<ReactorWindow> _windows = new();
    private static readonly List<ReactorTrayIcon> _trayIcons = new();

    /// <summary>Snapshot of open Reactor windows.</summary>
    public static IReadOnlyList<ReactorWindow> Windows
    {
        get { lock (_windows) { return _windows.ToArray(); } }
    }

    /// <summary>The primary (first) window, or null before launch.</summary>
    public static ReactorWindow? PrimaryWindow { get; internal set; }

    internal static void RegisterWindow(ReactorWindow w)
    {
        lock (_windows) { if (!_windows.Contains(w)) _windows.Add(w); }
        PrimaryWindow ??= w;
    }

    /// <summary>Finds an open window by key. Single-window model: returns the primary if it matches.</summary>
    public static ReactorWindow? FindWindow(WindowKey key)
    {
        lock (_windows)
        {
            foreach (var w in _windows)
                if (w.Spec.Key == key) return w;
        }
        return null;
    }

    /// <summary>
    /// Opening additional native windows is not supported on Skia heads in this
    /// port; returns the primary window so <c>UseOpenWindow</c> degrades to a no-op.
    /// </summary>
    public static ReactorWindow? OpenWindow(WindowKey key, WindowSpec spec, Func<Component> factory)
        => FindWindow(key) ?? PrimaryWindow;

    /// <summary>Overload used by <c>UseOpenWindow</c> (key carried on the spec).</summary>
    public static ReactorWindow? OpenWindow(WindowSpec spec, Func<Component> factory)
        => (spec.Key is { } k ? FindWindow(k) : null) ?? PrimaryWindow;

    /// <summary>Tray icons are a Windows shell feature; returns a stub handle.</summary>
    public static ReactorTrayIcon OpenTrayIcon(TrayIconSpec spec)
    {
        var icon = new ReactorTrayIcon(spec);
        lock (_trayIcons) { _trayIcons.Add(icon); }
        return icon;
    }

    /// <summary>Finds a registered tray-icon stub by key.</summary>
    public static ReactorTrayIcon? FindTrayIcon(WindowKey key)
    {
        lock (_trayIcons)
        {
            foreach (var t in _trayIcons)
                if (t.Spec.Key == key) return t;
        }
        return null;
    }

    /// <summary>
    /// Bulk-registers the built-in control catalog. Factory methods already
    /// self-register on first use, so this is only needed for the direct
    /// element-record idiom. The Uno port relies on factory self-registration,
    /// so this is currently a no-op kept for API parity.
    /// </summary>
    public static void RegisterAllBuiltIns() { /* factories self-register */ }

    // ── public Run entry points ───────────────────────────────────────────

    /// <summary>
    /// Launches a Reactor app whose root is a <see cref="Component"/> subclass.
    /// Blocks until the window closes (desktop). For WebAssembly use
    /// <see cref="RunAsync{TRoot}"/>.
    /// </summary>
    public static void Run<TRoot>(
        string title = "Reactor App",
        double width = 1024,
        double height = 768,
        bool fullScreen = false,
        Action<ReactorHost>? configure = null)
        where TRoot : Component, new()
    {
        Options = new ReactorAppOptions(
            RootFactory: static () => new TRoot(),
            Configure: configure,
            WindowTitle: title,
            WindowWidth: width,
            WindowHeight: height,
            FullScreen: fullScreen);
        UnoBootstrap.Run();
    }

    /// <summary>
    /// Launches a Reactor app from a render function (no Component subclass).
    /// Blocks until the window closes (desktop).
    /// </summary>
    public static void Run(
        string title,
        Func<RenderContext, Element> rootRender,
        double width = 1024,
        double height = 768,
        bool fullScreen = false,
        Action<ReactorHost>? configure = null)
    {
        Options = new ReactorAppOptions(
            RootRenderFunc: rootRender,
            Configure: configure,
            WindowTitle: title,
            WindowWidth: width,
            WindowHeight: height,
            FullScreen: fullScreen);
        UnoBootstrap.Run();
    }

    /// <summary>
    /// Async launch for WebAssembly (the browser thread cannot block). Mirror of
    /// <see cref="Run{TRoot}"/>; await it from a file-based app's top-level statements.
    /// </summary>
    public static Task RunAsync<TRoot>(
        string title = "Reactor App",
        double width = 1024,
        double height = 768,
        bool fullScreen = false,
        Action<ReactorHost>? configure = null)
        where TRoot : Component, new()
    {
        Options = new ReactorAppOptions(
            RootFactory: static () => new TRoot(),
            Configure: configure,
            WindowTitle: title,
            WindowWidth: width,
            WindowHeight: height,
            FullScreen: fullScreen);
        return UnoBootstrap.RunAsync();
    }

    /// <summary>Async launch (render function) for WebAssembly.</summary>
    public static Task RunAsync(
        string title,
        Func<RenderContext, Element> rootRender,
        double width = 1024,
        double height = 768,
        bool fullScreen = false,
        Action<ReactorHost>? configure = null)
    {
        Options = new ReactorAppOptions(
            RootRenderFunc: rootRender,
            Configure: configure,
            WindowTitle: title,
            WindowWidth: width,
            WindowHeight: height,
            FullScreen: fullScreen);
        return UnoBootstrap.RunAsync();
    }
}
