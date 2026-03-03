namespace Sim2600;

public sealed class Wire
{
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
    public NodePulled Pulled { get; set; }

    /// <summary>
    /// state reflects the logical state of the wire as the 
    /// simulation progresses.
    /// </summary>
    public NodeState State { get; set; }

    public Wire(int idIndex, string name, int[] controlTransIndices, int[] transGateIndices, NodePulled pulled)
    {
        _index = idIndex;
        _name = name;
        CTIndices = controlTransIndices;
        GateIndices = transGateIndices;

        Pulled = pulled;
        State = pulled switch
        {
            NodePulled.Floating => NodeState.FloatingLow,
            NodePulled.PulledLow => NodeState.FloatingLow,
            NodePulled.PulledHigh => NodeState.FloatingHigh,
            _ => throw new Exception($"Unexpected Pulled value {pulled}")
        };
    }

    /// <summary>
    /// Used to pin a pad or external input high
    /// </summary>
    public void SetHigh()
    {
        Pulled = NodePulled.PulledHigh;
        State = NodeState.PulledHigh;
    }

    /// <summary>
    /// Used to pin a pad or external input low
    /// </summary>
    public void SetLow()
    {
        Pulled = NodePulled.PulledLow;
        State = NodeState.PulledLow;
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
        NodeState.PulledHigh => true,
        NodeState.FloatingHigh => true,
        _ => false
    };

    public bool IsLow() => State switch
    {
        NodeState.PulledLow => true,
        NodeState.FloatingLow => true,
        _ => false
    };
}

public enum NodePulled : byte
{
    Floating,
    PulledLow,
    PulledHigh,
}

public enum NodeState : byte
{
    PulledLow,
    PulledHigh,
    FloatingLow,
    FloatingHigh,
}