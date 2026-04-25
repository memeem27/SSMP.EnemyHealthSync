using BepInEx.Configuration;

namespace SSMPEnemyHealthSync.Utils;

internal static class Config
{
    public const string ModName = "SSMP.EnemyHealthSync";
    public const string Version = "1.0.0";
    public const uint SSMPApiVersion = 1;

    public static bool Enabled { get; private set; } = true;
    public static bool SyncHealth { get; private set; } = true;
    public static bool SyncDeath { get; private set; } = true;

    public static void Init(ConfigFile config)
    {
        Enabled = config.Bind("General", "Enabled", true, "Enable the enemy health sync mod").Value;
        SyncHealth = config.Bind("General", "SyncHealth", true, "Sync enemy health/damage between players").Value;
        SyncDeath = config.Bind("General", "SyncDeath", true, "Sync enemy death state between players").Value;
    }
}
