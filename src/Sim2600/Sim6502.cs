namespace Sim2600;

public sealed class Sim6502 : CircuitSimulatorBase
{
    private static readonly string[] AddressBusPadNames =
    [
        "AB0", "AB1",  "AB2",  "AB3",  "AB4",  "AB5",  "AB6",  "AB7",
        "AB8", "AB9", "AB10", "AB11", "AB12", "AB13", "AB14", "AB15"
    ];

    private static readonly string[] DataBusPadNames = ["DB0", "DB1", "DB2", "DB3", "DB4", "DB5", "DB6", "DB7"];

    public readonly List<int> AddressBusPads = [];
    public readonly List<int> DataBusPads = [];
    public readonly int PadIndRW, PadIndCLK0, PadIndRDY, PadIndCLK1Out, PadIndSYNC, PadReset;

    public Sim6502()
    {
        LoadCircuit(
            Net6502Data.NumWires, Net6502Data.NextCtrl, Net6502Data.NoWire,
            Net6502Data.WirePulled, Net6502Data.WireCtrlFets, Net6502Data.WireGates, Net6502Data.WireNames,
            Net6502Data.NumFets, Net6502Data.FetSide1WireInds, Net6502Data.FetSide2WireInds, Net6502Data.FetGateInds);

        // Store indices into the wireList.  This saves having
        // to look up the wires by their string name from the
        // wireNames dict.
        foreach (var padName in AddressBusPadNames)
        {
            AddressBusPads.Add(GetWireIndex(padName));
        }

        foreach (var padName in DataBusPadNames)
        {
            DataBusPads.Add(GetWireIndex(padName));
        }

        PadIndRW = GetWireIndex("R/W");
        PadIndCLK0 = GetWireIndex("CLK0");
        PadIndRDY = GetWireIndex("RDY");
        PadIndCLK1Out = GetWireIndex("CLK1OUT");
        PadIndSYNC = GetWireIndex("SYNC");
        PadReset = GetWireIndex("RES");
    }

    public ushort AddressBusValue
    {
        get
        {
            ushort value = 0;
            for (var i = 0; i < AddressBusPads.Count; i++)
            {
                if (IsHigh(AddressBusPads[i]))
                {
                    value |= (ushort)(1 << i);
                }
            }
            return value;
        }
    }

    public byte DataBusValue
    {
        get
        {
            byte value = 0;
            for (var i = 0; i < DataBusPads.Count; i++)
            {
                if (IsHigh(DataBusPads[i]))
                {
                    value |= (byte)(1 << i);
                }
            }
            return value;
        }
        set
        {
            Span<int> dataBusPads = stackalloc int[DataBusPads.Count];
            Span<bool> values = stackalloc bool[DataBusPads.Count];

            for (var i = 0; i < DataBusPads.Count; i++)
            {
                dataBusPads[i] = DataBusPads[i];
                values[i] = (value & (1 << i)) != 0;
            }
            SetPulled(dataBusPads, values);
        }
    }

    public void ResetChip()
    {
        Console.WriteLine("Starting 6502 reset sequence: pulling RES low");
        RecalcAllWires();
        SetLowWN("RES");
        SetHighWN("IRQ"); // No interrupt
        SetHighWN("NMI"); // No interrupt
        SetHighWN("RDY"); // Let the chip run. Will connect to TIA with pullup.
        for (var i = 0; i < 4; i++)
        {
            if (i % 2 != 0)
            {
                SetLowWN("CLK0");
            }
            else
            {
                SetHighWN("CLK0");
            }
        }

        Console.WriteLine("Setting 6502 RES high");
        SetHighWN("RES");

        Console.WriteLine("Finished 6502 reset sequence");
    }
}
