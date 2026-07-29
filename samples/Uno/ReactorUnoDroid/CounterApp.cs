using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

namespace ReactorUnoDroid;

/// <summary>
/// The exact same component as samples/Uno/ReactorUnoCounter — byte-for-byte
/// portable. Only the hosting head differs on Android.
/// </summary>
internal sealed class CounterApp : Component
{
    public override Element Render()
    {
        var (count, setCount) = UseState(0);

        return VStack(12,
            Heading($"Count: {count}"),
            Body("Reactor + Uno Platform on Android"),
            HStack(8,
                Button("-", () => setCount(count - 1)),
                Button("+", () => setCount(count + 1))
            )
        ).Padding(24);
    }
}
