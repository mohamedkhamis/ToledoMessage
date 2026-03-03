<#
.SYNOPSIS
    Deploy ToledoMessage to local IIS for testing.

.DESCRIPTION
    Publishes the ToledoMessage ASP.NET Core + Blazor WASM app to local IIS.
    Supports Deploy, Uninstall, and Status actions.

.PARAMETER Action
    Deploy (default), Uninstall, or Status.

.PARAMETER SiteName
    IIS site and app pool name. Default: ToledoMessage

.PARAMETER Port
    HTTP port. 0 = auto-select from 8080-8099. Default: 0

.PARAMETER PublishPath
    Publish output folder. Default: C:\inetpub\ToledoMessage

.PARAMETER Environment
    ASPNETCORE_ENVIRONMENT value. Default: Production

.PARAMETER ConnectionString
    SQL Server connection string override.

.PARAMETER JwtSecretKey
    JWT signing key (min 32 chars). Required for Deploy.

.PARAMETER Force
    Skip confirmation prompts.

.EXAMPLE
    .\deploy-iis.ps1 -JwtSecretKey "my-secure-key-min-32-chars-long!!"

.EXAMPLE
    .\deploy-iis.ps1 -Port 8085 -JwtSecretKey "my-key-32-chars-long!!"

.EXAMPLE
    .\deploy-iis.ps1 -Action Status

.EXAMPLE
    .\deploy-iis.ps1 -Action Uninstall
#>

[CmdletBinding()]
param(
    [ValidateSet("Deploy", "Uninstall", "Status")]
    [string]$Action = "Deploy",

    [string]$SiteName = "ToledoMessage",

    [int]$Port = 8080,

    [string]$PublishPath = "C:\inetpub\ToledoMessage",

    [string]$Environment = "Production",

    [string]$ConnectionString = "",

    [string]$JwtSecretKey = "TjstMZVqlbv1ibCozYAKKSVa_HfyMtH7Nh7Ohh9XtnSgZtdzvWfsmAPsvGcvQj58",

    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# -- Colours ---------------------------------------------------------------
function Write-Step   { param([string]$Msg) Write-Host "[*] $Msg" -ForegroundColor Cyan }
function Write-Ok     { param([string]$Msg) Write-Host "[+] $Msg" -ForegroundColor Green }
function Write-Warn   { param([string]$Msg) Write-Host "[!] $Msg" -ForegroundColor Yellow }
function Write-Err    { param([string]$Msg) Write-Host "[-] $Msg" -ForegroundColor Red }
function Write-Info   { param([string]$Msg) Write-Host "    $Msg" -ForegroundColor Gray }

# -- Helpers ---------------------------------------------------------------

function Assert-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($id)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Write-Err "This script must be run as Administrator."
        Write-Info "Right-click PowerShell -> Run as Administrator, then re-run."
        exit 1
    }
}

