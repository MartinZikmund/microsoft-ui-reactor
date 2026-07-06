using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

#if __WASM__
await ReactorApp.RunAsync<Showcase>("Reactor Showcase (Uno)", width: 560, height: 620);
#else
ReactorApp.Run<Showcase>("Reactor Showcase (Uno)", width: 560, height: 620);
#endif

class Showcase : Component
{
    static readonly string[] Fruits = { "Donuts", "Apples", "Bananas" };

    public override Element Render()
    {
        var (on, setOn) = UseState(true);
        var (slider, setSlider) = UseState(40.0);
        var (chk, setChk) = UseState<bool?>(true);
        var (combo, setCombo) = UseState(0);
        var (count, setCount) = UseState(0);

        return ScrollView(
            VStack(14,
                Heading("Reactor on Uno — Control Showcase"),

                TextBlock($"Counter: {count}"),
                HStack(8,
                    Button("-", () => setCount(count - 1)),
                    Button("+", () => setCount(count + 1))),

                ToggleSwitch(on, setOn, header: "Feature toggle"),
                TextBlock(on ? "Feature is ON" : "Feature is OFF"),

                TextBlock($"Slider value: {slider:0}"),
                Slider(slider, 0, 100, setSlider),
                ProgressBar(slider),

                CheckBox(chk, b => setChk(b), label: "I agree"),
                TextBlock(chk == true ? "Checked" : "Unchecked"),

                TextBlock($"Favourite: {Fruits[combo]}"),
                ComboBox(Fruits, combo, setCombo)
            ).Padding(24)
        );
    }
}
