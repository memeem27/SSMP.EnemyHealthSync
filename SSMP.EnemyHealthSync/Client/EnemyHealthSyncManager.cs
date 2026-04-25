using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using SSMPEnemyHealthSync.Utils;

namespace SSMPEnemyHealthSync.Client;

internal static class EnemyHealthSyncManager
{
    private sealed class TrackedEnemy
    {
        internal ushort EntityId;
        internal HealthManager HealthManager = null!;
        internal int LastHealth;
        internal int MaxHealth;
        internal bool LastIsDead;
    }

    private static readonly Dictionary<ushort, TrackedEnemy> _trackedEnemies = new Dictionary<ushort, TrackedEnemy>();
    private static readonly Dictionary<string, HashSet<ushort>> _deadEnemiesByScene = new Dictionary<string, HashSet<ushort>>(StringComparer.Ordinal);
    private static HashSet<ushort> _deadEnemies = new HashSet<ushort>();

    private static FieldInfo? _hpField;
    private static FieldInfo? _isDeadField;
    private static readonly List<MethodInfo> _dieMethods = new List<MethodInfo>();

    private static string _lastSceneName = "";
    private static bool _initialized;
    private static int _scanRetryFrame;
    private const int SCAN_RETRY_INTERVAL_FRAMES = 60;

    internal static void Init()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        _hpField = typeof(HealthManager).GetField("hp", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        _isDeadField = typeof(HealthManager).GetField("isDead", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        _dieMethods.Clear();
        try
        {
            var methods = typeof(HealthManager).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var m in methods)
            {
                if (m.Name != "Die")
                {
                    continue;
                }

                _dieMethods.Add(m);
            }

            _dieMethods.Sort((a, b) => a.GetParameters().Length.CompareTo(b.GetParameters().Length));
        }
        catch
        {
            _dieMethods.Clear();
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Config.Enabled)
        {
            return;
        }

        _trackedEnemies.Clear();
        _lastSceneName = scene.name;
        if (!_deadEnemiesByScene.TryGetValue(_lastSceneName, out _deadEnemies!))
        {
            _deadEnemies = new HashSet<ushort>();
            _deadEnemiesByScene[_lastSceneName] = _deadEnemies;
        }
        _scanRetryFrame = 0;
        Client.ResetSceneHost();
        PacketSender.SendSceneEntered(scene.name);
        ScanForEnemies();
    }

    internal static void OnOtherPlayerEnteredLocalScene()
    {
        if (!Config.Enabled || !Config.SyncHealth)
        {
            return;
        }

        if (!Client.IsSceneHost || !Client.IsSceneHostDetermined)
        {
            return;
        }

        if (_deadEnemies.Count == 0)
        {
            return;
        }

        foreach (var entityId in _deadEnemies)
        {
            if (_trackedEnemies.TryGetValue(entityId, out var enemy) && enemy.HealthManager != null)
            {
                PacketSender.SendEnemyHealthUpdate(entityId, 0, enemy.MaxHealth, true, 0);
            }
            else
            {
                PacketSender.SendEnemyHealthUpdate(entityId, 0, 0, true, 0);
            }
        }
    }

