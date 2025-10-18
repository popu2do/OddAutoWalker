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

## Configuration

Available settings in `settings/settings.json`:

- `ActivationKey` - Key code for activating orb walk (default: 67 = C key)
- `HighAttackSpeedThreshold` - High attack speed threshold for maximum move frequency (default: 3.0)
- `LowAttackSpeedThreshold` - Low attack speed threshold for minimum move frequency (default: 1.2)

### Dynamic Move Frequency Algorithm

The tool uses a linear interpolation algorithm to adjust move command frequency based on attack speed:

- **Low Attack Speed (≤1.2)**: 10Hz move frequency (100ms interval)
- **High Attack Speed (≥3.0)**: 30Hz move frequency (33.33ms interval)  
- **Medium Attack Speed (1.2-3.0)**: Linear interpolation between 10Hz and 30Hz

This prevents excessive move commands at low attack speeds while maintaining responsiveness at high attack speeds.

---

## Improvements

This fork enhances the original [approved/OddAutoWalker](https://github.com/approved/OddAutoWalker) with:

- **Dynamic Move Frequency Control** - Adjusts move command frequency based on attack speed using linear interpolation
- **Attack Speed-Based Optimization** - Reduces move spam at low attack speeds, maintains responsiveness at high attack speeds
- **UAC Administrator Privileges** - Added application manifest to request administrator privileges for process access

---

## Credits

Based on [approved/OddAutoWalker](https://github.com/approved/OddAutoWalker) by [approved](https://github.com/approved)