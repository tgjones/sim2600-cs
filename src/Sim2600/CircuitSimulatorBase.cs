using System.Collections;
using System.Diagnostics;
using System.Numerics;
using System.Text;

namespace Sim2600;

public abstract class CircuitSimulatorBase
{
    public string Name { get; set; }

    private Wire[] _wires;
    private NmosFet[] _transistors;
    private readonly Dictionary<string, int> _wireNames = [];
    public int HalfClkCount;
    private bool[] _recalcArray;
    private bool[] _newRecalcArray;
    private int[] _recalcOrder;
    private int[] _newRecalcOrder;
    private int _newLastRecalcOrder;
    private int _lastRecalcOrder;
    private int _vccWireIndex;
    private int _gndWireIndex;
    private BigInteger[] _lastWireGroupState;

    private BigInteger _lastChipGroupState = 0;
    private int[] _groupList;
    private int _groupListLastIndex = 0;

    [Flags]
    protected enum GroupState
    {
        ContainsNothing = 0,
        ContainsHi = 1 << 0,
        ContainsPulldown = 1 << 1,
        ContainsPullup = 1 << 2,
        ContainsPwr = 1 << 3,
        ContainsGnd = 1 << 4,
    }

    private GroupState _groupState;

    private ChipLogger _logger;

    public IDisposable BeginLogging(string fileName)
    {
        return new ChipLogger(this, fileName);
    }

    public void SetState(string state)
    {
        var index = 0;

        for (var i = 0; i < _wires.Length; i++)
        {
            var wire = _wires[i];

            if (i == _vccWireIndex || i == _gndWireIndex)
            {
                Debug.Assert(state[index] == 'H' || state[index] == 'G');
                continue;
            }

            wire.Pulled = state[index++] switch
            {
                '_' => NodePulled.Floating,
                'h' => NodePulled.PulledHigh,
                'l' => NodePulled.PulledLow,
                _ => throw new InvalidOperationException($"Invalid character")
            };

            wire.State = state[index++] switch
            {
                'l' => NodeState.PulledLow,
                'h' => NodeState.PulledHigh,
                'f' => NodeState.Floating,
                _ => throw new InvalidOperationException($"Invalid character")
            };
        }

        for (var i = 0; i < _transistors.Length; i++)
        {
            var transistor = _transistors[i];

            transistor.GateState = state[index++] switch
            {
                '-' => NmosFet.GATE_LOW,
                '~' => NmosFet.GATE_HIGH,
                _ => throw new InvalidOperationException($"Invalid character")
            };
        }

        if (index != state.Length)
        {
            throw new InvalidOperationException("State string length does not match the expected length.");
        }
    }

    public string GetState()
    {
        var result = new StringBuilder(_wires.Length);

        for (var i = 0; i < _wires.Length; i++)
        {
            var wire = _wires[i];

            result.Append(wire.Pulled switch
            {
                NodePulled.Floating => "_",
                NodePulled.PulledLow => "l",
                NodePulled.PulledHigh => "h",
                _ => throw new InvalidOperationException()
            });

            if (i == _vccWireIndex)
            {
                result.Append('H');
            }
            else if (i == _gndWireIndex)
            {
                result.Append('G');
            }
            else
            {
                result.Append(wire.State switch
                {
                    NodeState.PulledLow => "l",
                    NodeState.PulledHigh => "h",
                    NodeState.Floating => "f",
                    _ => throw new InvalidOperationException()
                });
            }
        }

        for (var i = 0; i < _transistors.Length; i++)
        {
            var transistor = _transistors[i];

            result.Append(transistor.GateState switch
            {
                NmosFet.GATE_LOW => "-",
                NmosFet.GATE_HIGH => "~",
                _ => throw new InvalidOperationException()
            });
        }

        return result.ToString();
    }

    public int GetWireIndex(string wireName) => _wireNames[wireName];

    protected void RecalcNamedWire(string wireName) => RecalcWireList([GetWireIndex(wireName)]);

    public void RecalcWireNameList(params string[] wireNames)
    {
        RecalcWireList(wireNames.Select(GetWireIndex));
    }

