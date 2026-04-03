# SonarQube setup and analysis workflow for the Unity multiplayer project.
# Uses SonarScanner for .NET in the supported begin -> build -> end flow.

param(
    [switch]$StartSonarQube,
    [switch]$StopSonarQube,
    [switch]$RunAnalysis,
    [switch]$SetupProject,
    [string]$ProjectKey,
    [string]$MainBuildTarget = "",
    [string]$CoverageReportPath = "CodeCoverage/Report/SonarQube.xml",
    [string]$SonarConfigPath = "Tools/CodeAnalysis/sonar-project.properties"
)

$SONARQUBE_PORT = 9000
$SONARQUBE_CONTAINER = "sonarqube"
$SONARQUBE_IMAGE = "sonarqube:community"
$SONARQUBE_URL = "http://localhost:$SONARQUBE_PORT"

function Write-Header {
    param([string]$Message)
    Write-Host "`n=== $Message ===" -ForegroundColor Cyan
}

function Get-ProjectRoot {
    return (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent)
}

function Resolve-ProjectPath {
    param([string]$RelativePath)

    $projectRoot = Get-ProjectRoot
    return [System.IO.Path]::GetFullPath((Join-Path $projectRoot $RelativePath))
}

function Test-Docker {
    try {
        $null = docker --version
        return $true
    } catch {
        Write-Error "Docker is not installed or not running."
        return $false
    }
}

function Test-DotNet {
    try {
        $null = dotnet --version
        return $true
    } catch {
        Write-Error ".NET SDK is not installed."
        return $false
    }
}

function Ensure-DotNetTool {
    param(
        [string]$PackageId,
        [string]$CommandName
    )

    $toolList = dotnet tool list --global
    if ($toolList -notmatch [regex]::Escape($PackageId)) {
        Write-Host "Installing $PackageId..." -ForegroundColor Yellow
        dotnet tool install --global $PackageId
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to install $PackageId."
        }
    }

    try {
        $null = & $CommandName "--version" 2>$null
    } catch {
        # Some dotnet tools only resolve through `dotnet tool run` during the same session;
        # the global install check above is the authoritative signal.
    }
}

function Get-SonarServerVersion {
    try {
        return (Invoke-RestMethod -Uri "$SONARQUBE_URL/api/server/version" -TimeoutSec 5)
    } catch {
        return $null
    }
}

function Get-SonarProperties {
    param([string]$ConfigPath)

    if (-not (Test-Path $ConfigPath)) {
        throw "Sonar config file not found: $ConfigPath"
    }

    $properties = [ordered]@{}
    foreach ($line in Get-Content $ConfigPath) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith("#")) {
            continue
        }

        $parts = $trimmed -split "=", 2
        if ($parts.Count -ne 2) {
            continue
        }

        $properties[$parts[0].Trim()] = $parts[1].Trim()
    }

    return $properties
}

function Get-SonarToken {
    if ($env:SONAR_TOKEN) {
        return $env:SONAR_TOKEN
    }

    if ($env:SONARQUBE_TOKEN) {
        return $env:SONARQUBE_TOKEN
    }

    return $null
}

function Start-SonarQube {
    Write-Header "Starting SonarQube"

    if (-not (Test-Docker)) { return }

    $existing = docker ps -a --filter "name=$SONARQUBE_CONTAINER" --format "{{.Names}}"
    if ($existing -eq $SONARQUBE_CONTAINER) {
        Write-Host "Starting existing SonarQube container..." -ForegroundColor Yellow
        docker start $SONARQUBE_CONTAINER | Out-Null
    } else {
        Write-Host "Creating SonarQube container using $SONARQUBE_IMAGE..." -ForegroundColor Yellow
        docker run -d --name $SONARQUBE_CONTAINER -p ${SONARQUBE_PORT}:9000 $SONARQUBE_IMAGE | Out-Null
    }

    $maxRetries = 60
    for ($retry = 1; $retry -le $maxRetries; $retry++) {
        $version = Get-SonarServerVersion
        if ($version) {
            Write-Host "SonarQube is ready at $SONARQUBE_URL (server $version)." -ForegroundColor Green
            return
        }

        Write-Host "Waiting for SonarQube... ($retry/$maxRetries)"
        Start-Sleep -Seconds 5
    }

    throw "SonarQube failed to start properly."
}

function Stop-SonarQube {
    Write-Header "Stopping SonarQube"

    if (-not (Test-Docker)) { return }

    docker stop $SONARQUBE_CONTAINER 2>$null | Out-Null
    docker rm $SONARQUBE_CONTAINER 2>$null | Out-Null
    Write-Host "SonarQube stopped and container removed." -ForegroundColor Green
}

