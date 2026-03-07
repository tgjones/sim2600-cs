namespace Sim2600;

public class SimTIA : CircuitSimulatorBase
{
    private static readonly string[] AddressBusPadNames =
    [
        "AB0", "AB1",  "AB2",  "AB3",  "AB4",  "AB5"
    ];

    private static readonly string[] DataBusPadNames = ["DB0", "DB1", "DB2", "DB3", "DB4", "DB5", "DB6", "DB7"];

    // TODO(Tim: Bug? Should be DB7_drvLo?
    private static readonly string[] DataBusDriverNames = ["DB6_drvLo", "DB6_drvHi", "DB7_drvHi", "DB7_drvHi"];

    private static readonly string[] InputPadNames = ["I0", "I1", "I2", "I3", "I4", "I5"];

    private Rgb[] _colLumToRGB8LUT;
    public readonly List<int> AddressBusPads = [];
    public readonly List<int> DataBusPads = [];
    public readonly List<int> DataBusDrivers = [];
    public readonly List<int> InputPads = [];

    public readonly int IndDB6_drvLo;
    public readonly int IndDB6_drvHi;
    public readonly int IndDB7_drvLo;
    public readonly int IndDB7_drvHi;
    public readonly int PadIndClk0;
    public readonly int PadIndClk2;
    public readonly int PadIndPH0;
    public readonly int PadIndCS0;
    public readonly int PadIndCS3;
    public readonly int[] PadIndsCS0CS3;
    public readonly int PadIndRW;
    public readonly int PadIndDEL;

    public readonly int IndRDY_lowCtrl;
    public readonly int VBlank;
    public readonly int VSync;
    public readonly int WSync;
    public readonly int RSync;

    public readonly int L0_lowCtrl;
    public readonly int L1_lowCtrl;
    public readonly int L2_lowCtrl;
    public readonly int ColCnt_t0;
    public readonly int ColCnt_t1;
    public readonly int ColCnt_t2;
    public readonly int ColCnt_t3;

    public SimTIA()
    {
        LoadCircuit(
            NetTIAData.NumWires, NetTIAData.NextCtrl, NetTIAData.NoWire,
            NetTIAData.WirePulled, NetTIAData.WireCtrlFets, NetTIAData.WireGates, NetTIAData.WireNames,
            NetTIAData.NumFets, NetTIAData.FetSide1WireInds, NetTIAData.FetSide2WireInds, NetTIAData.FetGateInds);

        InitColLumLUT();

        // Temporarily inhibit TIA from driving DB6 and DB7
        SetHighWN("CS3");
        SetHighWN("CS0");

        RecalcAllWires();

        foreach (var padName in AddressBusPadNames)
        {
            AddressBusPads.Add(GetWireIndex(padName));
        }

        foreach (var padName in DataBusPadNames)
        {
            DataBusPads.Add(GetWireIndex(padName));
        }

        foreach (var padName in DataBusDriverNames)
        {
            DataBusDrivers.Add(GetWireIndex(padName));
        }

        foreach (var padName in InputPadNames)
        {
            InputPads.Add(GetWireIndex(padName));
        }

        IndDB6_drvLo = GetWireIndex("DB6_drvLo");
        IndDB6_drvHi = GetWireIndex("DB6_drvHi");
        IndDB7_drvLo = GetWireIndex("DB7_drvLo");
        IndDB7_drvHi = GetWireIndex("DB7_drvHi");
        PadIndClk0 = GetWireIndex("CLK0");
        PadIndClk2 = GetWireIndex("CLK2");
        PadIndPH0 = GetWireIndex("PH0");
        PadIndCS0 = GetWireIndex("CS0");
        PadIndCS3 = GetWireIndex("CS3");
        PadIndsCS0CS3 = [PadIndCS0, PadIndCS3];
        PadIndRW = GetWireIndex("R/W");
        PadIndDEL = GetWireIndex("del");

        // The TIA's RDY_low wire is high when it's pulling the
        // 6502's RDY to ground.  RDY_lowCtrl controls RDY_low
        IndRDY_lowCtrl = GetWireIndex("RDY_lowCtrl");
        VBlank = GetWireIndex("VBLANK");
        VSync = GetWireIndex("VSYNC");
        WSync = GetWireIndex("WSYNC");
        RSync = GetWireIndex("RSYNC");

        // Wires that govern the output pixel's luminance and color
        L0_lowCtrl = GetWireIndex("L0_lowCtrl");
        L1_lowCtrl = GetWireIndex("L1_lowCtrl");
        L2_lowCtrl = GetWireIndex("L2_lowCtrl");
        ColCnt_t0 = GetWireIndex("COLCNT_T0");
        ColCnt_t1 = GetWireIndex("COLCNT_T1");
        ColCnt_t2 = GetWireIndex("COLCNT_T2");
        ColCnt_t3 = GetWireIndex("COLCNT_T3");
    }

