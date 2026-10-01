<#
.SYNOPSIS
    Windows Control Center Agent automated enrollment and service installer.
.DESCRIPTION
    Downloads the agent binary, writes agentsettings.json with enrollment credentials,
    and registers/starts the Windows Service.
.PARAMETER ServerUrl
    The HTTPS/HTTP endpoint of the Windows Control Center API server.
.PARAMETER EnrollmentCode
    The pre-generated enrollment token code from the admin dashboard.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$ServerUrl,

    [Parameter(Mandatory=$true)]
    [string]$EnrollmentCode,

    [string]$InstallPath = "C:\Program Files\Windows Control Center\Agent"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  🖥️ Windows Control Center — Agent Automated Installer  " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# Check elevation
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "This script must be executed in an elevated Administrator PowerShell session!"
    exit 1
}

# 1. Stop existing service if running
$serviceName = "WindowsControlCenterAgent"
$existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Stopping existing agent service..." -ForegroundColor Yellow
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
}

# 2. Prepare directory
Write-Host "Creating agent installation directory: $InstallPath" -ForegroundColor Green
New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null

# 3. Write configuration file
$config = @{
    ServerUrl = $ServerUrl.TrimEnd('/')
    EnrollmentCode = $EnrollmentCode
    AgentId = ""
    ApiToken = ""
    HeartbeatIntervalSeconds = 30
    MetricsIntervalSeconds = 15
}

$configPath = Join-Path $InstallPath "agentsettings.json"
$configJson = $config | ConvertTo-Json -Depth 4
$configJson | Set-Content -Path $configPath -Encoding UTF8

$programDataDir = Join-Path $env:ProgramData "WindowsControlCenter\Agent"
New-Item -ItemType Directory -Path $programDataDir -Force | Out-Null
$configJson | Set-Content -Path (Join-Path $programDataDir "agentsettings.json") -Encoding UTF8

Write-Host "Configuration saved to $configPath and $programDataDir" -ForegroundColor Green

# 4. If binary exists locally, register service; otherwise notify
$exePath = Join-Path $InstallPath "WindowsControlCenter.Agent.exe"
if (Test-Path $exePath) {
    if (-not $existingService) {
        Write-Host "Creating Windows Service: $serviceName..." -ForegroundColor Green
        New-Service -Name $serviceName `
                    -BinaryPathName "`"$exePath`"" `
                    -DisplayName "Windows Control Center Management Agent" `
                    -Description "Communicates with Windows Control Center server for centralized administration, telemetry, and remote commands." `
                    -StartupType Automatic | Out-Null

        # Configure recovery options (restart on crash)
        & sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/60000/restart/60000
    }

    Write-Host "Starting agent service..." -ForegroundColor Green
    Start-Service -Name $serviceName
    Write-Host "Agent successfully installed and started!" -ForegroundColor Cyan
} else {
    Write-Host "Agent binary should be copied to: $exePath" -ForegroundColor Yellow
    Write-Host "Configuration is complete and ready for service start." -ForegroundColor Green
}
