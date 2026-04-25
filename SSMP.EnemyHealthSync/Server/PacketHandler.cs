using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SSMP.Networking.Packet;
using SSMPEnemyHealthSync.Networking;

namespace SSMPEnemyHealthSync.Server;

/// <summary>
/// Server-side packet handling for enemy sync.
/// Relays health updates from scene host to other clients.
/// </summary>
internal static class PacketHandler
{
    // Track which player is host for which scene/entities
    private static readonly Dictionary<ushort, ushort> _entityHostMap = new();

    private static readonly Dictionary<ushort, string> _playerLastScene = new();
    private static readonly Dictionary<string, HashSet<ushort>> _deadEntitiesByScene = new();

    private sealed class EntityState
    {
        internal int Health;
        internal int MaxHealth;
        internal bool IsDead;
    }

    private static readonly Dictionary<ushort, EntityState> _entityStateMap = new();

    internal static void Init()
    {
        // Register packet handler for receiving health updates from clients
        if (Server.networkReceiver != null)
        {
            Server.networkReceiver.RegisterPacketHandler<EnemyHealthPacket>(
                PacketId.EnemyHealthUpdate,
                OnHealthPacketReceived);
            Server.networkReceiver.RegisterPacketHandler<SceneEnteredPacket>(
                PacketId.SceneEntered,
                OnSceneEnteredReceived);
            Debug.Log("[SSMP.EnemyHealthSync] Server packet handler registered successfully");
        }
        else
        {
            Debug.LogWarning("[SSMP.EnemyHealthSync] Server networkReceiver is null, cannot register handler!");
        }

        Debug.Log("[SSMP.EnemyHealthSync] Server PacketHandler initialized");
    }

    /// <summary>
    /// Handle health update packet received from a client
    /// </summary>
    private static void OnHealthPacketReceived(ushort fromPlayerId, EnemyHealthPacket packet)
    {
        Debug.Log($"[SSMP.EnemyHealthSync] Server received health update from player {fromPlayerId} for entity {packet.EntityId}: {packet.Health}/{packet.MaxHealth}, isDead={packet.IsDead}");

        var sender = Server.api.ServerManager.Players.FirstOrDefault(p => p.Id == fromPlayerId);
        if (sender == null)
        {
            Debug.LogWarning($"[SSMP.EnemyHealthSync] Cannot find sender player {fromPlayerId}");
            return;
        }

        var senderScene = sender.CurrentScene;
        if (senderScene != null)
        {
            if (!_playerLastScene.TryGetValue(fromPlayerId, out var lastScene) || lastScene != senderScene)
            {
                _playerLastScene[fromPlayerId] = senderScene;
                SendDeadSnapshotToPlayer(fromPlayerId, senderScene);
            }

            CleanupSceneCaches();
        }
        
        // Verify this player is the host for this entity
        if (_entityHostMap.TryGetValue(packet.EntityId, out var hostId) && hostId != fromPlayerId)
        {
            if (_entityStateMap.TryGetValue(packet.EntityId, out var prev))
            {
                // Allow non-host updates only if they ADVANCE state (more damage or death).
                // This lets P2 kill an enemy and have it propagate, even if P1 was first "host".
                bool advances = packet.IsDead || packet.Health <= prev.Health;
                if (!advances)
                {
                    Debug.LogWarning($"[SSMP.EnemyHealthSync] Rejecting non-host update from {fromPlayerId} for entity {packet.EntityId} (host {hostId}) because it looks like a heal: {packet.Health}>{prev.Health}");
                    return;
                }
            }
        }

        // Set host if not already set
        if (!_entityHostMap.ContainsKey(packet.EntityId))
        {
            _entityHostMap[packet.EntityId] = fromPlayerId;
        }

        // Update server-side remembered state
        if (!_entityStateMap.TryGetValue(packet.EntityId, out var state))
        {
            state = new EntityState();
            _entityStateMap[packet.EntityId] = state;
        }

        state.Health = packet.Health;
        state.MaxHealth = packet.MaxHealth;
        state.IsDead = packet.IsDead;

        if (senderScene != null && (packet.IsDead || packet.Health <= 0))
        {
            if (!_deadEntitiesByScene.TryGetValue(senderScene, out var set))
            {
                set = new HashSet<ushort>();
                _deadEntitiesByScene[senderScene] = set;
            }

            set.Add(packet.EntityId);
        }

        // Relay to all other clients in the same scene
        RelayHealthUpdate(fromPlayerId, packet);
    }