    private static void ScanForEnemies()
    {
        if (!Config.Enabled)
        {
            return;
        }

        var healthManagers = UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None);
        foreach (var hm in healthManagers)
        {
            if (hm == null || hm.gameObject == null)
            {
                continue;
            }

            var id = ComputeEntityId(_lastSceneName, hm.gameObject);
            if (_trackedEnemies.ContainsKey(id))
            {
                continue;
            }

            var maxHealth = GetHealthViaReflection(hm);
            var isDead = GetIsDeadViaReflection(hm);

            _trackedEnemies[id] = new TrackedEnemy
            {
                EntityId = id,
                HealthManager = hm,
                LastHealth = maxHealth,
                MaxHealth = maxHealth,
                LastIsDead = isDead
            };

            if (_deadEnemies.Contains(id))
            {
                SetHealthViaReflection(hm, 0);
                if (Config.SyncDeath)
                {
                    TryCallDie(hm);
                }

                _trackedEnemies[id].LastHealth = 0;
                _trackedEnemies[id].LastIsDead = true;
            }
        }
    }

    internal static void Update()
    {
        if (!Config.Enabled || !Config.SyncHealth)
        {
            return;
        }

        if (!Client.IsSceneHostDetermined)
        {
            Client.OnSceneLoadedAndEnemiesScanned();
        }

        _scanRetryFrame++;
        if (_scanRetryFrame >= SCAN_RETRY_INTERVAL_FRAMES)
        {
            _scanRetryFrame = 0;

            if (_trackedEnemies.Count == 0)
            {
                ScanForEnemies();
            }
        }

        foreach (var enemy in _trackedEnemies.Values)
        {
            if (enemy.HealthManager == null)
            {
                continue;
            }

            if (_deadEnemies.Contains(enemy.EntityId))
            {
                var cachedHp = GetHealthViaReflection(enemy.HealthManager);
                if (cachedHp > 0)
                {
                    SetHealthViaReflection(enemy.HealthManager, 0);
                    if (Config.SyncDeath)
                    {
                        TryCallDie(enemy.HealthManager);
                    }

                    PacketSender.SendEnemyHealthUpdate(enemy.EntityId, 0, enemy.MaxHealth, true, cachedHp);
                    enemy.LastHealth = 0;
                    enemy.LastIsDead = true;
                    continue;
                }
            }

            var currentHp = GetHealthViaReflection(enemy.HealthManager);
            var isDead = GetIsDeadViaReflection(enemy.HealthManager);
            if (currentHp <= 0)
            {
                isDead = true;
            }

            if (isDead || currentHp <= 0)
            {
                _deadEnemies.Add(enemy.EntityId);
            }
            else
            {
                _deadEnemies.Remove(enemy.EntityId);
            }

            if (currentHp != enemy.LastHealth || isDead != enemy.LastIsDead)
            {
                var dmg = Math.Max(0, enemy.LastHealth - currentHp);
                PacketSender.SendEnemyHealthUpdate(enemy.EntityId, currentHp, enemy.MaxHealth, isDead, dmg);
                enemy.LastHealth = currentHp;
                enemy.LastIsDead = isDead;
            }
        }
    }

    internal static void HandleEnemyHealthUpdate(ushort entityId, int health, int maxHealth, bool isDead)
    {
        if (!Config.Enabled || !Config.SyncHealth)
        {
            return;
        }

        if (isDead || health <= 0)
        {
            _deadEnemies.Add(entityId);
        }
        else
        {
            _deadEnemies.Remove(entityId);
        }

        TrackedEnemy enemy;
        if (!_trackedEnemies.TryGetValue(entityId, out enemy!) || enemy.HealthManager == null)
        {
            ScanForEnemies();
            if (!_trackedEnemies.TryGetValue(entityId, out enemy!) || enemy.HealthManager == null)
            {
                return;
            }
        }

        enemy.MaxHealth = maxHealth;
        SetHealthViaReflection(enemy.HealthManager, health);

        if (Config.SyncDeath && isDead)
        {
            SetHealthViaReflection(enemy.HealthManager, 0);
            TryCallDie(enemy.HealthManager);
        }

        enemy.LastHealth = health;
        enemy.LastIsDead = isDead;
    }

    private static int GetHealthViaReflection(HealthManager hm)
    {
        try
        {
            if (_hpField != null)
            {
                return (int)_hpField.GetValue(hm);
            }
        }
        catch
        {
        }

        return hm.hp;
    }

    private static bool GetIsDeadViaReflection(HealthManager hm)
    {
        try
        {
            if (_isDeadField != null)
            {
                return (bool)_isDeadField.GetValue(hm);
            }
        }
        catch
        {
        }

        return false;
    }

    private static void SetHealthViaReflection(HealthManager hm, int health)
    {
        try
        {
            if (_hpField != null)
            {
                _hpField.SetValue(hm, health);
                return;
            }
        }
        catch
        {
        }

        hm.hp = health;
    }

    private static void TryCallDie(HealthManager hm)
    {
        if (_dieMethods.Count == 0)
        {
            return;
        }

        foreach (var m in _dieMethods)
        {
            try
            {
                var parameters = m.GetParameters();
                object?[]? args = null;
                if (parameters.Length > 0)
                {
                    args = new object?[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        args[i] = GetDefaultValue(parameters[i].ParameterType);
                    }
                }

                m.Invoke(hm, args);
                return;
            }
            catch
            {
            }
        }
    }

    private static object? GetDefaultValue(Type t)
    {
        if (!t.IsValueType)
        {
            return null;
        }

        if (t.IsEnum)
        {
            var values = Enum.GetValues(t);
            return values.Length > 0 ? values.GetValue(0) : Activator.CreateInstance(t);
        }

        return Activator.CreateInstance(t);
    }

    private static ushort ComputeEntityId(string sceneName, GameObject go)
    {
        unchecked
        {
            uint hash = 2166136261;

            Action<string> add = s =>
            {
                for (int i = 0; i < s.Length; i++)
                {
                    hash ^= s[i];
                    hash *= 16777619;
                }
            };

            add(sceneName ?? "");
            add("|");
            add(go.name ?? "");
            add("|");

            var p = go.transform.position;
            add(p.x.ToString("F2"));
            add(",");
            add(p.y.ToString("F2"));
            add(",");
            add(p.z.ToString("F2"));

            return (ushort)(hash & 0xFFFF);
        }
    }
}
