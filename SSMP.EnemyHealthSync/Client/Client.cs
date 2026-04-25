using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using SSMP.Api.Client;
using SSMP.Api.Client.Networking;
using SSMP.Networking.Packet;
using SSMPEnemyHealthSync.Networking;
using SSMPEnemyHealthSync.Utils;

namespace SSMPEnemyHealthSync.Client;

internal class Client : ClientAddon
{
    protected override string Name => Config.ModName;
    protected override string Version => Config.Version;
    public override uint ApiVersion => Config.SSMPApiVersion;
    public override bool NeedsNetwork => true;

    internal static IClientApi api = null!;
    internal static Client instance = null!;
    internal static IClientAddonNetworkSender<PacketId>? networkSender;
    internal static IClientAddonNetworkReceiver<PacketId>? networkReceiver;

    /// <summary>
    /// Whether we are the current scene host (responsible for syncing enemies)
    /// </summary>
    internal static bool IsSceneHost { get; private set; } = false;

    /// <summary>
    /// Whether scene host status has been determined for current scene
    /// </summary>
    internal static bool IsSceneHostDetermined { get; private set; } = false;

    public override void Initialize(IClientApi clientApi)
    {
        instance = this;
        api = clientApi;

        Debug.Log("[SSMP.EnemyHealthSync] Client addon initialized [v2-20250416]");

        // Get network sender and receiver from SSMP
        try
        {
            networkSender = api.NetClient.GetNetworkSender<PacketId>(this);
            Debug.Log($"[SSMP.EnemyHealthSync] Network sender obtained: {networkSender != null}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SSMP.EnemyHealthSync] Failed to get network sender: {ex.Message}");
        }

        try
        {
            networkReceiver = api.NetClient.GetNetworkReceiver<PacketId>(this, PacketInstantiator);
            Debug.Log($"[SSMP.EnemyHealthSync] Network receiver obtained: {networkReceiver != null}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SSMP.EnemyHealthSync] Failed to get network receiver: {ex.Message}");
        }

        // Initialize packet handling
        PacketReceiver.Init();
        PacketSender.Init();

        // Initialize enemy sync system
        EnemyHealthSyncManager.Init();

        // Hook into scene events to track scene host status
        api.ClientManager.PlayerEnterSceneEvent += OnPlayerEnterScene;
        api.ClientManager.PlayerLeaveSceneEvent += OnPlayerLeaveScene;
        // Use a coroutine to check if we're host after entering scene
        // If we don't receive AlreadyInScene within 0.5 seconds, we are the host

        Logger.Info("SSMP EnemyHealthSync Client Initialized");
    }

    /// <summary>
    /// Called when any player enters our scene
    /// </summary>
    private void OnPlayerEnterScene(IClientPlayer player)
    {
        // Only process AlreadyInScene for players who were already in the scene when WE entered
        // NOT for players who join after us (we should remain host in that case)
        if (player.IsInLocalScene && !IsSceneHostDetermined)
        {
            // We just entered and found someone already here - we are NOT host
            IsSceneHost = false;
            IsSceneHostDetermined = true;
            Debug.Log("[SSMP.EnemyHealthSync] We are NOT scene host (other player already in scene when we arrived)");
        }
        else if (player.IsInLocalScene && IsSceneHostDetermined && IsSceneHost)
        {
            EnemyHealthSyncManager.OnOtherPlayerEnteredLocalScene();
        }
        // If we ARE already determined as host, ignore host status changes - we stay host
    }

    /// <summary>
    /// Called when any player leaves the scene
    /// </summary>
    private void OnPlayerLeaveScene(IClientPlayer player)
    {
        // Host migration not implemented - SSMP will handle reassigning host
        // For now, if host leaves, no new updates will be sent until scene changes
    }

    /// <summary>
    /// Called by EnemyHealthSyncManager when scene is loaded and enemies are scanned
    /// </summary>
    private static int _hostCheckFrameDelay = 0;
    private const int HOST_CHECK_DELAY_FRAMES = 30; // ~0.5 seconds at 60fps

    internal static void ResetSceneHost()
    {
        IsSceneHost = false;
        IsSceneHostDetermined = false;
        _hostCheckFrameDelay = 0; // Reset the frame delay counter
        Debug.Log("[SSMP.EnemyHealthSync] Reset scene host status for new scene");
    }

    internal static void OnSceneLoadedAndEnemiesScanned()
    {
        if (IsSceneHostDetermined)
        {
            return;
        }

        if (api?.ClientManager?.Players == null)
        {
            Debug.Log("[SSMP.EnemyHealthSync] Delaying host check - API not ready yet");
            return;
        }

        _hostCheckFrameDelay++;
        if (_hostCheckFrameDelay < HOST_CHECK_DELAY_FRAMES)
        {
            return;
        }

        bool otherPlayersInScene = false;
        foreach (var player in api.ClientManager.Players)
        {
            if (player.IsInLocalScene)
            {
                otherPlayersInScene = true;
                break;
            }
        }

        if (otherPlayersInScene)
        {
            IsSceneHost = false;
            IsSceneHostDetermined = true;
            Debug.Log("[SSMP.EnemyHealthSync] We are NOT scene host (other players already present)");
        }
        else
        {
            IsSceneHost = true;
            IsSceneHostDetermined = true;
            Debug.Log("[SSMP.EnemyHealthSync] We are scene host (first in scene)");
        }
    }

    private static IPacketData PacketInstantiator(PacketId packetId)
    {
        return packetId switch
        {
            PacketId.EnemyHealthUpdate => new EnemyHealthPacket(),
            PacketId.SceneEntered => new SceneEnteredPacket(),
            _ => throw new System.ArgumentOutOfRangeException(nameof(packetId), packetId, null)
        };
    }
}
