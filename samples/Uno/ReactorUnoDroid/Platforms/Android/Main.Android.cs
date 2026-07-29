using System;
using Android.Runtime;

namespace ReactorUnoDroid.Droid;

/// <summary>
/// Android application entry point.
/// </summary>
/// <remarks>
/// Android has no console <c>Main</c>: the OS instantiates this type and Uno
/// starts the XAML application from the factory handed to the base ctor. That
/// factory is the one place the Android head differs from desktop/WASM — where
/// those call <c>ReactorApp.Run&lt;CounterApp&gt;(...)</c>, here we ask Reactor
/// for the <c>Application</c> and let Android own the lifecycle.
/// </remarks>
[global::Android.App.ApplicationAttribute(
    Label = "@string/ApplicationName",
    LargeHeap = true,
    HardwareAccelerated = true,
    Theme = "@style/AppTheme"
)]
public class Application : Microsoft.UI.Xaml.NativeApplication
{
    public Application(IntPtr javaReference, JniHandleOwnership transfer)
        : base(
            () => Microsoft.UI.Reactor.ReactorApp.CreateApplication<CounterApp>(
                "Reactor Counter (Android)"),
            javaReference,
            transfer)
    {
    }
}
