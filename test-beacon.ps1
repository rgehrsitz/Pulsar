# test-beacon.ps1 - Script to test Beacon AOT-compatible solution generation with AllowInvalidSensors flag

param (
    [Parameter(Mandatory=$false)]
    [string]$rules = $null,
    
    [Parameter(Mandatory=$false)]
    [string]$config = $null,
    
    [Parameter(Mandatory=$false)]
    [string]$output = $null,
    
    [Parameter(Mandatory=$false)]
    [string]$target = "win-x64"
)

# Function to show usage
function Show-Usage {
    Write-Host "Usage: .\test-beacon.ps1 -rules <rules-path> -config <config-path> -output <output-path> [-target <runtime-id>]"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -rules <path>      Path to YAML rule file or directory containing rule files (required)"
    Write-Host "  -config <path>     Path to system configuration YAML file (required)"
    Write-Host "  -output <path>     Output directory for the Beacon solution (required)"
    Write-Host "  -target <runtime>  Target runtime identifier for AOT compilation (default: win-x64)"
    Write-Host ""
    Write-Host "Example:"
    Write-Host "  .\test-beacon.ps1 -rules .\rules -config .\config\system_config.yaml -output .\output -target win-x64"
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
}

# Build the command to run with AllowInvalidSensors flag
$command = "dotnet run --project .\Pulsar.Compiler\Pulsar.Compiler.csproj beacon --rules `"$rules`" --config `"$config`" --output `"$output`" --target $target --allow-invalid-sensors"

Write-Host "Generating Beacon AOT-compatible solution with AllowInvalidSensors flag..."
Write-Host "Command: $command"

# Execute the command
Invoke-Expression $command

# Check if the command succeeded
if ($LASTEXITCODE -eq 0) {
    Write-Host "Beacon solution generated successfully in $output\Beacon" -ForegroundColor Green
} else {
    Write-Host "Failed to generate Beacon solution (exit code: $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}
