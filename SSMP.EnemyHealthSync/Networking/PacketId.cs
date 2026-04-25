namespace SSMPEnemyHealthSync.Networking;

/// <summary>
/// Packet IDs for SSMP.EnemyHealthSync addon
/// </summary>
public enum PacketId : byte
{
    /// <summary>
    /// Health update from client to server (scene host only)
    /// </summary>
    EnemyHealthUpdate = 0,

    SceneEntered = 1,
}
