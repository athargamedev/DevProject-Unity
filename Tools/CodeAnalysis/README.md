# Unity Code Analysis Tools

This directory contains development tools for code quality analysis in the Unity project. These tools are placed outside the `Assets/` folder to maintain proper Unity project structure.

## 📁 Unity Project Structure Best Practices

### ✅ What Belongs in Assets/
- Game assets (models, textures, audio, etc.)
- C# scripts and components
- Unity scenes and prefabs
- Editor scripts (in `Assets/Editor/`)
- Third-party assets and plugins

### ❌ What Should NOT Be in Assets/
- Development tools and utilities
- Build scripts and automation
- Code analysis tools
- Documentation (except in-game docs)
- CI/CD configuration
- Package management files

### 🛠️ Development Tools Location
Development tools should be placed in:
- `Tools/` - General development utilities
- `Scripts/` - Build and automation scripts
- Project root - Configuration files

## 🔧 Available Tools

### 1. Custom Code Analyzer (`analyze-code.ps1`)
Performs basic static analysis on C# files:
- TODO/FIXME comments detection
- Empty catch blocks
- Long lines (>120 characters)
- Missing using statements

**Usage:**
```powershell
# From project root
.\Tools\CodeAnalysis\analyze-code.ps1 -Analyze -Report

# Or from Tools/CodeAnalysis directory
.\analyze-code.ps1 -Analyze -Report
```

### 2. SonarQube Setup (`setup-sonarqube.ps1`)
Runs SonarScanner for .NET in a Unity-aware workflow.

What it does:
- Uses the supported `begin -> build -> end` SonarScanner for .NET flow
- Limits C# analysis to first-party `Network_Game*` assemblies during Sonar runs
- Imports Unity Code Coverage from `CodeCoverage/Report/SonarQube.xml`
- Keeps the `supabase` TypeScript LLM proxy in the same Sonar project via multi-language scan

**Usage:**
```powershell
# Start SonarQube server
.\Tools\CodeAnalysis\setup-sonarqube.ps1 -StartSonarQube

# Verify local Sonar tooling + config
.\Tools\CodeAnalysis\setup-sonarqube.ps1 -SetupProject

# Run analysis (requires server running)
.\Tools\CodeAnalysis\setup-sonarqube.ps1 -RunAnalysis

# Stop server
.\Tools\CodeAnalysis\setup-sonarqube.ps1 -StopSonarQube
```

### 3. SonarQube Batch File (`setup-sonarqube.bat`)
Thin Windows wrapper around `setup-sonarqube.ps1`.

## 📊 Analysis Results

- **Files Analyzed**: 130 C# files
- **Total Lines**: 47,460
- **Issues Found**: 303
  - Long lines: 272
  - Missing using statements: 31

Results are saved to `code-analysis-report.txt` in the project root.

## 🔍 SonarQube Web Interface

When SonarQube is running:
- URL: http://localhost:9000
- Credentials: admin / admin
- Project Key: unity-multiplayer-game

## 📝 Notes

- These tools are designed for development-time analysis
- They do not affect the Unity build process
- Analysis results help maintain code quality standards
- SonarQube provides more comprehensive analysis than the custom analyzer
- Unity coverage settings currently live in `ProjectSettings/Packages/com.unity.testtools.codecoverage/Settings.json`
- Sonar configuration properties live in `Tools/CodeAnalysis/sonar-project.properties`
- The Unity + dialogue tuning guide lives in `Tools/CodeAnalysis/sonarqube-unity-llm-playbook.md`