    private void InitColLumLUT()
    {
        // Colors from http://en.wikipedia.org/wiki/Television_Interface_Adapter
        var col = new Rgb[16][];
        col[0] = [new Rgb(0, 0, 0), new Rgb(236, 236, 236)];
        col[1] = [new Rgb(68, 68, 0), new Rgb(252, 252, 104)];
        col[2] = [new Rgb(112, 40, 0), new Rgb(236, 200, 120)];
        col[3] = [new Rgb(132, 24, 0), new Rgb(252, 188, 148)];
        col[4] = [new Rgb(136, 0, 0), new Rgb(252, 180, 180)];
        col[5] = [new Rgb(120, 0, 92), new Rgb(236, 176, 224)];
        col[6] = [new Rgb(72, 0, 120), new Rgb(212, 176, 252)];
        col[7] = [new Rgb(20, 0, 132), new Rgb(188, 180, 252)];
        col[8] = [new Rgb(0, 0, 136), new Rgb(164, 164, 252)];
        col[9] = [new Rgb(0, 24, 124), new Rgb(164, 200, 252)];
        col[10] = [new Rgb(0, 44, 92), new Rgb(164, 224, 252)];
        col[11] = [new Rgb(0, 60, 44), new Rgb(164, 252, 212)];
        col[12] = [new Rgb(0, 60, 0), new Rgb(184, 252, 184)];
        col[13] = [new Rgb(20, 56, 0), new Rgb(200, 252, 164)];
        col[14] = [new Rgb(44, 48, 0), new Rgb(224, 236, 156)];
        col[15] = [new Rgb(68, 40, 0), new Rgb(252, 224, 140)];

        // Interpolate linearly between the colors above using 3-bit lum
        // Populate the look up table addressed by a 7-bit col-lum value,
        // where color bits are most significant and luminance bits are
        // least significant
        _colLumToRGB8LUT = new Rgb[128];
        for (var intKey = 0; intKey < col.Length; intKey++)
        {
            var colPair = col[intKey];
            var start = colPair[0];
            var end = colPair[1];

            var diff = new Rgb(
                (byte)(end.R - start.R),
                (byte)(end.G - start.G),
                (byte)(end.B - start.B));

            // lumInt from 0 to 7
            for (var lumInt = 0; lumInt < 8; lumInt++)
            {
                var lumFrac = lumInt / 7.0;

                var ctup = new Rgb(
                    (byte)(start.R + diff.R * lumFrac),
                    (byte)(start.G + diff.G * lumFrac),
                    (byte)(start.B + diff.B * lumFrac));

                var colLumInd = (intKey << 3) + lumInt;
                _colLumToRGB8LUT[colLumInd] = ctup;
            }
        }
    }

    private byte ThreeBitLuminance
    {
        get
        {
            byte lum = 7;

            // If L0_lowCtrl is high, then the pad for the least significant bit of
            // luminance is pulled low, so subtract 1 from the luminance
            if (IsHigh(L0_lowCtrl))
            {
                lum -= 1;
            }

            // If L1_lowCtrl is high, then the pad for the twos bit of luminance
            // is pulled low, so subtract 2 from the luminance
            if (IsHigh(L1_lowCtrl))
            {
                lum -= 2;
            }

            // If the most significant bit is pulled low, subtract 4
            if (IsHigh(L2_lowCtrl))
            {
                lum -= 4;
            }

            return lum;
        }
    }

    private byte FourBitColor
    {
        get
        {
            byte col = 0;
            if (IsHigh(ColCnt_t0))
            {
                col += 1;
            }
            if (IsHigh(ColCnt_t1))
            {
                col += 2;
            }
            if (IsHigh(ColCnt_t2))
            {
                col += 4;
            }
            if (IsHigh(ColCnt_t3))
            {
                col += 8;
            }
            return col;
        }
    }

    public Rgba ColorRgba8
    {
        get
        {
            var lum = ThreeBitLuminance;
            var col = FourBitColor;

            // Lowest 4 bits of col, shift them 3 bits to the right,
            // and add the low 3 bits of luminance
            var index = ((col & 0xF) << 3) + (lum & 0x7);

            var rgb8Tuple = _colLumToRGB8LUT[index];
            return new Rgba(
                rgb8Tuple.R,
                rgb8Tuple.G,
                rgb8Tuple.B,
                0xFF);
        }
    }

    private record struct Rgb(byte R, byte G, byte B);
}

public record struct Rgba(byte R, byte G, byte B, byte A)
{
    public readonly int ToRgba8()
    {
        return (R << 24) + (G << 16) + (B << 8) + A;
    }
}
