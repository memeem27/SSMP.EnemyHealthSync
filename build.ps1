# Build script for SSMP.EnemyHealthSync using .NET SDK
# Run: .\build.ps1

$ErrorActionPreference = "Stop"

# Check for dotnet
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Host "dotnet command not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install .NET 8.0 SDK:" -ForegroundColor Yellow
    Write-Host "  https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor White
    Write-Host ""
    Write-Host "Or run: winget install Microsoft.DotNet.SDK.8" -ForegroundColor Cyan
    exit 1
}

Write-Host "Found dotnet: $($dotnet.Source)" -ForegroundColor Green

# Create SilksongPath.props if it doesn't exist
$propsPath = "SilksongPath.props"
if (-not (Test-Path $propsPath)) {
    Write-Host "SilksongPath.props not found. Creating from template..." -ForegroundColor Yellow
    Write-Host "Please update the paths in SilksongPath.props to match your Silksong installation." -ForegroundColor Yellow

    # Try to detect common install paths
    $steamPath = "C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong"
    $defaultPluginsPath = "$steamPath\BepInEx\plugins"

    if (Test-Path $steamPath) {
        Write-Host "Detected Steam installation at: $steamPath" -ForegroundColor Green
    } else {
        Write-Host "Could not detect Silksong installation." -ForegroundColor Red
        Write-Host "You'll need to manually edit SilksongPath.props after creation." -ForegroundColor Yellow
        $defaultPluginsPath = "C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong\BepInEx\plugins"
    }

    @"
<Project>
  <PropertyGroup>
    <!-- Update these paths to match your Silksong installation -->
    <SilksongPluginsFolder>$defaultPluginsPath</SilksongPluginsFolder>
    <SilksongGameFolder>$steamPath</SilksongGameFolder>
  </PropertyGroup>
</Project>
"@ | Out-File -FilePath $propsPath -Encoding UTF8

    Write-Host "Created $propsPath - please verify the paths are correct before building." -ForegroundColor Cyan
    exit 1
}

# Build the project
Write-Host "Building project..." -ForegroundColor Cyan
& dotnet build "SSMP.EnemyHealthSync\SSMPEnemyHealthSync.csproj" --configuration Debug

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build successful!" -ForegroundColor Green
    
    # Show output location
    $outputPath = "SSMP.EnemyHealthSync\bin\Debug\net472\SSMPEnemyHealthSync.dll"
    if (Test-Path $outputPath) {
        Write-Host "Output: $outputPath" -ForegroundColor Green
    }
} else {
    Write-Error "Build failed!"
    exit 1
}
