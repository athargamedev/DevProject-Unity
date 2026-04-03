@echo off
REM Quick setup for SonarQube analysis as CAST Imaging alternative
REM This provides a free, open-source alternative for code quality analysis

echo SonarQube Setup for Unity Code Analysis
echo ======================================
echo.
echo This script provides a free alternative to CAST Imaging
echo for analyzing your Unity C# codebase.
echo.
echo Prerequisites:
echo - Docker Desktop installed and running
echo - .NET SDK 6.0 or later
echo.
echo Usage:
echo 1. Run: setup-sonarqube.bat start
echo 2. Open http://localhost:9000 in browser (admin/admin)
echo 3. Run: setup-sonarqube.bat setup
echo 4. Run: setup-sonarqube.bat analyze
echo.

if "%1"=="start" goto start_sonarqube
if "%1"=="stop" goto stop_sonarqube
if "%1"=="setup" goto setup_project
if "%1"=="analyze" goto run_analysis
goto show_help

:start_sonarqube
echo Starting SonarQube Community Edition...
docker run -d --name sonarqube -p 9000:9000 sonarqube:community
echo.
echo Waiting for SonarQube to start (this may take a few minutes)...
timeout /t 30 /nobreak > nul
echo.
echo SonarQube should be available at: http://localhost:9000
echo Default login: admin / admin
goto end

:stop_sonarqube
echo Stopping SonarQube...
docker stop sonarqube 2>nul
docker rm sonarqube 2>nul
echo SonarQube stopped.
goto end

:setup_project
echo Setting up project for SonarQube analysis...
echo Installing SonarScanner for .NET...
dotnet tool install --global dotnet-sonarscanner
echo.
echo Project configuration file (sonar-project.properties) should already exist.
echo If not, copy from sonar-project.properties.example
goto end

:run_analysis
echo Running SonarQube analysis...
echo.
echo This will analyze your Unity C# code for:
echo - Code quality issues
echo - Security vulnerabilities
echo - Technical debt
echo - Maintainability metrics
echo.
dotnet sonarscanner begin /k:"unity-multiplayer-game" /d:sonar.host.url="http://localhost:9000"
dotnet build
dotnet sonarscanner end
echo.
echo Analysis complete! View results at: http://localhost:9000/dashboard?id=unity-multiplayer-game
goto end

:show_help
echo Available commands:
echo   setup-sonarqube.bat start   - Start SonarQube server
echo   setup-sonarqube.bat stop    - Stop SonarQube server
echo   setup-sonarqube.bat setup   - Install tools and configure project
echo   setup-sonarqube.bat analyze - Run full code analysis
echo.
echo For detailed documentation, see: CAST_IMAGING_ALTERNATIVES.md
goto end

:end
echo.
pause