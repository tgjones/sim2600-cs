# Sim2600-CS

Sim2600-CS is a C# port of [Sim2600](https://github.com/gregjames/Sim2600) by Greg James of Visual 6502 fame.

It is a transistor-level simulation of the Atari 2600 game console, written in C#. Rather than emulating the 
Atari 2600 at the instruction level, Sim2600-CS simulates the actual transistors and wiring of the original 
hardware chips — the 6507 CPU and TIA graphics chip — using reverse-engineered netlist data. The 6532 RIOT 
chip (RAM, I/O, Timer) is emulated at the register level.

A live web demo (compiled from C# to WASM) is available at https://tgjones.github.io/sim2600-cs/. Drop any 
Atari 2600 ROM file onto the page to run it. I say "any", but it really only works with 4KB cartridges, or 
8KB cartridges that use the same bank-switching mechanism as Asteroids. The ROMS 
[that are covered by tests](https://github.com/tgjones/sim2600-cs/blob/b96dc283b1b1696e3a1904b3be47177bed722e97/src/Sim2600.Tests/Tests.cs#L6-L10) 
are known to work:

* Adventure
* Asteroids
* Donkey Kong
* Pitfall
* Space Invaders

## How it works

The core of the simulator is `CircuitSimulatorBase`, which models every NMOS transistor and wire node in 
the original chips. At each half clock cycle, the simulator propagates signal state through the transistor 
network to determine the outputs of the CPU and TIA. This approach accurately reproduces the behavior of the 
original hardware — including any quirks — because it is derived from the actual circuit topology.

The netlist data for the 6502 (used as the basis for the 6507) and TIA chips originates from the 
[Visual6502](http://visual6502.org) project's reverse-engineering work, and the overall approach is based 
on the original Python [Sim2600](https://github.com/gregjames/Sim2600) project by Greg James, along with
a few optimizations.

## Build

**Requirements:** [.NET 10 SDK](https://dotnet.microsoft.com/download)

```sh
# Clone the repository
git clone https://github.com/tgjones/Sim2600-CS.git
cd Sim2600-CS

# Restore .NET workloads (required for the Blazor WebAssembly project)
cd src
dotnet workload restore

# Build all projects
dotnet build --configuration Release

# Run tests
dotnet test --configuration Release
```

Tests automatically download the required ROM files on first run. To avoid copyright violations, 
there are no ROMs included in the repository.

## Usage

### Sim2600.Web

The web interface runs in the browser via Blazor WebAssembly. To run it locally:

```sh
cd src/Sim2600.Web
dotnet watch run
```

Then open [http://localhost:5207](http://localhost:5207) in your browser.

Drop an Atari 2600 ROM file (`.bin`, `.a26`, or `.rom`) onto the drop zone, or click to browse for one. The simulation starts immediately and renders to a canvas at the native Atari 2600 resolution (228×262 pixels). The status bar at the bottom shows the half-cycle count, frame timing, and VSYNC/VBLANK indicators.

### Sim2600.Cli

The CLI project runs the simulator and writes rendered frames to PNG files. The ROM path is specified directly in [src/Sim2600.Cli/Program.cs](src/Sim2600.Cli/Program.cs):

```csharp
MainSim.Execute("/path/to/your/rom.bin");
```

After setting the path, run:

```sh
cd src
dotnet run --project Sim2600.Cli/Sim2600.Cli.csproj
```

PNG frames are written to disk each time the TIA signals VSYNC and a sufficiently complete frame has been accumulated.

## Credits

- [Greg James](https://github.com/gregjames/Sim2600) — original Python Sim2600 project, which this is based on
- [Visual6502](http://visual6502.org) — reverse-engineered netlists of the 6502 CPU and TIA chip
- [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) — PNG output in the CLI project
- [TUnit](https://github.com/thomhurst/TUnit) — test framework