    protected void RecalcAllWires()
    {
        var wireIndices = new List<int>(_wires.Length);
        for (var i = 0; i < _wires.Length; i++)
        {
            if (_wires[i] != null && i != _vccWireIndex && i != _gndWireIndex)
            {
                wireIndices.Add(i);
            }
        }
        RecalcWireList(wireIndices);
    }

    private void PrepForRecalc()
    {
        if (_recalcArray == null)
        {
            var recalcCap = _transistors.Length;
            _recalcArray = new bool[recalcCap];
            _recalcOrder = new int[recalcCap];
            _newRecalcArray = new bool[recalcCap];
            _newRecalcOrder = new int[recalcCap];
        }

        _newLastRecalcOrder = 0;
        _lastRecalcOrder = 0;
    }

    public void RecalcWireList(IEnumerable<int> nwireList)
    {
        PrepForRecalc();

        foreach (var wireIndex in nwireList)
        {
            // recalcOrder is a list of wire indices.  self.lastRecalcOrder
            // marks the last index into this list that we should recalculate.
            // recalcArray has entries for all wires and is used to mark
            // which wires need to be recalcualted.
            _recalcOrder[_lastRecalcOrder++] = wireIndex;
            _recalcArray[wireIndex] = true;
        }

        DoRecalcIterations();
    }

    public void RecalcWire(int wireIndex)
    {
        PrepForRecalc();

        _recalcOrder[_lastRecalcOrder++] = wireIndex;
        _recalcArray[wireIndex] = true;

        DoRecalcIterations();
    }

    private void DoRecalcIterations()
    {
        // Simulation is not allowed to try more than 'stepLimit' 
        // iterations.  If it doesn't converge by then, raise an 
        // exception.
        var step = 0;
        const int stepLimit = 400;

        while (step < stepLimit)
        {
            if (_lastRecalcOrder == 0)
            {
                _logger?.End();
                break;
            }

            _logger?.BeginIteration(step);

            var i = 0;
            while (i < _lastRecalcOrder)
            {
                var wireIndex = _recalcOrder[i];
                
                // If wire has already been put into the newRecalcArray, then don't say that it needs to be recalculated again, since it's already been marked for recalculation.
                // This can happen when a wire is connected to multiple transistors that are switching on or off in the same iteration.
                var alreadyInNewArray = _newRecalcOrder.Take(_newLastRecalcOrder).Any(index => index == wireIndex);
                if (!alreadyInNewArray)
                    _newRecalcArray[wireIndex] = false;

                DoWireRecalc(wireIndex);

                _recalcArray[wireIndex] = false;
                i++;
            }

            _logger?.EndIteration();

            var tmp = _recalcArray;
            _recalcArray = _newRecalcArray;
            _newRecalcArray = tmp;

            var tmp2 = _recalcOrder;
            _recalcOrder = _newRecalcOrder;
            _newRecalcOrder = tmp2;

            _lastRecalcOrder = _newLastRecalcOrder;
            _newLastRecalcOrder = 0;

            step++;
        }

        // The first attempt to compute the state of a chip's circuit
        // may not converge, but it's enough to settle the chip into
        // a reasonable state so that when input and clock pulses are
        // applied, the simulation will converge.
        if (step >= stepLimit)
        {
            _logger?.End();

            // Don't raise an exception if this is the first attempt
            // to compute the state of a chip, but raise an exception if
            // the simulation doesn't converge any time other than that.
            if (HalfClkCount > 0)
            {
                throw new Exception($"Simulation {Name} did not converge after {stepLimit} iterations");
            }
        }

        // Check that we've properly reset the recalcArray.  All entries
        // should be zero in preparation for the next half clock cycle.
        // Only do this sanity check for the first clock cycles.
        if (HalfClkCount < 20)
        {
            var needNewArray = false;
            foreach (var recalc in _recalcArray)
            {
                if (recalc)
                {
                    needNewArray = true;
                    if (step < stepLimit)
                    {
                        throw new Exception($"At halfclk {HalfClkCount} after {step} iterations an entry in recalcArray is not False at the end of an update");
                    }
                }
            }
            if (needNewArray)
            {
                _recalcArray = new bool[_recalcArray.Length];
            }
        }
    }

