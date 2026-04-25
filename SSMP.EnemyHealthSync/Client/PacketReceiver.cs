using UnityEngine;
using SSMP.Networking.Packet;
using SSMPEnemyHealthSync.Networking;

namespace SSMPEnemyHealthSync.Client;

internal static class PacketReceiver
{
    internal static void Init()
    {
        // Register packet handler for receiving health updates from server
        if (Client.networkReceiver != null)
        {
            Client.networkReceiver.RegisterPacketHandler<EnemyHealthPacket>(
                PacketId.EnemyHealthUpdate,
                OnHealthPacketReceived);
        }

        Debug.Log("[SSMP.EnemyHealthSync] PacketReceiver initialized [v2-20250416]");
    }

    /// <summary>
    /// Handle health update packet from server
    /// </summary>
    private static void OnHealthPacketReceived(EnemyHealthPacket packet)
    {
        // Validate packet data - check for corruption
        if (packet.EntityId == 0 || packet.MaxHealth > 10000 || packet.Health < -100000)
        {
            Debug.LogWarning($"[SSMP.EnemyHealthSync] CORRUPTED PACKET DETECTED! Entity={packet.EntityId}, Health={packet.Health}, Max={packet.MaxHealth}, IsDead={packet.IsDead}, Dmg={packet.DamageDealt}");
            return;
        }
        
        Debug.Log($"[SSMP.EnemyHealthSync] Client received health update for entity {packet.EntityId}: {packet.Health}/{packet.MaxHealth}, isDead={packet.IsDead}");

        EnemyHealthSyncManager.HandleEnemyHealthUpdate(packet.EntityId, packet.Health, packet.MaxHealth, packet.IsDead);
    }

    internal static void OnEnemyHealthUpdate(ushort entityId, int health, int maxHealth, bool isDead)
    {
        Debug.Log($"[SSMP.EnemyHealthSync] Received health update for entity {entityId}: {health}/{maxHealth}, dead={isDead}");
        EnemyHealthSyncManager.HandleEnemyHealthUpdate(entityId, health, maxHealth, isDead);
    }
}