function Setup-Project {
    Write-Header "Checking SonarQube Tooling"

    if (-not (Test-DotNet)) { return }

    Ensure-DotNetTool -PackageId "dotnet-sonarscanner" -CommandName "dotnet-sonarscanner"

    $configPath = Resolve-ProjectPath $SonarConfigPath
    $coveragePath = Resolve-ProjectPath $CoverageReportPath

    Write-Host "Project root      : $(Get-ProjectRoot)"
    Write-Host "Sonar config      : $configPath"
    Write-Host "Coverage report   : $coveragePath"
    $scannerVersion = (
        dotnet tool list --global |
        Select-String "dotnet-sonarscanner" |
        ForEach-Object { ($_ -split "\s+")[1] } |
        Select-Object -First 1
    )
    Write-Host "Scanner version   : $scannerVersion"

    $serverVersion = Get-SonarServerVersion
    if ($serverVersion) {
        Write-Host "Server version    : $serverVersion"
    } else {
        Write-Host "Server version    : unavailable (SonarQube is not currently responding)" -ForegroundColor Yellow
    }

    if (-not (Test-Path $configPath)) {
        throw "Expected Sonar config file is missing: $configPath"
    }

    if (-not (Test-Path $coveragePath)) {
        Write-Host "Coverage report is missing. Analysis can still run, but coverage will remain 0 until Unity regenerates $CoverageReportPath." -ForegroundColor Yellow
    } else {
        $coverageItem = Get-Item $coveragePath
        Write-Host "Coverage updated  : $($coverageItem.LastWriteTime)"
    }
}

function Sync-UnityCompiledAssemblies {
    $projectRoot = Get-ProjectRoot
    $unityAssembliesPath = Join-Path $projectRoot "Library\ScriptAssemblies"
    $msbuildOutputPath = Join-Path $projectRoot "Temp\bin\Debug"

    if (-not (Test-Path $unityAssembliesPath)) {
        Write-Host "Unity compiled assemblies were not found at $unityAssembliesPath." -ForegroundColor Yellow
        return
    }

    New-Item -ItemType Directory -Path $msbuildOutputPath -Force | Out-Null

    $assemblyCount = 0
    foreach ($assembly in Get-ChildItem -Path $unityAssembliesPath -Filter "*.dll" -File) {
        Copy-Item -Path $assembly.FullName -Destination (Join-Path $msbuildOutputPath $assembly.Name) -Force
        $assemblyCount++
    }

    Write-Host "Synced $assemblyCount Unity-generated assemblies into $msbuildOutputPath." -ForegroundColor Green
}

function Get-FirstPartyProjects {
    param([string]$MainBuildTarget)

    $projectRoot = Get-ProjectRoot

    if (-not [string]::IsNullOrWhiteSpace($MainBuildTarget)) {
        $resolvedTarget = Resolve-ProjectPath $MainBuildTarget
        if (-not (Test-Path $resolvedTarget)) {
            throw "Main build target not found: $resolvedTarget"
        }

        return @(Get-Item $resolvedTarget)
    }

    return Get-ChildItem -Path $projectRoot -Filter "Network_Game*.csproj" -File |
        Where-Object { $_.Name -notmatch "\.Tests\." } |
        Sort-Object Name
}

function Get-TestProjects {
    $projectRoot = Get-ProjectRoot

    return Get-ChildItem -Path $projectRoot -Filter "Network_Game*.csproj" -File |
        Where-Object { $_.Name -match "\.Tests\." } |
        Sort-Object Name
}

function Get-BeginArguments {
    param(
        $Properties,
        [string]$OverrideProjectKey
    )

    $key = if ([string]::IsNullOrWhiteSpace($OverrideProjectKey)) {
        $Properties["sonar.projectKey"]
    } else {
        $OverrideProjectKey
    }

    if ([string]::IsNullOrWhiteSpace($key)) {
        throw "A Sonar project key is required."
    }

    $name = $Properties["sonar.projectName"]
    $version = $Properties["sonar.projectVersion"]

    $arguments = @("begin", "/k:$key")
    if ($name) { $arguments += "/n:$name" }
    if ($version) { $arguments += "/v:$version" }

    foreach ($entry in $Properties.GetEnumerator()) {
        if ($entry.Key -in @("sonar.projectKey", "sonar.projectName", "sonar.projectVersion")) {
            continue
        }

        $arguments += "/d:$($entry.Key)=$($entry.Value)"
    }

    $token = Get-SonarToken
    if ($token) {
        $arguments += "/d:sonar.token=$token"
    }

    return $arguments
}

function Get-EndArguments {
    $arguments = @("end")

    $token = Get-SonarToken
    if ($token) {
        $arguments += "/d:sonar.token=$token"
    }

    return $arguments
}