    private void DoWireRecalc(int wireIndex)
    {
        if (wireIndex == _gndWireIndex || wireIndex == _vccWireIndex)
        {
            return;
        }

        _logger?.BeginRecalcNode(wireIndex);

        _lastChipGroupState++;
        _groupListLastIndex = 0;
        _groupState = GroupState.ContainsNothing;

        AddWireToGroupList(wireIndex);

        var newValue = NodeState.Floating;

        if ((_groupState & GroupState.ContainsGnd) != 0)
        {
            newValue = NodeState.PulledLow;
        }
        else if ((_groupState & GroupState.ContainsPwr) != 0)
        {
            newValue = NodeState.PulledHigh;
        }
        else if ((_groupState & GroupState.ContainsPulldown) != 0)
        {
            newValue = NodeState.PulledLow;
        }
        else if ((_groupState & GroupState.ContainsPullup) != 0)
        {
            newValue = NodeState.PulledHigh;
        }
        else if ((_groupState & GroupState.ContainsHi) != 0)
        {
            newValue = CountWireSizes();
        }

        var newHigh = newValue == NodeState.PulledHigh;

        _logger?.SetGroupState(_groupState, newValue);

        for (var i = 0; i < _groupListLastIndex; i++)
        {
            var index = _groupList[i];

            var simWire = _wires[index];
            simWire.State = newValue;

            // Turn on or off the transistor gates controlled by this wire
            if (newHigh)
            {
                foreach (var transIndex in simWire.GateIndices)
                {
                    var transistor = _transistors[transIndex];
                    if (transistor.GateState == NmosFet.GATE_LOW)
                    {
                        TurnTransistorOn(transistor);
                    }
                    else
                    {
                        _logger?.AddUnaffectedTransistor(transistor);
                    }
                }
            }
            else
            {
                foreach (var transIndex in simWire.GateIndices)
                {
                    var transistor = _transistors[transIndex];
                    if (transistor.GateState == NmosFet.GATE_HIGH)
                    {
                        TurnTransistorOff(transistor);
                    }
                    else
                    {
                        _logger?.AddUnaffectedTransistor(transistor);
                    }
                }
            }
        }

        _logger?.EndRecalcNode();
    }

    private void TurnTransistorOn(NmosFet transistor)
    {
        _logger?.AddAffectedTransistor(transistor, true);

        transistor.GateState = NmosFet.GATE_HIGH;

        AddRecalcNode(transistor.Side1WireIndex);
    }

    private void TurnTransistorOff(NmosFet transistor)
    {
        _logger?.AddAffectedTransistor(transistor, false);

        transistor.GateState = NmosFet.GATE_LOW;

        AddRecalcNode(transistor.Side1WireIndex);
        AddRecalcNode(transistor.Side2WireIndex);
    }

    private void AddRecalcNode(int wireInd)
    {
        if (!_newRecalcArray[wireInd])
        {
            _newRecalcArray[wireInd] = true;
            _newRecalcOrder[_newLastRecalcOrder++] = wireInd;
            _lastChipGroupState++;
        }
    }

    private void AddWireToGroupList(int wireIndex)
    {
        _logger?.AddNodeToGroup(wireIndex, _wires[wireIndex].State);

        // Do nothing if we've already added the wire to the group.
        if (_lastWireGroupState[wireIndex] == _lastChipGroupState)
        {
            return;
        }

        if (wireIndex == _gndWireIndex)
        {
            _groupState |= GroupState.ContainsGnd;
            return;
        }
        else if (wireIndex == _vccWireIndex)
        {
            _groupState |= GroupState.ContainsPwr;
            return;
        }

        _groupList[_groupListLastIndex++] = wireIndex;
        _lastWireGroupState[wireIndex] = _lastChipGroupState;

        var wire = _wires[wireIndex];

        // Wire.Pulled is 0, 1, or 2
        if (wire.Pulled == NodePulled.PulledHigh)
        {
            _groupState |= GroupState.ContainsPullup;
        }
        else if (wire.Pulled == NodePulled.PulledLow)
        {
            _groupState |= GroupState.ContainsPulldown;
        }
        else if (wire.State == NodeState.PulledHigh)
        {
            _groupState |= GroupState.ContainsHi;
        }

        foreach (var transIndex in wire.CTIndices)
        {
            // If the transistor at index 't' is on, add the
            // wires of the circuit on the other side of the 
            // transistor, since wireIndex is connected to them.
            var other = -1;
            var trans = _transistors[transIndex];
            if (trans.GateState == NmosFet.GATE_LOW)
            {
                continue;
            }

            if (trans.Side1WireIndex == wireIndex)
            {
                other = trans.Side2WireIndex;
            }
            else if (trans.Side2WireIndex == wireIndex)
            {
                other = trans.Side1WireIndex;
            }

            // No need to check if 'other' is already in the groupList:
            // self.groupList[0:self.groupListLastIndex]
            // That's done in the first line of addWireToGroupList()
            AddWireToGroupList(other);
        }
    }

