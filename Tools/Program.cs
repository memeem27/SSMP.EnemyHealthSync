using System;
using System.IO;
using SSMPEnemyHealthSync.Tools;

Console.WriteLine("SSMP Enemy Scanner");
Console.WriteLine("==================\n");

// Default path to the decompiled assembly (update this to your path)
var defaultPath = @".\Assembly-CSharp";

Console.Write($"Enter path to decompiled .cs files [{defaultPath}]: ");
var input = Console.ReadLine();
var assemblyPath = string.IsNullOrWhiteSpace(input) ? defaultPath : input;

if (!Directory.Exists(assemblyPath))
{
    Console.WriteLine($"Error: Directory not found: {assemblyPath}");
    Console.WriteLine("\nMake sure you have decompiled Assembly-CSharp.dll first.");
    Console.WriteLine("You can use ILSpy or dnSpy to decompile the assembly.");
    return 1;
}

Console.WriteLine($"\nScanning: {assemblyPath}\n");

var enemies = EnemyNameExtractor.ExtractEnemies(assemblyPath);

EnemyNameExtractor.PrintEnemyReport(enemies, 100);

// Ask if user wants to generate registry file
Console.Write("\nGenerate entity-registry.json entries? (y/n): ");
if (Console.ReadLine()?.ToLower() == "y")
{
    var outputPath = Path.Combine(assemblyPath, "enemy-registry-entries.json");
    EnemyNameExtractor.GenerateEntityRegistryJson(enemies, outputPath);
    Console.WriteLine($"\nGenerated: {outputPath}");
}

return 0;