function Invoke-DotNet {
    param(
        [string[]]$Arguments,
        [string]$Description
    )

    Write-Host $Description -ForegroundColor Yellow
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

function Invoke-SonarScanner {
    param(
        [string[]]$Arguments,
        [string]$Description
    )

    Write-Host $Description -ForegroundColor Yellow
    & dotnet-sonarscanner @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

function Run-Analysis {
    Write-Header "Running SonarQube Analysis"

    if (-not (Test-DotNet)) { return }

    $serverVersion = Get-SonarServerVersion
    if (-not $serverVersion) {
        throw "SonarQube is not running at $SONARQUBE_URL. Start it first."
    }

    $configPath = Resolve-ProjectPath $SonarConfigPath
    $coveragePath = Resolve-ProjectPath $CoverageReportPath
    $projectRoot = Get-ProjectRoot

    $properties = Get-SonarProperties -ConfigPath $configPath

    if (-not (Test-Path $coveragePath)) {
        Write-Host "Coverage report not found at $coveragePath. The analysis will run without imported coverage." -ForegroundColor Yellow
        $properties.Remove("sonar.coverageReportPaths")
    }

    $firstPartyProjects = Get-FirstPartyProjects -MainBuildTarget $MainBuildTarget
    $testProjects = Get-TestProjects

    if ($firstPartyProjects.Count -eq 0) {
        throw "No first-party Network_Game projects were found."
    }

    if ($testProjects.Count -eq 0) {
        Write-Host "No first-party Unity test projects were detected." -ForegroundColor Yellow
    }

    $failedProjects = New-Object System.Collections.Generic.List[string]
    $scannerStarted = $false

    Push-Location $projectRoot
    try {
        $beginArguments = Get-BeginArguments -Properties $properties -OverrideProjectKey $ProjectKey
        Invoke-SonarScanner -Arguments $beginArguments -Description "Starting SonarScanner session"
        $scannerStarted = $true

        Sync-UnityCompiledAssemblies

        foreach ($project in $firstPartyProjects) {
            try {
                Invoke-DotNet -Arguments @(
                    "build",
                    $project.FullName,
                    "-nologo",
                    "-v:n",
                    "-p:BuildProjectReferences=false"
                ) -Description "Building project $($project.Name)"
            } catch {
                Write-Host $_.Exception.Message -ForegroundColor Yellow
                $failedProjects.Add($project.Name)
            }
        }

        foreach ($testProject in $testProjects) {
            try {
                Invoke-DotNet -Arguments @(
                    "build",
                    $testProject.FullName,
                    "-nologo",
                    "-v:n",
                    "-p:BuildProjectReferences=false"
                ) -Description "Building test project $($testProject.Name)"
            } catch {
                Write-Host $_.Exception.Message -ForegroundColor Yellow
                $failedProjects.Add($testProject.Name)
            }
        }

        if ($scannerStarted) {
            $endArguments = Get-EndArguments
            Invoke-SonarScanner -Arguments $endArguments -Description "Finalizing SonarScanner session"
        }
    } finally {
        Pop-Location
    }

    $effectiveProjectKey = if ([string]::IsNullOrWhiteSpace($ProjectKey)) {
        $properties["sonar.projectKey"]
    } else {
        $ProjectKey
    }

    if ($failedProjects.Count -gt 0) {
        Write-Host "Analysis uploaded with build failures in: $($failedProjects -join ', ')" -ForegroundColor Yellow
    }

    Write-Host "Analysis completed. Dashboard: $SONARQUBE_URL/dashboard?id=$effectiveProjectKey" -ForegroundColor Green
}

if ($StartSonarQube) {
    Start-SonarQube
} elseif ($StopSonarQube) {
    Stop-SonarQube
} elseif ($SetupProject) {
    Setup-Project
} elseif ($RunAnalysis) {
    Run-Analysis
} else {
    Write-Host "SonarQube Setup Script for Unity Code Analysis" -ForegroundColor Cyan
    Write-Host "==============================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Usage:"
    Write-Host "  .\setup-sonarqube.ps1 -StartSonarQube"
    Write-Host "  .\setup-sonarqube.ps1 -SetupProject"
    Write-Host "  .\setup-sonarqube.ps1 -RunAnalysis"
    Write-Host "  .\setup-sonarqube.ps1 -StopSonarQube"
    Write-Host ""
    Write-Host "Optional:"
    Write-Host "  -ProjectKey <key>                 Override sonar.projectKey"
    Write-Host "  -MainBuildTarget <path>           Optional: build only one first-party csproj"
    Write-Host "  -CoverageReportPath <path>        Default: CodeCoverage/Report/SonarQube.xml"
    Write-Host "  -SonarConfigPath <path>           Default: Tools/CodeAnalysis/sonar-project.properties"
    Write-Host ""
    Write-Host "Notes:"
    Write-Host "  * This script follows the official SonarScanner for .NET begin -> build -> end workflow."
    Write-Host "  * Unity coverage is imported from the existing generic SonarQube XML report."
    Write-Host "  * During Sonar runs, Directory.Build.props excludes non-Network_Game projects from analysis."
}
