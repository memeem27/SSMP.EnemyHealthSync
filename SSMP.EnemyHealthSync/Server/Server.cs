using UnityEngine;
using SSMP.Api.Server;
using SSMP.Api.Server.Networking;
using SSMP.Networking.Packet;
using SSMPEnemyHealthSync.Networking;
using SSMPEnemyHealthSync.Utils;

namespace SSMPEnemyHealthSync.Server;

internal class Server : ServerAddon
{
    protected override string Name => Config.ModName;
    protected override string Version => Config.Version;
    public override uint ApiVersion => Config.SSMPApiVersion;
    public override bool NeedsNetwork => true;

    internal static IServerApi api = null!;
    internal static Server instance = null!;
    internal static IServerAddonNetworkSender<PacketId>? networkSender;
    internal static IServerAddonNetworkReceiver<PacketId>? networkReceiver;

    public override void Initialize(IServerApi serverApi)
    {
        instance = this;
        api = serverApi;

        Debug.Log("[SSMP.EnemyHealthSync] Server addon initializing...");

        // Get network sender and receiver from SSMP
        try
        {
            networkSender = api.NetServer.GetNetworkSender<PacketId>(this);
            Debug.Log($"[SSMP.EnemyHealthSync] Server network sender obtained: {networkSender != null}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SSMP.EnemyHealthSync] Failed to get server network sender: {ex.Message}");
        }

        try
        {
            networkReceiver = api.NetServer.GetNetworkReceiver<PacketId>(this, PacketInstantiator);
            Debug.Log($"[SSMP.EnemyHealthSync] Server network receiver obtained: {networkReceiver != null}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SSMP.EnemyHealthSync] Failed to get server network receiver: {ex.Message}");
        }

        PacketHandler.Init();

        Logger.Info("SSMP EnemyHealthSync Server Initialized");
    }

    /// <summary>
    /// Instantiate packet data based on packet ID
    /// </summary>
    private static IPacketData PacketInstantiator(PacketId packetId)
    {
        return packetId switch
        {
            PacketId.EnemyHealthUpdate => new EnemyHealthPacket(),
            PacketId.SceneEntered => new SceneEnteredPacket(),
            _ => throw new System.ArgumentOutOfRangeException(nameof(packetId), $"Unknown packet ID: {packetId}")
        };
    }
}
