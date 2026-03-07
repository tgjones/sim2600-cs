using System;
using System.Runtime.InteropServices.JavaScript;
using Sim2600;

Console.WriteLine("Hello, Browser!");

partial class Sim2600WebProgram
{
    private static Sim2600Console _sim;

    [JSImport("dom.setSimState", "main.js")]
    internal static partial void SetSimState(int halfClockCount, bool vSync, bool vBlank, bool restartImage, int color);

    [JSExport]
    internal static void StartSimulator(byte[] romBytes)
    {
        _sim = new Sim2600Console(romBytes);
    }

    [JSExport]
    internal static void RunHalfCycle()
    {
        _sim.AdvanceOneHalfClock();

        var tia = _sim.SimTIA;

        // Get pixel color when TIA clock (~3mHz) is low
        if (tia.IsLow(tia.PadIndClk0))
        {
            var restartImage = tia.IsHigh(tia.VSync);
            var rgba = tia.ColorRgba8;

            SetSimState(
                tia.HalfClkCount, 
                tia.IsHigh(tia.VSync), 
                tia.IsHigh(tia.VBlank), 
                restartImage, 
                rgba.ToRgba8());
        }
    }
}