    private NodeState CountWireSizes()
    {
        var maxState = NodeState.PulledLow;
        var maxConnections = 0;

        for (var i = 0; i < _groupListLastIndex; i++)
        {
            var wire = _wires[_groupList[i]];

            var num = wire.CTIndices.Count + wire.GateIndices.Count;
            if (num > maxConnections)
            {
                maxConnections = num;
                maxState = wire.State == NodeState.PulledHigh
                    ? NodeState.PulledHigh 
                    : NodeState.PulledLow;
            }
        }

        return maxState;
    }

    // setHighWN() and setLowWN() do not trigger an update
    // of the simulation.
    protected void SetHighWN(string n)
    {
        var wireIndex = _wireNames[n];
        _wires[wireIndex].SetHigh();
    }

    protected void SetLowWN(string n)
    {
        var wireIndex = _wireNames[n];
        _wires[wireIndex].SetLow();
    }

    public void SetHigh(int wireIndex)
    {
        _wires[wireIndex].SetPulledHighOrLow(true);
    }

    public void SetLow(int wireIndex)
    {
        _wires[wireIndex].SetPulledHighOrLow(false);
    }

    public void SetPulled(int wireIndex, bool high)
    {
        _wires[wireIndex].SetPulledHighOrLow(high);
    }

    public void SetPulledHigh(int wireIndex)
    {
        _wires[wireIndex].SetPulledHighOrLow(true);
    }

    public void SetPulledLow(int wireIndex)
    {
        _wires[wireIndex].SetPulledHighOrLow(false);
    }

    public bool IsHigh(int wireIndex) => _wires[wireIndex].IsHigh();

    public bool IsLow(int wireIndex) => _wires[wireIndex].IsLow();

    public bool IsHighWN(string n) => _wires[_wireNames[n]].IsHigh();

    public bool IsLowWN(string n) => _wires[_wireNames[n]].IsLow();