    private static void OnSceneEnteredReceived(ushort fromPlayerId, SceneEnteredPacket packet)
    {
        var sceneName = packet.SceneName ?? "";
        if (sceneName.Length == 0)
        {
            return;
        }

        _playerLastScene[fromPlayerId] = sceneName;
        CleanupSceneCaches();
        SendDeadSnapshotToPlayer(fromPlayerId, sceneName);
    }

    private static void SendDeadSnapshotToPlayer(ushort playerId, string sceneName)
    {
        if (Server.networkSender == null)
        {
            return;
        }

        if (!_deadEntitiesByScene.TryGetValue(sceneName, out var dead) || dead.Count == 0)
        {
            return;
        }

        foreach (var entityId in dead)
        {
            int maxHp = 0;
            if (_entityStateMap.TryGetValue(entityId, out var state))
            {
                maxHp = state.MaxHealth;
            }

            var pkt = new EnemyHealthPacket
            {
                EntityId = entityId,
                Health = 0,
                MaxHealth = maxHp,
                IsDead = true,
                DamageDealt = 0
            };

            Server.networkSender.SendSingleData(PacketId.EnemyHealthUpdate, pkt, playerId);
        }
    }

    private static void CleanupSceneCaches()
    {
        var activeScenes = Server.api.ServerManager.Players
            .Select(p => p.CurrentScene)
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .ToHashSet();

        if (activeScenes.Count == 0)
        {
            _deadEntitiesByScene.Clear();
            return;
        }

        var scenesToRemove = _deadEntitiesByScene.Keys.Where(s => !activeScenes.Contains(s)).ToArray();
        foreach (var s in scenesToRemove)
        {
            _deadEntitiesByScene.Remove(s);
        }
    }

    /// <summary>
    /// Relay health update to all clients in the same scene except the sender
    /// </summary>
    private static void RelayHealthUpdate(ushort fromPlayerId, EnemyHealthPacket packet)
    {
        if (Server.networkSender == null)
        {
            Debug.LogWarning("[SSMP.EnemyHealthSync] Cannot relay - networkSender is null!");
            return;
        }

        // Get the sender's scene to only relay to players in the same scene
        var sender = Server.api.ServerManager.Players.FirstOrDefault(p => p.Id == fromPlayerId);
        if (sender == null)
        {
            Debug.LogWarning($"[SSMP.EnemyHealthSync] Cannot find sender player {fromPlayerId}");
            return;
        }
        
        var senderScene = sender.CurrentScene;

        // Get player IDs in the SAME SCENE as the sender, excluding the sender
        var targetPlayerIds = Server.api.ServerManager.Players
            .Where(p => p.Id != fromPlayerId && p.CurrentScene == senderScene)
            .Select(p => p.Id)
            .ToArray();

        if (targetPlayerIds.Length == 0)
        {
            Debug.Log($"[SSMP.EnemyHealthSync] No other clients in scene '{senderScene}' to relay to for entity {packet.EntityId}");
            return;
        }

        Debug.Log($"[SSMP.EnemyHealthSync] SERVER: Relaying entity {packet.EntityId} health {packet.Health}/{packet.MaxHealth} to players in scene '{senderScene}' [{string.Join(",", targetPlayerIds)}] (excluded sender {fromPlayerId})");

        // Send to specific players only (NOT broadcast - that would echo back to sender)
        Server.networkSender.SendSingleData(PacketId.EnemyHealthUpdate, packet, targetPlayerIds);

        Debug.Log($"[SSMP.EnemyHealthSync] Relayed health update for entity {packet.EntityId} from player {fromPlayerId} to {targetPlayerIds.Length} other clients in scene '{senderScene}'");
    }

    /// <summary>
    /// Called when a player leaves - clear their hosted entities
    /// </summary>
    internal static void OnPlayerDisconnect(ushort playerId)
    {
        var entitiesToRemove = new List<ushort>();

        foreach (var kvp in _entityHostMap)
        {
            if (kvp.Value == playerId)
            {
                entitiesToRemove.Add(kvp.Key);
            }
        }

        foreach (var entityId in entitiesToRemove)
        {
            _entityHostMap.Remove(entityId);
            Debug.Log($"[SSMP.EnemyHealthSync] Cleared entity {entityId} host (player {playerId} disconnected)");
        }
    }

    /// <summary>
    /// Clear all entity hosts (e.g., when server shuts down)
    /// </summary>
    internal static void Clear()
    {
        _entityHostMap.Clear();
        _entityStateMap.Clear();
        _playerLastScene.Clear();
        _deadEntitiesByScene.Clear();
        Debug.Log("[SSMP.EnemyHealthSync] Cleared all entity hosts");
    }
}
