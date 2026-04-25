using SSMP.Networking.Packet;

namespace SSMPEnemyHealthSync.Networking;

/// <summary>
/// Packet data for enemy health synchronization
/// </summary>
public class EnemyHealthPacket : IPacketData
{
    /// <inheritdoc />
    public bool IsReliable => true;

    /// <inheritdoc />
    public bool DropReliableDataIfNewerExists => true;

    /// <summary>
    /// Entity ID of the enemy
    /// </summary>
    public ushort EntityId { get; set; }

    /// <summary>
    /// Current health value
    /// </summary>
    public int Health { get; set; }

    /// <summary>
    /// Maximum health value
    /// </summary>
    public int MaxHealth { get; set; }

    /// <summary>
    /// Whether the enemy is dead
    /// </summary>
    public bool IsDead { get; set; }

    /// <summary>
    /// Damage dealt (for hit feedback)
    /// </summary>
    public int DamageDealt { get; set; }

    /// <inheritdoc />
    public void WriteData(IPacket packet)
    {
        packet.Write(EntityId);
        packet.Write(Health);
        packet.Write(MaxHealth);
        packet.Write(IsDead);
        packet.Write(DamageDealt);
    }

    /// <inheritdoc />
    public void ReadData(IPacket packet)
    {
        EntityId = packet.ReadUShort();
        Health = packet.ReadInt();
        MaxHealth = packet.ReadInt();
        IsDead = packet.ReadBool();
        DamageDealt = packet.ReadInt();
    }
}
