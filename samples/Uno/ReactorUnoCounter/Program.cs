using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

// Reactor owns the Uno Application + window + render loop. This single call is
// the whole entry point — the same shape as the Windows/WinUI ReactorApp.Run.
// WebAssembly can't block the browser thread, so it uses the async entry.
#if __WASM__
await ReactorApp.RunAsync<CounterApp>("Reactor Counter (Uno)", width: 480, height: 360);
#else
ReactorApp.Run<CounterApp>("Reactor Counter (Uno)", width: 480, height: 360);
#endif

class CounterApp : Component
{
    public override Element Render()
    {
        var (count, setCount) = UseState(0);

        return VStack(12,
            Heading($"Count: {count}"),
            HStack(8,
                Button("-", () => setCount(count - 1)),
                Button("+", () => setCount(count + 1))
            )
        ).Padding(24);
    }
}