function Assert-Prerequisites {
    Write-Step "Checking prerequisites..."

    # .NET SDK
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        Write-Err ".NET SDK not found. Install from https://dot.net/download"
        exit 1
    }
    $sdkList = & dotnet --list-sdks 2>&1
    $hasNet10 = $sdkList | Where-Object { $_ -match "^10\." }
    if (-not $hasNet10) {
        Write-Err ".NET 10 SDK not found. Installed SDKs:"
        $sdkList | ForEach-Object { Write-Info $_ }
        exit 1
    }
    Write-Ok ".NET 10 SDK found"

    # IIS
    $iisFeature = Get-WindowsOptionalFeature -Online -FeatureName IIS-WebServer -ErrorAction SilentlyContinue
    if (-not $iisFeature -or $iisFeature.State -ne "Enabled") {
        Write-Err "IIS is not installed/enabled."
        Write-Info "Enable via: Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServer -All"
        exit 1
    }
    Write-Ok "IIS is enabled"

    # WebSocket Protocol
    $wsFeature = Get-WindowsOptionalFeature -Online -FeatureName IIS-WebSockets -ErrorAction SilentlyContinue
    if (-not $wsFeature -or $wsFeature.State -ne "Enabled") {
        Write-Warn "IIS WebSocket Protocol is not enabled. SignalR needs it."
        Write-Info "Enabling IIS-WebSockets..."
        Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebSockets -All -NoRestart | Out-Null
        Write-Ok "IIS WebSocket Protocol enabled"
    } else {
        Write-Ok "IIS WebSocket Protocol enabled"
    }

    # ASP.NET Core Hosting Bundle - check for aspnetcorev2 module or handler
    $ancmPath = Join-Path $env:ProgramFiles "IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
    $ancmPath2 = Join-Path ${env:ProgramFiles(x86)} "IIS\Asp.Net Core Module\V2\aspnetcorev2.dll" 2>$null
    $ancmInSystem = Join-Path $env:SystemRoot "System32\inetsrv\aspnetcorev2.dll"
    if (-not (Test-Path $ancmPath) -and -not (Test-Path $ancmInSystem) -and -not ($ancmPath2 -and (Test-Path $ancmPath2))) {
        Write-Err "ASP.NET Core Hosting Bundle not found."
        Write-Info "Download from: https://dotnet.microsoft.com/download/dotnet/10.0"
        Write-Info "Install the 'Hosting Bundle' (not just the runtime)."
        exit 1
    }
    Write-Ok "ASP.NET Core Hosting Bundle detected"

    # IISAdministration module
    if (-not (Get-Module -ListAvailable -Name IISAdministration)) {
        Write-Warn "IISAdministration module not found. Installing..."
        Install-Module -Name IISAdministration -Force -AllowClobber -Scope CurrentUser
    }
    Import-Module IISAdministration
    Write-Ok "IISAdministration module loaded"

    # Unlock IIS config sections needed by ASP.NET Core web.config
    Write-Step "Unlocking IIS configuration sections..."
    $sectionsToUnlock = @(
        "system.webServer/handlers",
        "system.webServer/modules"
    )
    foreach ($section in $sectionsToUnlock) {
        & "$env:SystemRoot\System32\inetsrv\appcmd.exe" unlock config /section:$section 2>&1 | Out-Null
    }
    Write-Ok "IIS configuration sections unlocked"
}

function Find-FreePort {
    param([int]$RangeStart = 8080, [int]$RangeEnd = 8099)

    $usedPorts = (Get-NetTCPConnection -ErrorAction SilentlyContinue |
        Where-Object { $_.State -eq "Listen" } |
        Select-Object -ExpandProperty LocalPort -Unique)

    for ($p = $RangeStart; $p -le $RangeEnd; $p++) {
        if ($p -notin $usedPorts) {
            return $p
        }
    }
    Write-Err "No free port found in range $RangeStart-$RangeEnd."
    Write-Info "Specify a port manually with -Port <number>"
    exit 1
}