    protected void LoadCircuit(string filePath)
    {
        var unpickler = new Razorvine.Pickle.Unpickler();

        using var fileStream = File.OpenRead(filePath);

        var rootObj = (Hashtable)unpickler.load(fileStream);

        var numWires = (int)rootObj["NUM_WIRES"];
        var nextCtrl = (int)rootObj["NEXT_CTRL"];
        var noWire = (int)rootObj["NO_WIRE"];
        var wirePulled = (byte[])rootObj["WIRE_PULLED"];
        var wireCtrlFets = (int[])rootObj["WIRE_CTRL_FETS"];
        var wireGates = (int[])rootObj["WIRE_GATES"];
        var wireNames = (ArrayList)rootObj["WIRE_NAMES"];
        var numFets = (int)rootObj["NUM_FETS"];
        var fetSide1WireInds = (int[])rootObj["FET_SIDE1_WIRE_INDS"];
        var fetSide2WireInds = (int[])rootObj["FET_SIDE2_WIRE_INDS"];
        var fetGateWireInds = (int[])rootObj["FET_GATE_INDS"];

        Debug.Assert(wirePulled.Length == numWires);
        Debug.Assert(wireNames.Count == numWires);
        Debug.Assert(fetSide1WireInds.Length == numFets);
        Debug.Assert(fetSide2WireInds.Length == numFets);
        Debug.Assert(fetGateWireInds.Length == numFets);

        _wires = new Wire[numWires];

        var wcfi = 0;
        var wgi = 0;
        for (var i = 0; i < numWires; i++)
        {
            var numControlFets = wireCtrlFets[wcfi++];
            var controlFets = new List<int>();

            for (var n = 0; n < numControlFets; n++)
            {
                if (!controlFets.Contains(wireCtrlFets[wcfi]))
                    controlFets.Add(wireCtrlFets[wcfi]);
                wcfi++;
            }

            var tok = wireCtrlFets[wcfi++];
            Debug.Assert(tok == nextCtrl);

            var numGates = wireGates[wgi++];
            var gates = new List<int>();
            for (var n = 0; n < numGates; n++)
            {
                if (!gates.Contains(wireGates[wgi]))
                    gates.Add(wireGates[wgi]);
                wgi++;
            }

            tok = wireGates[wgi++];
            Debug.Assert(tok == nextCtrl);

            if (wireCtrlFets.Length == 0 && gates.Count == 0)
            {
                Debug.Assert(wireNames[i] == "");
            }
            else
            {
                var wirePulledValue = wirePulled[i] switch
                {
                    0 => NodePulled.Floating,
                    1 => NodePulled.PulledHigh,
                    2 => NodePulled.PulledLow,
                    _ => throw new InvalidOperationException()
                };
                _wires[i] = new Wire(i, (string)wireNames[i], wirePulledValue);
                _wireNames[(string)wireNames[i]] = i;
            }
        }

        _transistors = new NmosFet[numFets];

        _vccWireIndex = _wireNames["VCC"];
        _gndWireIndex = _wireNames["VSS"];

        for (var i = 0; i < numFets; i++)
        {
            var s1 = fetSide1WireInds[i];
            var s2 = fetSide2WireInds[i];
            var gate = fetGateWireInds[i];

            if (s1 == noWire)
            {
                Debug.Assert(s2 == noWire);
                Debug.Assert(gate == noWire);
            }
            else
            {
                if (s1 == _gndWireIndex) { s1 = s2; s2 = _gndWireIndex; }
                else if (s1 == _vccWireIndex) { s1 = s2; s2 = _vccWireIndex; }
                
                _transistors[i] = new NmosFet(i, s1, s2, gate);

                _wires[gate].GateIndices.Add(i);
                _wires[s1].CTIndices.Add(i);
                _wires[s2].CTIndices.Add(i);
            }
        }

        _wires[_vccWireIndex].State = NodeState.PulledHigh;
        _wires[_gndWireIndex].State = NodeState.PulledLow;

        foreach (var transInd in _wires[_vccWireIndex].GateIndices)
        {
            _transistors[transInd].GateState = NmosFet.GATE_HIGH;
        }

        _lastWireGroupState = new BigInteger[numWires];

        _groupList = new int[_wires.Length];
    }

    private sealed class ChipLogger : IDisposable
    {
        private readonly CircuitSimulatorBase _chipSimulator;
        private readonly StreamWriter _streamWriter;

        private readonly List<int> _nodes = new();
        private readonly List<RecalcNodesIteration> _iterations = new();
        private RecalcNodesIteration? _currentIteration;
        private RecalcNode? _currentRecalcNode;
        private int? _nextNodeTransistorGate;

        private int _indentLevel;

        public ChipLogger(CircuitSimulatorBase chipSimulator, string filePath)
        {
            _chipSimulator = chipSimulator;
            _streamWriter = new StreamWriter(filePath);

            chipSimulator._logger = this;
        }

        public void Begin() { }

        public void BeginIteration(int iteration)
        {
            _currentIteration = new RecalcNodesIteration(iteration);
        }

        public void BeginRecalcNode(int nodeId)
        {
            _currentRecalcNode = new RecalcNode(nodeId);
        }

        public void AddNodeToGroup(int nodeId, NodeState currentValue)
        {
            if (!_currentRecalcNode!.Group.Any(x => x.Item1 == nodeId))
            {
                _currentRecalcNode!.Group.Add((nodeId, _nextNodeTransistorGate, currentValue));
            }
        }

