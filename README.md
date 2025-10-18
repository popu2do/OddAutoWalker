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

To Compile Yourself:

* Your computer must be running Windows 10 version 1703 or higher
* Download the latest version of [Visual Studio](https://visualstudio.microsoft.com/downloads/) (VS)
* Make sure the .NET Core 3.1 SDK is installed if it was not installed with VS
* Clone or Download the Source
* Open `OddAutoWalker.sln` with VS to build and run the project

### Build Commands

```bash
# Restore packages
dotnet restore

# Build the project
dotnet build --configuration Release

# Create single file executable
dotnet publish --configuration Release --runtime win10-x64 --self-contained true --property:PublishSingleFile=true
```

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
3. **Short press 'C'** - Executes one complete walk-attack cycle (attack → wait for windup+buffer → move)
4. **Hold 'C'** - Continuous orb walking until released
5. Deactivate by releasing 'C'

### Usage Tips:

- **Low attack speed heroes (0.6-1.0)**: Use short press for precise positioning and skill dodging
- **High attack speed heroes (2.5+)**: Use hold for smooth orb walking experience
- The tool automatically calculates optimal timing based on your champion's attack speed

---

## Configuration

Available settings in `settings/settings.json`:

- `ActivationKey` - Key code for activating orb walk (default: 67 = C key)
- `MaxCheckFrequencyHz` - Maximum tick frequency for checking attack/move timing (default: 120Hz)
- `MinMoveCommandIntervalSeconds` - Minimum interval between move commands (default: 0.01s)
- `MaxMoveCommandIntervalSeconds` - Maximum interval between move commands (default: 0.15s)

---

## Improvements

This fork enhances the original [approved/OddAutoWalker](https://github.com/approved/OddAutoWalker) with:

- **Dynamic Tick System** - Adjusts check frequency based on attack speed for precise timing
- **Orb Walk Move Interval** - Calculates move command frequency based on attack speed to prevent disconnection
- **Complete Walk-Attack Cycle** - Short press executes full attack-wait-move cycle, eliminating initial delay

---

## Credits

Based on [approved/OddAutoWalker](https://github.com/approved/OddAutoWalker) by [approved](https://github.com/approved)