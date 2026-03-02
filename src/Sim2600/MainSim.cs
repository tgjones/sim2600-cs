namespace Sim2600;

public sealed class MainSim
{
    // The console simulator ties together a simulation
    // of the 6502 chip, a simulation of the TIA chip, 
    // an emulation of the 6532 RIOT (RAM, I/O, Timer), and
    // a cartridge ROM file holding the program instructions.

    public static void Execute(string romFilePath)
    {
        var sim = new Sim2600Console(romFilePath);
        var imageWriter = new ImageWriter(Path.GetFileNameWithoutExtension(romFilePath));

        var lastUpdateTimeSec = DateTime.MinValue;

        while (true)
        {
            Console.WriteLine("Entering simulation loop");

            var tia = sim.SimTIA;

            // How many simulation clock changes to run between updates
            const int numTIAHalfClocksPerRender = 128;

            for (var i = 0; i < numTIAHalfClocksPerRender; i++)
            {
                sim.AdvanceOneHalfClock();

                // Get pixel color when TIA clock (~3mHz) is low
                if (tia.IsLow(tia.PadIndClk0))
                {
                    var restartImage = false;
                    if (tia.IsHigh(tia.VSync))
                    {
                        Console.WriteLine($"VSYNC high at TIA halfclock {tia.HalfClkCount}");
                        restartImage = true;
                    }

                    // grayscale
                    // lum = self.simTIA.get3BitLuminance() << 5
                    // rgba = (lum << 24) | (lum << 16) | (lum << 8) | 0xFF

                    // color
                    var rgba = tia.ColorRgba8;

                    if (restartImage)
                    {
                        imageWriter.RestartImage();
                    }
                    imageWriter.SetNextPixel(rgba);

                    if (tia.IsHigh(tia.VBlank))
                    {
                        Console.WriteLine($"VBLANK at TIA halfclock {tia.HalfClkCount}");
                    }
                }
            }

            var timeNow = DateTime.UtcNow;
            if (lastUpdateTimeSec != DateTime.MinValue)
            {
                var elapsedSec = timeNow - lastUpdateTimeSec;
                var secPerSimClock = 2.0 * elapsedSec / numTIAHalfClocksPerRender;
                // TODO: Stats
            }
            lastUpdateTimeSec = timeNow;
        }
    }
}
