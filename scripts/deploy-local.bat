@echo off
setlocal enabledelayedexpansion

:: Local deployment script for Redis and Sentinel
echo Starting local Redis deployment...

:: Set paths
set REDIS_PATH=C:\Redis
set CONFIG_PATH=C:\Redis\config
set LOG_PATH=C:\Redis\logs
set DATA_PATH=C:\Redis\data

:: Create directories
echo Creating directories...
if not exist "%REDIS_PATH%" mkdir "%REDIS_PATH%"
if not exist "%CONFIG_PATH%" mkdir "%CONFIG_PATH%"
if not exist "%LOG_PATH%" mkdir "%LOG_PATH%"
if not exist "%DATA_PATH%" mkdir "%DATA_PATH%"

:: Copy configuration files
echo Copying configuration files...
copy /Y "..\config\redis\*.conf" "%CONFIG_PATH%"

:: Determine which configuration to use based on hostname
for /f "tokens=*" %%i in ('hostname') do set HOSTNAME=%%i

:: Set default configuration
set REDIS_CONFIG=
set SENTINEL_CONFIG=
set IS_REDIS_SERVER=false

:: Check hostname against configuration
powershell -Command "$config = Get-Content ..\config\redis\deployment-config.json | ConvertFrom-Json; foreach ($building in $config.buildings.PSObject.Properties) { $redis = $building.Value.redis_server; if ($redis.hostname -eq '%HOSTNAME%') { Write-Host 'redis:' $redis.redis_config; Write-Host 'sentinel:' $redis.sentinel_config; exit 0 } }"

if %ERRORLEVEL% EQU 0 (
    set IS_REDIS_SERVER=true
    for /f "tokens=1,2" %%a in ('powershell -Command "$config = Get-Content ..\config\redis\deployment-config.json | ConvertFrom-Json; foreach ($building in $config.buildings.PSObject.Properties) { $redis = $building.Value.redis_server; if ($redis.hostname -eq '%HOSTNAME%') { Write-Host 'redis:' $redis.redis_config; Write-Host 'sentinel:' $redis.sentinel_config; exit 0 } }"') do (
        if "%%a"=="redis:" set REDIS_CONFIG=%%b
        if "%%a"=="sentinel:" set SENTINEL_CONFIG=%%b
    )
) else (
    :: Check if it's a sentinel node
    powershell -Command "$config = Get-Content ..\config\redis\deployment-config.json | ConvertFrom-Json; foreach ($building in $config.buildings.PSObject.Properties) { foreach ($node in $building.Value.sentinel_nodes) { if ($node.hostname -eq '%HOSTNAME%') { Write-Host 'sentinel:' $node.sentinel_config; exit 0 } } }"
    
    if %ERRORLEVEL% EQU 0 (
        for /f "tokens=1,2" %%a in ('powershell -Command "$config = Get-Content ..\config\redis\deployment-config.json | ConvertFrom-Json; foreach ($building in $config.buildings.PSObject.Properties) { foreach ($node in $building.Value.sentinel_nodes) { if ($node.hostname -eq '%HOSTNAME%') { Write-Host 'sentinel:' $node.sentinel_config; exit 0 } } }"') do (
            if "%%a"=="sentinel:" set SENTINEL_CONFIG=%%b
        )
    )
)

:: Install and start services
if "%IS_REDIS_SERVER%"=="true" (
    echo Installing Redis service...
    sc create Redis binPath= "\"%REDIS_PATH%\redis-server.exe\" \"%CONFIG_PATH%\%REDIS_CONFIG%\" --service-run" start= auto
    net start Redis
)

if not "%SENTINEL_CONFIG%"=="" (
    echo Installing Sentinel service...
    sc create RedisSentinel binPath= "\"%REDIS_PATH%\redis-server.exe\" \"%CONFIG_PATH%\%SENTINEL_CONFIG%\" --service-run --sentinel" start= auto
    net start RedisSentinel
)

echo Deployment completed!
pause
