# SSMP.EnemyHealthSync - Simple Approach

## How It Works (No Registry Needed!)

Instead of maintaining a list of enemy names, this addon **automatically detects ALL enemies**:

1. **Scene Load**: When you enter a room, it finds ALL GameObjects with `HealthManager` component
2. **Auto-Tracking**: Any object with health gets tracked automatically
3. **Health Sync**: When health changes, it syncs to other players
4. **No Names Needed**: Works with ANY enemy - even ones we don't know about yet

## The Addon Files

| File | Purpose |
|------|---------|
| `SSMPEnemyHealthSyncPlugin.cs` | Main plugin, hooks into BepInEx |
| `Client/EnemyHealthSyncManager.cs` | Auto-detects and tracks ALL enemies |
| `Client/PacketSender.cs` | Sends health updates to server |
| `Client/PacketReceiver.cs` | Receives health updates from server |
| `Server/PacketHandler.cs` | Server-side relay of health updates |

## How to Build

1. Copy `SilksongPath.props.example` to `SilksongPath.props` and update the paths:
```powershell
cp SilksongPath.props.example SilksongPath.props
```

2. Edit `SilksongPath.props` to point to your Silksong installation:
```xml
<Project>
  <PropertyGroup>
    <SilksongPluginsFolder>C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong\BepInEx\plugins</SilksongPluginsFolder>
    <SilksongGameFolder>C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong</SilksongGameFolder>
  </PropertyGroup>
</Project>
```

3. Build:
```powershell
dotnet build SSMP.EnemyHealthSync.sln
```

4. The DLL will be copied to `BepInEx\plugins\SSMP.EnemyHealthSync\`

## What You Need from SSMP

The addon uses SSMP's API to:
- Register as an addon (`ClientAddon.RegisterAddon`)
- Send/receive network packets
- Know which player is scene host

SSMP needs to expose these APIs (they should already exist based on SSMP.Essentials).

## Testing

1. Host a game with Player 1
2. Join with Player 2
3. Both enter the same room
4. Both should see the same enemies
5. When Player 1 hits an enemy, Player 2 sees the health change
6. When enemy dies, it dies for both players

## Config Options (BepInEx Config)

```ini
[General]
Enabled = true      # Master toggle - enable/disable the mod
SyncHealth = true   # Sync enemy health/damage between players
SyncDeath = true    # Sync enemy death state between players
```
