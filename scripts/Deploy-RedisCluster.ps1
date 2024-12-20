# PowerShell script for deploying Redis cluster
param(
    [Parameter(Mandatory=$true)]
    [string]$ConfigPath
)

# Import configuration
$config = Get-Content $ConfigPath | ConvertFrom-Json

# Function to create required directories
function Create-Directories {
    param (
        [string]$computerName,
        [PSCredential]$credential
    )
    
    $dirs = @(
        $config.redis.install_path,
        $config.redis.config_path,
        $config.redis.log_path,
        $config.redis.data_path
    )

    Invoke-Command -ComputerName $computerName -Credential $credential -ScriptBlock {
        param($directories)
        foreach ($dir in $directories) {
            if (-not (Test-Path $dir)) {
                New-Item -Path $dir -ItemType Directory -Force
            }
        }
    } -ArgumentList (,$dirs)
}

# Function to copy Redis files
function Copy-RedisFiles {
    param (
        [string]$computerName,
        [string]$configFile,
        [PSCredential]$credential
    )

    # Copy Redis binaries and configuration
    $sourceConfig = Join-Path $PSScriptRoot "..\config\redis\$configFile"
    $destConfig = Join-Path $config.redis.config_path $configFile

    Copy-Item -Path $sourceConfig -Destination $destConfig -ToSession (New-PSSession -ComputerName $computerName -Credential $credential)
}

# Function to install and configure Redis service
function Install-RedisService {
    param (
        [string]$computerName,
        [string]$serviceName,
        [string]$configFile,
        [PSCredential]$credential
    )

    $configPath = Join-Path $config.redis.config_path $configFile
    
    Invoke-Command -ComputerName $computerName -Credential $credential -ScriptBlock {
        param($service, $config, $installPath)
        
        # Stop and remove existing service if it exists
        if (Get-Service -Name $service -ErrorAction SilentlyContinue) {
            Stop-Service -Name $service -Force
            sc.exe delete $service
        }

        # Install new service
        $exePath = Join-Path $installPath "redis-server.exe"
        sc.exe create $service binPath= "`"$exePath`" `"$config`" --service-run" start= auto
        Start-Service -Name $service
    } -ArgumentList $serviceName, $configPath, $config.redis.install_path
}

# Function to deploy to a building
function Deploy-ToBuilding {
    param (
        [string]$buildingId,
        [PSCredential]$credential
    )

    $building = $config.buildings.$buildingId

    Write-Host "Deploying to $buildingId..."

    # Deploy to Redis server
    $redisServer = $building.redis_server
    Write-Host "Deploying Redis to $($redisServer.hostname)..."
    
    Create-Directories -computerName $redisServer.hostname -credential $credential
    Copy-RedisFiles -computerName $redisServer.hostname -configFile $redisServer.redis_config -credential $credential
    Install-RedisService -computerName $redisServer.hostname -serviceName "Redis" -configFile $redisServer.redis_config -credential $credential
    Install-RedisService -computerName $redisServer.hostname -serviceName "RedisSentinel" -configFile $redisServer.sentinel_config -credential $credential

    # Deploy to sentinel server
    $sentinelServer = $building.sentinel_server
    Write-Host "Deploying Sentinel to $($sentinelServer.hostname)..."
    
    Create-Directories -computerName $sentinelServer.hostname -credential $credential
    Copy-RedisFiles -computerName $sentinelServer.hostname -configFile $sentinelServer.sentinel_config -credential $credential
    Install-RedisService -computerName $sentinelServer.hostname -serviceName "RedisSentinel" -configFile $sentinelServer.sentinel_config -credential $credential

    # Deploy to sentinel nodes
    foreach ($node in $building.sentinel_nodes) {
        Write-Host "Deploying Sentinel to $($node.hostname)..."
        
        Create-Directories -computerName $node.hostname -credential $credential
        Copy-RedisFiles -computerName $node.hostname -configFile $node.sentinel_config -credential $credential
        Install-RedisService -computerName $node.hostname -serviceName "RedisSentinel" -configFile $node.sentinel_config -credential $credential
    }
}

# Main deployment script
try {
    # Get credentials for remote deployment
    $credential = Get-Credential -Message "Enter credentials for remote deployment"

    # Deploy to both buildings
    Deploy-ToBuilding -buildingId "building1" -credential $credential
    Deploy-ToBuilding -buildingId "building2" -credential $credential

    Write-Host "Deployment completed successfully!"
}
catch {
    Write-Error "Deployment failed: $_"
    exit 1
}
