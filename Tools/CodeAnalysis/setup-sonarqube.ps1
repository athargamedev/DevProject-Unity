# SonarQube Setup Script for Unity C# Code Analysis
# Alternative to CAST Imaging - Free and Open Source

param(
    [switch]$StartSonarQube,
    [switch]$StopSonarQube,
    [switch]$RunAnalysis,
    [switch]$SetupProject,
    [string]$ProjectKey = "unity-multiplayer-game"
)

$SONARQUBE_PORT = 9000
$SONARQUBE_CONTAINER = "sonarqube"
$SONARQUBE_URL = "http://localhost:$SONARQUBE_PORT"

function Write-Header {
    param([string]$Message)
    Write-Host "`n=== $Message ===" -ForegroundColor Cyan
}

function Test-Docker {
    try {
        $null = docker --version
        return $true
    } catch {
        Write-Error "Docker is not installed or not running. Please install Docker Desktop."
        return $false
    }
}

function Test-DotNet {
    try {
        $null = dotnet --version
        return $true
    } catch {
        Write-Error ".NET SDK is not installed. Please install .NET 6.0 or later."
        return $false
    }
}

function Start-SonarQube {
    Write-Header "Starting SonarQube Community Edition"

    if (-not (Test-Docker)) { return }

    # Check if container already exists
    $existing = docker ps -a --filter "name=$SONARQUBE_CONTAINER" --format "{{.Names}}"
    if ($existing -eq $SONARQUBE_CONTAINER) {
        Write-Host "SonarQube container already exists. Starting it..."
        docker start $SONARQUBE_CONTAINER
    } else {
        Write-Host "Creating and starting SonarQube container..."
        docker run -d --name $SONARQUBE_CONTAINER -p ${SONARQUBE_PORT}:9000 sonarqube:community
    }

    Write-Host "Waiting for SonarQube to start up..."
    Start-Sleep -Seconds 30

    # Wait for SonarQube to be ready
    $maxRetries = 60
    $retryCount = 0
    do {
        try {
            $response = Invoke-WebRequest -Uri $SONARQUBE_URL -TimeoutSec 10 -ErrorAction Stop
            if ($response.StatusCode -eq 200) {
                Write-Host "SonarQube is ready at $SONARQUBE_URL" -ForegroundColor Green
                Write-Host "Default credentials: admin/admin" -ForegroundColor Yellow
                return $true
            }
        } catch {
            Write-Host "Waiting for SonarQube... ($retryCount/$maxRetries)"
            Start-Sleep -Seconds 10
            $retryCount++
        }
    } while ($retryCount -lt $maxRetries)

    Write-Error "SonarQube failed to start properly"
    return $false
}

function Stop-SonarQube {
    Write-Header "Stopping SonarQube"

    if (-not (Test-Docker)) { return }

    docker stop $SONARQUBE_CONTAINER 2>$null
    docker rm $SONARQUBE_CONTAINER 2>$null
    Write-Host "SonarQube stopped and container removed"
}

function Setup-Project {
    Write-Header "Setting up SonarQube project configuration"

    if (-not (Test-DotNet)) { return }

    # Install SonarScanner if not present
    try {
        dotnet tool list --global | Out-Null
    } catch {
        Write-Host "Installing .NET tools..."
    }

    $scannerInstalled = dotnet tool list --global | Select-String "dotnet-sonarscanner"
    if (-not $scannerInstalled) {
        Write-Host "Installing SonarScanner for .NET..."
        dotnet tool install --global dotnet-sonarscanner
    }

    # Create sonar-project.properties
    $sonarConfig = @"
sonar.projectKey=$ProjectKey
sonar.projectName=Unity Multiplayer Game
sonar.projectVersion=1.0.0
sonar.sources=Assets/Network_Game/
sonar.exclusions=**/*.meta,**/*.asset,**/*.prefab,**/*.unity,**/*.dll,**/*.exe
sonar.language=cs
sonar.sourceEncoding=UTF-8
sonar.cs.roslyn.ignoreIssues=false
sonar.cs.analyzeGeneratedCode=false
"@

    # Create sonar-project.properties in project root
    $projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $configPath = Join-Path $projectRoot "sonar-project.properties"
    $sonarConfig | Out-File -FilePath $configPath -Encoding UTF8
    Write-Host "Created sonar-project.properties at $configPath"
}

function Run-Analysis {
    Write-Header "Running SonarQube Analysis"

    if (-not (Test-DotNet)) { return }

    # Check if SonarQube is running
    try {
        $response = Invoke-WebRequest -Uri $SONARQUBE_URL -TimeoutSec 5 -ErrorAction Stop
    } catch {
        Write-Error "SonarQube is not running at $SONARQUBE_URL. Use -StartSonarQube first."
        return
    }

    # Check if project config exists (in project root)
    $projectRoot = Split-Path $PSScriptRoot -Parent
    $configPath = Join-Path $projectRoot "sonar-project.properties"
    if (-not (Test-Path $configPath)) {
        Write-Host "Project configuration not found. Running setup..."
        Setup-Project
    }

    Write-Host "Starting SonarQube analysis..."
    Write-Host "Note: Skipping build step for Unity project analysis"

    # Begin analysis
    Write-Host "Beginning analysis session..."
    dotnet sonarscanner begin /k:"$ProjectKey" /d:sonar.host.url="$SONARQUBE_URL" /d:sonar.scm.disabled="true" /d:sonar.skipPackageDesign="true"

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to begin SonarQube analysis"
        return
    }

    # Skip build for Unity projects - just analyze source files
    Write-Host "Analyzing source files directly..."

    # End analysis
    Write-Host "Ending analysis session..."
    dotnet sonarscanner end

    if ($LASTEXITCODE -eq 0) {
        Write-Host "Analysis completed successfully!" -ForegroundColor Green
        Write-Host "View results at: $SONARQUBE_URL/dashboard?id=$ProjectKey"
    } else {
        Write-Error "Analysis failed"
    }
}

# Main execution logic
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
    Write-Host "=============================================="
    Write-Host ""
    Write-Host "Usage:"
    Write-Host "  .\setup-sonarqube.ps1 -StartSonarQube    # Start SonarQube server"
    Write-Host "  .\setup-sonarqube.ps1 -StopSonarQube     # Stop SonarQube server"
    Write-Host "  .\setup-sonarqube.ps1 -SetupProject       # Configure project for analysis"
    Write-Host "  .\setup-sonarqube.ps1 -RunAnalysis        # Run full analysis"
    Write-Host ""
    Write-Host "Example workflow:"
    Write-Host "  1. .\setup-sonarqube.ps1 -StartSonarQube"
    Write-Host "  2. Open http://localhost:9000 (admin/admin)"
    Write-Host "  3. .\setup-sonarqube.ps1 -SetupProject"
    Write-Host "  4. .\setup-sonarqube.ps1 -RunAnalysis"
    Write-Host ""
    Write-Host "Requirements: Docker, .NET SDK 6.0+"
}