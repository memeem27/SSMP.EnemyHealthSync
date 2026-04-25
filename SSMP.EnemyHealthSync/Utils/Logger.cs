namespace SSMPEnemyHealthSync.Utils;

internal static class Logger
{
    private static BepInEx.Logging.ManualLogSource? _logger;

    internal static void SetLogger(BepInEx.Logging.ManualLogSource logger)
    {
        _logger = logger;
    }

    internal static void LogInfo(string message)
    {
        _logger?.LogInfo(message);
    }

    internal static void LogDebug(string message)
    {
        _logger?.LogDebug(message);
    }

    internal static void LogWarning(string message)
    {
        _logger?.LogWarning(message);
    }

    internal static void LogError(string message)
    {
        _logger?.LogError(message);
    }
}