function Publish-App {
    param([string]$OutputPath)

    $projectPath = Join-Path $PSScriptRoot "src\ToledoMessage\ToledoMessage.csproj"
    if (-not (Test-Path $projectPath)) {
        Write-Err "Project not found at: $projectPath"
        exit 1
    }

    Write-Step "Publishing app to $OutputPath ..."
    & dotnet publish $projectPath `
        --configuration Release `
        --output $OutputPath `
        --no-self-contained 2>&1 | ForEach-Object {
        if ($_ -match "error") { Write-Err $_ } else { Write-Info $_ }
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Err "dotnet publish failed (exit code $LASTEXITCODE)."
        exit 1
    }
    Write-Ok "Publish succeeded"
}

function Write-WebConfig {
    param([string]$OutputPath, [string]$Env)

    $webConfigPath = Join-Path $OutputPath "web.config"
    $xml = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet"
                  arguments=".\ToledoMessage.dll"
                  stdoutLogEnabled="true"
                  stdoutLogFile=".\logs\stdout"
                  hostingModel="InProcess">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="$Env" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
"@
    Set-Content -Path $webConfigPath -Value $xml -Encoding UTF8
    Write-Ok "web.config written (InProcess, WebSocket enabled, stdout logging)"

    # Ensure logs directory exists
    $logsDir = Join-Path $OutputPath "logs"
    if (-not (Test-Path $logsDir)) {
        New-Item -ItemType Directory -Path $logsDir -Force | Out-Null
    }
}

function Write-AppSettingsProduction {
    param(
        [string]$OutputPath,
        [string]$ConnStr,
        [string]$JwtKey,
        [int]$PortNum = 8080
    )

    $settingsPath = Join-Path $OutputPath "appsettings.Production.json"
    $settings = @{
        ConnectionStrings = @{
            DefaultConnection = $ConnStr
        }
        Jwt = @{
            SecretKey = $JwtKey
            Issuer = "ToledoMessage"
            Audience = "ToledoMessage"
        }
        Cors = @{
            AllowedOrigins = @("http://localhost:$PortNum")
        }
        Logging = @{
            LogLevel = @{
                Default = "Warning"
                "Microsoft.AspNetCore" = "Warning"
            }
        }
    }
    $settings | ConvertTo-Json -Depth 4 | Set-Content -Path $settingsPath -Encoding UTF8
    Write-Ok "appsettings.Production.json written (in publish dir, not source)"
}

function Setup-AppPool {
    param([string]$Name)

    $mgr = Get-IISServerManager
    $pool = $mgr.ApplicationPools[$Name]

    if ($pool) {
        Write-Warn "App pool '$Name' already exists - updating settings"
    } else {
        Write-Step "Creating app pool '$Name'..."
        $pool = $mgr.ApplicationPools.Add($Name)
    }

    $pool.ManagedRuntimeVersion = ""                         # No Managed Code
    $pool.ProcessModel.IdleTimeout = [TimeSpan]::Zero        # Never idle out (SignalR)
    $pool.StartMode = 1                                      # AlwaysRunning (enum: 0=OnDemand, 1=AlwaysRunning)
    $pool.AutoStart = $true

    $mgr.CommitChanges()
    Write-Ok "App pool '$Name' configured (No Managed Code, AlwaysRunning, IdleTimeout=0)"
}

function Setup-Site {
    param(
        [string]$Name,
        [string]$PoolName,
        [int]$PortNum,
        [string]$PhysicalPath
    )

    $mgr = Get-IISServerManager
    $site = $mgr.Sites[$Name]

    if ($site) {
        Write-Warn "Site '$Name' already exists - updating"
        $mgr.Sites.Remove($site)
        $mgr.CommitChanges()
        $mgr = Get-IISServerManager
    }

    Write-Step "Creating IIS site '$Name' on port $PortNum..."
    $site = $mgr.Sites.Add($Name, "http", "*:${PortNum}:", $PhysicalPath)
    $site.ApplicationDefaults.ApplicationPoolName = $PoolName
    $site.Applications["/"].ApplicationPoolName = $PoolName

    $mgr.CommitChanges()
    Write-Ok "Site '$Name' bound to http://localhost:$PortNum"
}

function Set-FolderPermissions {
    param(
        [string]$Path,
        [string]$PoolName
    )

    Write-Step "Setting folder permissions for IIS AppPool\$PoolName..."
    $identity = "IIS AppPool\$PoolName"
    $acl = Get-Acl $Path
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        $identity, "Modify", "ContainerInherit,ObjectInherit", "None", "Allow"
    )
    $acl.AddAccessRule($rule)
    Set-Acl -Path $Path -AclObject $acl
    Write-Ok "Modify permission granted to $identity on $Path"
}

function Start-IISSite {
    param([string]$Name)

    Write-Step "Starting app pool and site..."

    # Start app pool
    $pool = Get-IISAppPool -Name $Name -ErrorAction SilentlyContinue
    if ($pool -and $pool.State -ne "Started") {
        Start-IISCommitDelay
        $pool = (Get-IISServerManager).ApplicationPools[$Name]
        $pool.Start()
        Stop-IISCommitDelay
    }

    # Start site
    $mgr = Get-IISServerManager
    $site = $mgr.Sites[$Name]
    if ($site -and $site.State -ne "Started") {
        Start-IISCommitDelay
        $site.Start()
        Stop-IISCommitDelay
    }

    Write-Ok "App pool and site started"
}

# -- Actions ---------------------------------------------------------------

function Invoke-Deploy {
    Assert-Admin
    Assert-Prerequisites

    # Validate JWT key
    if ([string]::IsNullOrWhiteSpace($JwtSecretKey)) {
        Write-Err "-JwtSecretKey is required for deployment."
        Write-Info 'Example: .\deploy-iis.ps1 -JwtSecretKey "my-secure-key-min-32-chars-long!!"'
        exit 1
    }
    if ($JwtSecretKey.Length -lt 32) {
        Write-Err "JwtSecretKey must be at least 32 characters."
        exit 1
    }

    # Resolve connection string
    $connStr = $ConnectionString
    if ([string]::IsNullOrWhiteSpace($connStr)) {
        $connStr = "Server=.;Database=ToledoMessage;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
    }

    # Auto-select port
    $selectedPort = $Port
    if ($selectedPort -eq 0) {
        $selectedPort = Find-FreePort
        Write-Ok "Auto-selected port: $selectedPort"
    } else {
        # Check if specified port is in use
        $usedPorts = (Get-NetTCPConnection -ErrorAction SilentlyContinue |
            Where-Object { $_.State -eq "Listen" } |
            Select-Object -ExpandProperty LocalPort -Unique)
        if ($selectedPort -in $usedPorts) {
            Write-Warn "Port $selectedPort is already in use."
            if (-not $Force) {
                $reply = Read-Host "Continue anyway? (y/N)"
                if ($reply -ne "y") { exit 0 }
            }
        }
    }

    # Confirm
    if (-not $Force) {
        Write-Host ""
        Write-Host "  Deployment Summary" -ForegroundColor White
        Write-Host "  --------------------------------------"
        Write-Info "  Site Name:    $SiteName"
        Write-Info "  Port:         $selectedPort"
        Write-Info "  Publish Path: $PublishPath"
        Write-Info "  Environment:  $Environment"
        Write-Info "  Conn String:  $connStr"
        Write-Host ""
        $reply = Read-Host "Proceed? (Y/n)"
        if ($reply -eq "n") { exit 0 }
    }

    # Stop existing site/pool if running
    $existingPool = Get-IISAppPool -Name $SiteName -ErrorAction SilentlyContinue
    if ($existingPool -and $existingPool.State -eq "Started") {
        Write-Step "Stopping existing app pool '$SiteName'..."
        $mgr = Get-IISServerManager
        $mgr.ApplicationPools[$SiteName].Stop()
        $mgr.CommitChanges()
        Start-Sleep -Seconds 2
    }

    # Publish
    Publish-App -OutputPath $PublishPath

    # web.config
    Write-WebConfig -OutputPath $PublishPath -Env $Environment

    # appsettings.Production.json
    Write-AppSettingsProduction -OutputPath $PublishPath -ConnStr $connStr -JwtKey $JwtSecretKey -PortNum $selectedPort

    # App Pool
    Setup-AppPool -Name $SiteName

    # Site
    Setup-Site -Name $SiteName -PoolName $SiteName -PortNum $selectedPort -PhysicalPath $PublishPath

    # Folder permissions
    Set-FolderPermissions -Path $PublishPath -PoolName $SiteName

    # Start
    Start-IISSite -Name $SiteName

    # Result
    Write-Host ""
    Write-Host "  =============================================" -ForegroundColor Green
    Write-Host "  Deployment complete!" -ForegroundColor Green
    Write-Host "  =============================================" -ForegroundColor Green
    Write-Host ""
    Write-Info "  App:     http://localhost:$selectedPort"
    Write-Info "  API:     http://localhost:$selectedPort/api/auth/login"
    Write-Info "  SignalR:  http://localhost:$selectedPort/hubs/chat"
    Write-Info "  Logs:    $PublishPath\logs\stdout*.log"
    Write-Host ""

    # SQL Server instructions
    Write-Host "  -- SQL Server Setup --------------------------" -ForegroundColor Yellow
    Write-Host ""
    Write-Info "  If using Windows Authentication (Trusted_Connection=True),"
    Write-Info "  grant the IIS app pool identity access to SQL Server:"
    Write-Host ""
    Write-Host "    USE [master]" -ForegroundColor White
    Write-Host "    CREATE LOGIN [IIS AppPool\$SiteName] FROM WINDOWS;" -ForegroundColor White
    Write-Host "    USE [ToledoMessage]" -ForegroundColor White
    Write-Host "    CREATE USER [IIS AppPool\$SiteName] FOR LOGIN [IIS AppPool\$SiteName];" -ForegroundColor White
    Write-Host "    ALTER ROLE [db_owner] ADD MEMBER [IIS AppPool\$SiteName];" -ForegroundColor White
    Write-Host ""
    Write-Info "  Run these commands in SSMS or sqlcmd."
    Write-Host ""
}

function Invoke-Uninstall {
    Assert-Admin

    if (-not (Get-Module -ListAvailable -Name IISAdministration)) {
        Write-Err "IISAdministration module not found."
        exit 1
    }
    Import-Module IISAdministration

    if (-not $Force) {
        Write-Warn "This will remove the IIS site '$SiteName', app pool, and published files at '$PublishPath'."
        $reply = Read-Host "Are you sure? (y/N)"
        if ($reply -ne "y") { exit 0 }
    }

    # Stop and remove site
    $mgr = Get-IISServerManager
    $site = $mgr.Sites[$SiteName]
    if ($site) {
        Write-Step "Removing site '$SiteName'..."
        if ($site.State -eq "Started") {
            $site.Stop()
        }
        $mgr.Sites.Remove($site)
        $mgr.CommitChanges()
        Write-Ok "Site removed"
    } else {
        Write-Info "Site '$SiteName' not found (already removed)"
    }

    # Stop and remove app pool
    $mgr = Get-IISServerManager
    $pool = $mgr.ApplicationPools[$SiteName]
    if ($pool) {
        Write-Step "Removing app pool '$SiteName'..."
        if ($pool.State -eq "Started") {
            $pool.Stop()
            Start-Sleep -Seconds 2
        }
        $mgr.ApplicationPools.Remove($pool)
        $mgr.CommitChanges()
        Write-Ok "App pool removed"
    } else {
        Write-Info "App pool '$SiteName' not found (already removed)"
    }

    # Remove publish folder
    if (Test-Path $PublishPath) {
        Write-Step "Removing published files at $PublishPath..."
        Remove-Item -Path $PublishPath -Recurse -Force
        Write-Ok "Published files removed"
    } else {
        Write-Info "Publish folder not found (already removed)"
    }

    Write-Host ""
    Write-Ok "Uninstall complete."
}

function Invoke-Status {
    if (-not (Get-Module -ListAvailable -Name IISAdministration)) {
        Write-Err "IISAdministration module not found."
        exit 1
    }
    Import-Module IISAdministration

    Write-Host ""
    Write-Host "  ToledoMessage IIS Status" -ForegroundColor White
    Write-Host "  --------------------------------------"

    # App Pool
    $pool = Get-IISAppPool -Name $SiteName -ErrorAction SilentlyContinue
    if ($pool) {
        $poolColor = "Red"; if ($pool.State -eq "Started") { $poolColor = "Green" }
        Write-Host "  App Pool:  $SiteName - $($pool.State)" -ForegroundColor $poolColor
    } else {
        Write-Host "  App Pool:  $SiteName - NOT FOUND" -ForegroundColor Red
    }

    # Site
    $mgr = Get-IISServerManager
    $site = $mgr.Sites[$SiteName]
    if ($site) {
        $siteColor = "Red"; if ($site.State -eq "Started") { $siteColor = "Green" }
        Write-Host "  Site:      $SiteName - $($site.State)" -ForegroundColor $siteColor

        foreach ($binding in $site.Bindings) {
            $info = $binding.BindingInformation   # e.g. "*:8080:"
            $bPort = ($info -split ":")[1]
            Write-Host "  URL:       http://localhost:$bPort" -ForegroundColor Cyan
        }
    } else {
        Write-Host "  Site:      $SiteName - NOT FOUND" -ForegroundColor Red
    }

    # Publish path
    if (Test-Path $PublishPath) {
        $dllPath = Join-Path $PublishPath "ToledoMessage.dll"
        if (Test-Path $dllPath) {
            $dllInfo = Get-Item $dllPath
            Write-Host "  Published: $PublishPath" -ForegroundColor Green
            Write-Host "  DLL Date:  $($dllInfo.LastWriteTime)" -ForegroundColor Gray
        } else {
            Write-Host "  Published: $PublishPath (DLL not found)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  Published: $PublishPath - NOT FOUND" -ForegroundColor Red
    }

    # Recent logs
    $logsDir = Join-Path $PublishPath "logs"
    if (Test-Path $logsDir) {
        $latestLog = Get-ChildItem -Path $logsDir -Filter "stdout*.log" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($latestLog) {
            Write-Host "  Latest Log: $($latestLog.FullName)" -ForegroundColor Gray
            Write-Host "  Log Date:   $($latestLog.LastWriteTime)" -ForegroundColor Gray
            Write-Host ""
            Write-Host "  Last 10 log lines:" -ForegroundColor Yellow
            Get-Content $latestLog.FullName -Tail 10 | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
        }
    }

    Write-Host ""
}

# -- Main ------------------------------------------------------------------

switch ($Action) {
    "Deploy"    { Invoke-Deploy }
    "Uninstall" { Invoke-Uninstall }
    "Status"    { Invoke-Status }
}
