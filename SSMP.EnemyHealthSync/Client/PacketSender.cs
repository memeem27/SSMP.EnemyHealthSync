using UnityEngine;
using SSMPEnemyHealthSync.Networking;

namespace SSMPEnemyHealthSync.Client;

internal static class PacketSender
{
    internal static void Init()
    {
        Debug.Log("[SSMP.EnemyHealthSync] PacketSender initialized");
    }

    internal static void SendSceneEntered(string sceneName)
    {
        if (Client.api?.NetClient?.IsConnected != true)
        {
            return;
        }

        if (Client.networkSender == null)
        {
            return;
        }

        var packet = new SceneEnteredPacket
        {
            SceneName = sceneName ?? ""
        };

        try
        {
            Client.networkSender.SendSingleData(PacketId.SceneEntered, packet);
        }
        catch
        {
        }
    }

    /// <summary>
    /// Send enemy health update to server (any player can send - server validates)
    /// </summary>
    internal static void SendEnemyHealthUpdate(ushort entityId, int health, int maxHealth, bool isDead, int damageDealt)
    {
        // Check if client is connected
        if (Client.api?.NetClient?.IsConnected != true)
        {
            Debug.LogWarning("[SSMP.EnemyHealthSync] Cannot send - client not connected to server");
            return;
        }

        // Only send if we have a network sender
        if (Client.networkSender == null)
        {
            Debug.LogWarning("[SSMP.EnemyHealthSync] Cannot send health update - networkSender is null!");
            return;
        }

        var packet = new EnemyHealthPacket
        {
            EntityId = entityId,
            Health = health,
            MaxHealth = maxHealth,
            IsDead = isDead,
            DamageDealt = damageDealt
        };

        Debug.Log($"[SSMP.EnemyHealthSync] About to send health update for entity {entityId}: {health}/{maxHealth}");
        
        try
        {
            Client.networkSender.SendSingleData(PacketId.EnemyHealthUpdate, packet);
            Debug.Log($"[SSMP.EnemyHealthSync] Successfully sent health update for entity {entityId}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SSMP.EnemyHealthSync] Failed to send health update: {ex.Message}");
        }
    }
}
