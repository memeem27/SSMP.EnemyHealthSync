# SSMP.EnemyHealthSync

An addon for SSMP (Silksong Multiplayer) that adds enemy health synchronization between players.

## Features

- **Automatic Enemy Detection**: Detects ALL enemies with HealthManager - no registry needed!
- **Health Synchronization**: Syncs enemy health between all players
- **Damage Synchronization**: When any player damages an enemy, all players see the updated health
- **Death Synchronization**: When an enemy dies for the host, it dies for all clients
- **Movement Synchronization**: Enemies move identically for all players (via SSMP's existing system)
- **Works with ANY Enemy**: Automatically works with modded enemies too!

## How It Works

Instead of maintaining a list of enemy names, this addon **automatically finds all enemies**:

1. Uses `FindObjectsOfType<HealthManager>()` to find ALL enemies in the scene
2. Tracks their health values every frame
3. When health changes, sends update to server
4. Server relays to other clients
5. Clients apply health changes via reflection

**No enemy registry required!**

## Installation

1. Build the project with `dotnet build`
2. Copy `SSMPEnemyHealthSync.dll` to your `BepInEx/plugins/SSMP.EnemyHealthSync/` folder
3. Make sure SSMP is already installed

## Configuration

The mod can be configured via the BepInEx config file:

```ini
[General]
Enabled = true      # Enable/disable the mod
SyncHealth = true   # Enable/disable health/damage sync
SyncDeath = true    # Enable/disable death sync
```

## Finding Enemy Names (Optional - For Debug/Logging)

Since the addon auto-detects enemies, you don't need to add names manually. But if you want a list for debugging, look in your decompiled game assembly folder for files containing `HealthManager`. The class names (e.g., `MossBoneFly.cs`) usually match the GameObject names (e.g., "MossBone Fly").

## Architecture

This addon uses SSMP's existing entity system:

- **Client Side**: Tracks local enemy health changes and sends updates
- **Server Side**: Receives updates from scene host and relays to other clients
- **Scene Host**: The first player in a scene becomes the "host" for that scene's enemies

## Building

### Prerequisites

- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Hollow Knight: Silksong installed with BepInEx and SSMP
- Optional: Decompiled Assembly-CSharp for development

### Setup

1. Copy `SilksongPath.props.example` to `SilksongPath.props` and update the paths:
   ```powershell
   cp SilksongPath.props.example SilksongPath.props
   ```

2. Edit `SilksongPath.props` to point to your Silksong installation:
   ```xml
   <SilksongPluginsFolder>C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong\BepInEx\plugins</SilksongPluginsFolder>
   <SilksongGameFolder>C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong</SilksongGameFolder>
   ```

3. Build the project:
   ```bash
   dotnet build SSMP.EnemyHealthSync.sln
   ```

The DLL will be automatically copied to your plugins folder if the path is configured correctly.

### Using the Build Script

Alternatively, use the provided PowerShell script which creates the props file automatically:
```powershell
.\build.ps1
```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Credits

- Uses SSMP's addon API
- Built for Hollow Knight: Silksong
