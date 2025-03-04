# generate-beacon.ps1 - Script to generate a Beacon AOT-compatible solution from Pulsar rule files

param (
    [Parameter(Mandatory=$false)]
    [string]$rules = $null,
    
    [Parameter(Mandatory=$false)]
    [string]$config = $null,
    
    [Parameter(Mandatory=$false)]
    [string]$output = $null,
    
    [Parameter(Mandatory=$false)]
    [string]$target = "win-x64",

    [Parameter(Mandatory=$false)]
    [switch]$detailed = $false
)

# Function to show usage
function Show-Usage {
    Write-Host "Usage: .\generate-beacon.ps1 -rules <rules-path> -config <config-path> -output <output-path> [-target <runtime-id>] [-detailed]"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -rules <path>      Path to YAML rule file or directory containing rule files (required)"
    Write-Host "  -config <path>     Path to system configuration YAML file (required)"
    Write-Host "  -output <path>     Output directory for the Beacon solution (required)"
    Write-Host "  -target <runtime>  Target runtime identifier for AOT compilation (default: win-x64)"
    Write-Host "  -detailed         Enable detailed logging"
    Write-Host ""
    Write-Host "Example:"
    Write-Host "  .\generate-beacon.ps1 -rules .\rules.yaml -config .\system_config.yaml -output C:\Beacon -target win-x64"
}

# Validate required parameters
if ([string]::IsNullOrEmpty($rules) -or [string]::IsNullOrEmpty($config) -or [string]::IsNullOrEmpty($output)) {
    Write-Host "Error: Missing required parameters" -ForegroundColor Red
    Show-Usage
    exit 1
}

# Validate that rules path exists
if (-not (Test-Path $rules)) {
    Write-Host "Error: Rules path not found: $rules" -ForegroundColor Red
    exit 1
}

# Validate that config file exists
if (-not (Test-Path $config)) {
    Write-Host "Error: Config file not found: $config" -ForegroundColor Red
    exit 1
}

# Create output directory if it doesn't exist
if (-not (Test-Path $output)) {
    New-Item -ItemType Directory -Path $output | Out-Null
    Write-Host "Created output directory: $output"
} else {
    Write-Host "Output directory already exists: $output"
    # Clean the output directory if it's not empty
    $files = Get-ChildItem -Path $output -File
    if ($files.Count -gt 0) {
        Write-Host "Cleaning output directory..."
        Remove-Item -Path "$output\*" -Force -Recurse
    }
}

# Set verbosity level for dotnet command
$verbosityArg = ""
if ($detailed) {
    $verbosityArg = "--verbosity detailed"
    Write-Host "Detailed logging enabled" -ForegroundColor Yellow
}

# Build the command to run
$command = "dotnet run --project .\Pulsar.Compiler\Pulsar.Compiler.csproj $verbosityArg beacon --rules `"$rules`" --config `"$config`" --output `"$output`" --target $target"

Write-Host "Generating Beacon AOT-compatible solution..." -ForegroundColor Cyan
Write-Host "Rules: $rules"
Write-Host "Config: $config"
Write-Host "Output: $output"
Write-Host "Target: $target"
Write-Host "Command: $command" -ForegroundColor Gray

# Execute the command
Write-Host "Executing command..." -ForegroundColor Cyan
Invoke-Expression $command

# Check if the command succeeded
if ($LASTEXITCODE -eq 0) {
    Write-Host "Beacon solution generated successfully in $output" -ForegroundColor Green
    
    # List the generated files
    Write-Host "Generated files:" -ForegroundColor Cyan
    Get-ChildItem -Path $output -File | ForEach-Object {
        Write-Host "  $($_.Name) ($($_.Length) bytes)"
    }
} else {
    Write-Host "Failed to generate Beacon solution (exit code: $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}
