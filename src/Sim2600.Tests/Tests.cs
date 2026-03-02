namespace Sim2600.Tests;

public class Tests
{
    [Test]
    [Arguments("Adventure")]
    [Arguments("Asteroids")]
    [Arguments("DonkeyKong")]
    [Arguments("Pitfall")]
    [Arguments("SpaceInvaders")]
    public async Task StateMatchesExpectedValues(string rom)
    {
        var expectedFilePath = Path.Combine("Assets", $"{rom}-ExpectedStates.txt");
        var actualFilePath = $"{rom}-ActualStates.txt";

        {
            var sim = new Sim2600Console(Path.Combine("Assets", "Roms", $"{rom}.bin"));
            var imageWriter = new ImageWriter($"{rom}-ActualFrame");

            using var stateWriter = new StreamWriter(actualFilePath);

            while (true)
            {
                if (sim.HalfClkCount % 10_000 == 0)
                {
                    sim.WriteState(stateWriter);

                    if (sim.HalfClkCount == 260_000)
                    {
                        Assert.Fail("Didn't produce a frame after 260000 half cycles");
                        break;
                    }
                }

                sim.AdvanceOneHalfClock();

                // Get pixel color when TIA clock (~3mHz) is low
                var tia = sim.SimTIA;
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
                        var savedImage = imageWriter.RestartImage();
                        if (savedImage)
                        {
                            sim.WriteState(stateWriter);
                            break;
                        }
                    }
                    imageWriter.SetNextPixel(rgba);

                    if (tia.IsHigh(tia.VBlank))
                    {
                        Console.WriteLine($"VBLANK at TIA halfclock {tia.HalfClkCount}");
                    }
                }
            }
        }

        await Assert
            .That(File.ReadAllText(actualFilePath))
            .IsEqualTo(File.ReadAllText(expectedFilePath));

        await Assert
            .That(File.ReadAllBytes($"{rom}-ActualFrame-0.png"))
            .IsEquivalentTo(File.ReadAllBytes(Path.Combine("Assets", $"{rom}-ExpectedFrame-0.png")));
    }
}
