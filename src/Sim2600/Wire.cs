namespace Sim2600;

public sealed class Wire
{
    public const byte PULLED_HIGH = 1 << 0;
    public const byte PULLED_LOW = 1 << 1;
    public const byte FLOATING_HIGH = 1 << 4;
    public const byte FLOATING_LOW = 1 << 5;

    private readonly int _index;
    private readonly string _name;

    /// <summary>
    /// Transistors that switch other wires into connection with this wire
    /// </summary>
    public readonly int[] CTIndices;

    /// <summary>
    /// Transistors whos gate is driven by this wire
    /// </summary>
    public readonly int[] GateIndices;

    /// <summary>
    /// pulled reflects whether or not the wire is connected to
    /// a pullup or pulldown.
    /// </summary>
    public byte Pulled { get; set; }

    /// <summary>
    /// state reflects the logical state of the wire as the 
    /// simulation progresses.
    /// </summary>
    public byte State { get; set; }

    public Wire(int idIndex, string name, int[] controlTransIndices, int[] transGateIndices, byte pulled)
    {
        _index = idIndex;
        _name = name;
        CTIndices = controlTransIndices;
        GateIndices = transGateIndices;

        Pulled = pulled;
    }

    /// <summary>
    /// Used to pin a pad or external input high
    /// </summary>
    public void SetHigh()
    {
        Pulled = State = PULLED_HIGH;
    }

    /// <summary>
    /// Used to pin a pad or external input low
    /// </summary>
    public void SetLow()
    {
        Pulled = State = PULLED_LOW;
    }

    /// <summary>
    /// Used to pin a pad or external input high or low
    /// </summary>
    /// <param name="high"></param>
    public void SetPulledHighOrLow(bool high)
    {
        if (high)
        {
            SetHigh();
        }
        else
        {
            SetLow();
        }
    }

    public bool IsHigh() => State switch
    {
        FLOATING_HIGH => true,
        PULLED_HIGH => true,
        _ => false
    };

    public bool IsLow() => State switch
    {
        FLOATING_LOW => true,
        PULLED_LOW => true,
        _ => false
    };
}