<h1 align="center">
    Odd AutoWalker
</h1>
<p align="center">
    <a href="https://github.com/approved/OddAutoWalker/actions?query=workflow%3A%22.NET+Core%22">
        <img src="https://img.shields.io/github/workflow/status/approved/OddAutoWalker/.NET%20Core/master?style=for-the-badge">
    </a>
    <a href="license">
        <img alt="GitHub" src="https://img.shields.io/github/license/approved/OddAutoWalker?style=for-the-badge">
    </a>
    <br>
    <b>
    Get a competitive edge and save your wrists in League of Legends. <br>
    This tool is designed to <a href="https://mobalytics.gg/blog/lol-attack-move-how-to-orb-walk/" title="Orb walking is where you auto attack a target but cancel or finish the animation early by entering a new command that interrupts it.">orb walk</a> optimally by calculating the amount of time it takes to complete an auto attack and automatically issuing both the move and attack commands.
    </b>
</p>

<p align="center">
    <img src="https://odd.dev/videos/league_kogmaw_autowalker.gif">
</p>

---

## How It Works

This project utilizes the [League of Legends Live Client API](https://developer.riotgames.com/docs/lol#game-client-api_live-client-data-api) to get your in-game stats to calculate the appropriate times to issue moves and attacks.
Using [LowLevelInput.Net](https://github.com/michel-pi/LowLevelInput.Net), `OddAutoWalker` is able to capture the user's input and know when to start issuing actions. The actions are hardware emulated using pinvoke and `SendInput` found in the [InputSimulator.cs](OddAutoWalker/InputSimulator.cs) class

---

## How To Get This Program

If you have not already, you will need to grab the latest version from here: [Latest Release](https://github.com/approved/OddAutoWalker/releases)

## Compilation

### Prerequisites
* Windows 10/11 x64
* .NET 9.0 SDK or later
* Visual Studio 2022 (optional, for IDE development)

### Basic Compilation

1. **Clone the repository**
   ```bash
   git clone https://github.com/approved/OddAutoWalker.git
   cd OddAutoWalker
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Build Release version**
   ```bash
   dotnet build -c Release
   ```

4. **Run the program**
   ```bash
   dotnet run -c Release
   ```

### Advanced Compilation (CPU Optimization)

If your CPU supports modern instruction sets (AVX-512, AVX2), you can create an optimized single-file executable:

```bash
# Clean previous builds
dotnet clean -c Release

# Publish optimized single-file executable
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:PublishTrimmed=true -p:TrimMode=partial
```

**Output location**: `bin\Release\net9.0\win-x64\publish\OddAutoWalker.exe`

### CPU Instruction Set Support

The program automatically detects and utilizes available CPU instruction sets:
- **AVX-512**: Intel Skylake-X+ or AMD Zen 4+ (best performance)
- **AVX2**: Intel Haswell+ or AMD Excavator+ (good performance)
- **SSE4.2**: Most modern CPUs (baseline performance)

### Compilation Options

| Option | Description |
|--------|-------------|
| `PublishSingleFile` | Creates a single executable file |
| `PublishReadyToRun` | Pre-compiles to native code for faster startup |
| `PublishTrimmed` | Removes unused code to reduce file size |
| `SelfContained` | Includes .NET runtime (no installation required) |

---

## Using This Program
<details>
    <summary>Important Note</summary>
    <p>
        <i>
            <b>
                While this program is usable, it is intended to be used as reference for both a better implementation and your own project.
                <br>
                <br>
                If you don't want to mess with the program yourself, you must have your "Player Attack Move" bound to 'A'. <br>
                This setting can be found in the in-game settings at Settings->Hotkeys->Player Movement.
            </b>
        </i>
    </p>
</details>

---

Steps:

1. Launch OddAutoWalker.exe and League of Legends
2. Queue up in any mode, excluding Team Fight Tactics, and wait until you're in game
3. Press and hold 'C' to activate the auto walker
4. Deactivate by releasing 'C'