        public void SetNextNodeTransistorGate(int? gate)
        {
            _nextNodeTransistorGate = gate;
        }

        public void SetGroupState(GroupState currentGroupState, NodeState newGroupState)
        {
            _currentRecalcNode!.CurrentGroupState = currentGroupState;
            _currentRecalcNode!.NewGroupState = newGroupState;
        }

        public void AddAffectedTransistor(NmosFet transistor, bool turnedOn)
        {
            _currentRecalcNode!.AffectedTransistors.Add((transistor, turnedOn));
        }

        public void AddUnaffectedTransistor(NmosFet transistor)
        {
            _currentRecalcNode!.UnaffectedTransistors.Add(transistor);
        }

        public void EndRecalcNode()
        {
            _currentIteration!.RecalcNodes.Add(_currentRecalcNode!);
            _currentRecalcNode = null;
        }

        public void EndIteration()
        {
            _iterations.Add(_currentIteration!);
            _currentIteration = null;
        }

        public void End()
        {
            // Search backwards through iterations for everything that affected _nodeToTrace

            List<RecalcNodesIteration> filteredIterations = _iterations;

            // Write it to log.

            foreach (var iteration in filteredIterations)
            {
                WriteLine($"Iteration {iteration.Iteration}");
                PushIndentLevel();
                foreach (var recalcNode in iteration.RecalcNodes)
                {
                    WriteLine($"Recalc Node {GetNodeName(recalcNode.NodeId)}:");
                    PushIndentLevel();

                    WriteLine($"Group Nodes:");
                    PushIndentLevel();
                    foreach (var (groupNodeId, viaGateId, groupNodeValue) in recalcNode.Group)
                    {
                        var transistorGateSuffix = viaGateId != null ? $" (via transistor gate {GetNodeName(viaGateId.Value)})" : "";
                        WriteLine($"- {GetNodeName(groupNodeId)}: {groupNodeValue}{transistorGateSuffix}");
                    }
                    PopIndentLevel();

                    WriteLine($"Current Group State: {recalcNode.CurrentGroupState}");
                    WriteLine($"New Group State:     {recalcNode.NewGroupState}");

                    WriteLine("Unaffected Transistors:");
                    PushIndentLevel();
                    foreach (var transistor in recalcNode.UnaffectedTransistors)
                    {
                        WriteLine($"- Transistor: {transistor.ToDisplayString()}");
                    }
                    PopIndentLevel();

                    WriteLine("Affected Transistors:");
                    PushIndentLevel();
                    foreach (var affectedTransistor in recalcNode.AffectedTransistors)
                    {
                        var (transistor, turnedOn) = affectedTransistor;
                        WriteLine($"- Transistor {(turnedOn ? "On" : "Off")}: {transistor.ToDisplayString()}");
                    }
                    PopIndentLevel();

                    PopIndentLevel();
                }
                PopIndentLevel();
            }

            WriteLine("****************");

            _streamWriter.Flush();

            _iterations.Clear();
            _nodes.Clear();
        }

        private string GetNodeName(int nodeId)
        {
            return $"({nodeId})";
        }

        private void WriteLine(string line)
        {
            _streamWriter.WriteLine(new string(' ', _indentLevel * 4) + line);
        }

        private void PushIndentLevel() => _indentLevel++;

        private void PopIndentLevel() => _indentLevel--;

        public void Dispose()
        {
            _streamWriter.Flush();
            _streamWriter.Dispose();
            _chipSimulator._logger = null;
        }

        private sealed class RecalcNodesIteration(int iteration)
        {
            public List<RecalcNode> RecalcNodes = [];

            public int Iteration => iteration;
        }

        private sealed class RecalcNode(int nodeId)
        {
            public int NodeId => nodeId;
            public List<(int, int?, NodeState)> Group { get; } = [];
            public GroupState CurrentGroupState { get; set; }
            public NodeState NewGroupState { get; set; }
            public List<(NmosFet, bool)> AffectedTransistors { get; } = [];
            public List<NmosFet> UnaffectedTransistors { get; } = [];
        }
    }
}
