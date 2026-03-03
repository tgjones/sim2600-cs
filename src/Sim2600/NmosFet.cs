namespace Sim2600;

public sealed class NmosFet
{
    public const byte GATE_LOW  = 0;
    public const byte GATE_HIGH = 1 << 0;

    public readonly int Index;
    public readonly int Side1WireIndex;
    public readonly int Side2WireIndex;
    public readonly int GateWireIndex;

    public byte GateState { get; set; }

    public NmosFet(int idIndex, int side1WireIndex, int side2WireIndex, int gateWireIndex)
    {
        Index = idIndex;
        Side1WireIndex = side1WireIndex;
        Side2WireIndex = side2WireIndex;
        GateWireIndex = gateWireIndex;

        GateState = GATE_LOW;
    }

    public string ToDisplayString()
    {
        return $"Gate=({GateWireIndex}) C1=({Side1WireIndex}) C2=({Side2WireIndex})";
    }
}
