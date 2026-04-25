using BepInEx;
using HarmonyLib;
using SSMP.Api.Client;
using SSMP.Api.Server;

namespace SSMPEnemyHealthSync;

[BepInAutoPlugin(id: "com.ssmp.enemyhealthsync", version: Utils.Config.Version)]
[BepInDependency("ssmp", BepInDependency.DependencyFlags.HardDependency)]
public partial class SSMPEnemyHealthSyncPlugin : BaseUnityPlugin
{
    internal static SSMPEnemyHealthSyncPlugin instance = null!;

    private void Awake()
    {
        instance = this;

        // Register as SSMP addon
        ClientAddon.RegisterAddon(new Client.Client());
        ServerAddon.RegisterAddon(new Server.Server());

        Utils.Config.Init(Config);
        Utils.Logger.SetLogger(Logger);
        Logger.LogInfo($"Plugin {Name} ({Id}) has loaded!");

        var harmony = new Harmony("ssmp.enemyhealthsync");
        harmony.PatchAll();
    }

    private void Update()
    {
        // Update enemy tracking every frame
        Client.EnemyHealthSyncManager.Update();
    }
}
