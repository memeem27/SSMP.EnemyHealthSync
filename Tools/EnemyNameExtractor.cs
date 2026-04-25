using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SSMPEnemyHealthSync.Tools;

/// <summary>
/// Tool to extract enemy class names from decompiled Silksong assembly files.
/// This helps identify which enemy types to add to the entity registry.
/// </summary>
public static class EnemyNameExtractor
{
    /// <summary>
    /// Analyzes decompiled C# files to find enemy MonoBehaviour classes.
    /// </summary>
    /// <param name="assemblyFolder">Path to folder containing decompiled .cs files</param>
    /// <returns>List of potential enemy class names</returns>
    public static List<EnemyInfo> ExtractEnemies(string assemblyFolder)
    {
        var enemies = new List<EnemyInfo>();
        var files = Directory.GetFiles(assemblyFolder, "*.cs", SearchOption.TopDirectoryOnly);

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            var filename = Path.GetFileNameWithoutExtension(file);

            // Check if it's a MonoBehaviour
            if (!IsMonoBehaviour(content))
                continue;

            // Check for enemy indicators
            var indicators = GetEnemyIndicators(content);
            if (indicators.Count == 0)
                continue;

            enemies.Add(new EnemyInfo
            {
                ClassName = filename,
                Indicators = indicators,
                HasHealthManager = content.Contains("HealthManager"),
                HasHitResponder = content.Contains("IHitResponder"),
                HasEnemyMessageReceiver = content.Contains("IEnemyMessageReceiver"),
                DamageType = ExtractDamageType(content),
                Confidence = CalculateConfidence(indicators, content)
            });
        }

        return enemies.OrderByDescending(e => e.Confidence).ToList();
    }

    private static bool IsMonoBehaviour(string content)
    {
        // Check if class inherits from MonoBehaviour
        return Regex.IsMatch(content, @"class\s+\w+\s*:\s*MonoBehaviour") ||
               content.Contains(": MonoBehaviour");
    }

    private static List<string> GetEnemyIndicators(string content)
    {
        var indicators = new List<string>();

        // Health/Damage related
        if (content.Contains("HealthManager")) indicators.Add("HealthManager");
        if (content.Contains("IHitResponder")) indicators.Add("HitResponder");
        if (content.Contains("TakeDamage")) indicators.Add("TakeDamage");
        if (content.Contains("Die(")) indicators.Add("Death");

        // AI/Behavior related
        if (content.Contains("IEnemyMessageReceiver")) indicators.Add("EnemyMessage");
        if (content.Contains("AIState")) indicators.Add("AI");
        if (content.Contains("EnemyTypes")) indicators.Add("EnemyType");

        // Movement/Attack
        if (content.Contains("Attack")) indicators.Add("Attack");
        if (content.Contains("Chase")) indicators.Add("Chase");

        return indicators;
    }

    private static string ExtractDamageType(string content)
    {
        // Try to find damage amount or type
        var match = Regex.Match(content, @"damage\s*=\s*(\d+)");
        if (match.Success)
            return $"Damage: {match.Groups[1].Value}";

        return "Unknown";
    }

    private static int CalculateConfidence(List<string> indicators, string content)
    {
        int score = indicators.Count * 10;

        // Bonus for HealthManager
        if (content.Contains("HealthManager")) score += 20;

        // Bonus for having both health and AI
        if (content.Contains("HealthManager") && content.Contains("IEnemyMessageReceiver"))
            score += 15;

        // Bonus for having multiple attack methods
        var attackCount = Regex.Matches(content, @"void\s+\w*[Aa]ttack").Count;
        score += attackCount * 5;

        return score;
    }

    public static void GenerateEntityRegistryJson(List<EnemyInfo> enemies, string outputPath)
    {
        using var writer = new StreamWriter(outputPath);
        writer.WriteLine("[");
        writer.WriteLine("    // Auto-generated enemy registry entries");

        foreach (var enemy in enemies.Where(e => e.Confidence >= 30))
        {
            // Convert class name to likely GameObject name
            var baseName = enemy.ClassName;
            // Common patterns: SomeClass -> Some Class, or keep as-is
            var gameObjectName = Regex.Replace(baseName, @"([a-z])([A-Z])", "$1 $2");

            writer.WriteLine("    {");
            writer.WriteLine($"        \"_comment\": \"{string.Join(", ", enemy.Indicators)}\",");
            writer.WriteLine($"        \"base_object_name\": \"{gameObjectName}\",");
            writer.WriteLine($"        \"type\": \"{baseName}\"");
            writer.WriteLine("    },");
        }

        writer.WriteLine("]");
    }

    public static void PrintEnemyReport(List<EnemyInfo> enemies, int topCount = 50)
    {
        Console.WriteLine("\n=== Enemy Analysis Report ===\n");
        Console.WriteLine($"Found {enemies.Count} potential enemies\n");

        Console.WriteLine($"{'Rank',-5} {'Class Name',-30} {'Confidence',-12} {'Indicators',-40}");
        Console.WriteLine(new string('-', 90));

        int rank = 1;
        foreach (var enemy in enemies.Take(topCount))
        {
            var indicators = string.Join(", ", enemy.Indicators.Take(4));
            Console.WriteLine($"{rank,-5} {enemy.ClassName,-30} {enemy.Confidence,-12} {indicators,-40}");
            rank++;
        }

        Console.WriteLine("\n=== High Confidence Enemies (Likely actual enemies) ===\n");
        foreach (var enemy in enemies.Where(e => e.Confidence >= 40).Take(20))
        {
            Console.WriteLine($"  - {enemy.ClassName}");
            Console.WriteLine($"    HealthManager: {enemy.HasHealthManager}");
            Console.WriteLine($"    HitResponder: {enemy.HasHitResponder}");
            Console.WriteLine($"    Damage: {enemy.DamageType}");
            Console.WriteLine();
        }
    }
}

public class EnemyInfo
{
    public string ClassName = "";
    public List<string> Indicators = new();
    public bool HasHealthManager;
    public bool HasHitResponder;
    public bool HasEnemyMessageReceiver;
    public string DamageType = "";
    public int Confidence;
}
