# Unity C# Code Analysis Script
# Alternative to SonarQube - Free and Simple

param(
    [string]$SourcePath = "$PSScriptRoot\..\..\Assets\Network_Game",
    [switch]$Analyze,
    [switch]$Report
)

function Write-Header {
    param([string]$Message)
    Write-Host "`n=== $Message ===" -ForegroundColor Cyan
}

function Get-CSharpFiles {
    param([string]$Path)
    Get-ChildItem -Path $Path -Recurse -Include "*.cs" -File | Where-Object {
        $_.FullName -notmatch "\\bin\\" -and
        $_.FullName -notmatch "\\obj\\" -and
        $_.FullName -notmatch "\\.git\\" -and
        $_.FullName -notmatch "\\Library\\" -and
        $_.FullName -notmatch "\\Temp\\"
    }
}

function Analyze-CodeQuality {
    Write-Header "Analyzing C# Code Quality"

    $files = Get-CSharpFiles -Path $SourcePath
    Write-Host "Found $($files.Count) C# files to analyze"

    $totalLines = 0
    $totalFiles = 0
    $issues = @()

    foreach ($file in $files) {
        $totalFiles++
        $content = Get-Content $file.FullName -Raw
        $lines = $content -split "`n"
        $totalLines += $lines.Count

        # Basic analysis
        $lineNumber = 0
        foreach ($line in $lines) {
            $lineNumber++

            # Check for TODO comments
            if ($line -match "TODO|FIXME|HACK") {
                $issues += @{
                    File = $file.FullName
                    Line = $lineNumber
                    Type = "TODO"
                    Message = $line.Trim()
                }
            }

            # Check for empty catch blocks
            if ($line -match "catch\s*\(" -and $lines[$lineNumber] -match "\s*{\s*}\s*") {
                $issues += @{
                    File = $file.FullName
                    Line = $lineNumber
                    Type = "EmptyCatch"
                    Message = "Empty catch block"
                }
            }

            # Check for long lines (>120 chars)
            if ($line.Length -gt 120) {
                $issues += @{
                    File = $file.FullName
                    Line = $lineNumber
                    Type = "LongLine"
                    Message = "Line too long ($($line.Length) chars)"
                }
            }
        }

        # Check for missing using statements (basic)
        if ($content -notmatch "using System") {
            $issues += @{
                File = $file.FullName
                Line = 1
                Type = "MissingUsing"
                Message = "File may be missing System using statements"
            }
        }
    }

    Write-Host "Analysis complete:"
    Write-Host "- Total files: $totalFiles"
    Write-Host "- Total lines: $totalLines"
    Write-Host "- Average lines per file: $([math]::Round($totalLines / $totalFiles, 1))"
    Write-Host "- Issues found: $($issues.Count)"

    return $issues
}

function Generate-Report {
    param([array]$Issues)

    Write-Header "Generating Analysis Report"

    $projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $reportPath = Join-Path $projectRoot "code-analysis-report.txt"
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

    $report = @"
Unity C# Code Analysis Report
Generated: $timestamp

SUMMARY
=======
Total Issues Found: $($Issues.Count)

ISSUES BY TYPE
==============
"@

    $issuesByType = $Issues | Group-Object -Property Type
    foreach ($group in $issuesByType) {
        $report += "$($group.Name): $($group.Count)`n"
    }

    $report += @"

DETAILED ISSUES
===============
"@

    foreach ($issue in $Issues | Sort-Object File, Line) {
        $report += "$($issue.File):$($issue.Line) - $($issue.Type) - $($issue.Message)`n"
    }

    $report | Out-File -FilePath $reportPath -Encoding UTF8
    Write-Host "Report saved to: $reportPath"
}

# Main execution
if ($Analyze) {
    $issues = Analyze-CodeQuality
    if ($Report) {
        Generate-Report -Issues $issues
    }
} else {
    Write-Host "Unity C# Code Analysis Script" -ForegroundColor Cyan
    Write-Host "============================"
    Write-Host ""
    Write-Host "Usage:"
    Write-Host "  .\analyze-code.ps1 -Analyze              # Run basic code analysis"
    Write-Host "  .\analyze-code.ps1 -Analyze -Report      # Run analysis and generate report"
    Write-Host ""
    Write-Host "Parameters:"
    Write-Host "  -SourcePath <path>    # Path to analyze (default: Assets/Network_Game)"
    Write-Host ""
    Write-Host "This script performs basic static analysis on C# files including:"
    Write-Host "  - TODO/FIXME comments"
    Write-Host "  - Empty catch blocks"
    Write-Host "  - Long lines (>120 chars)"
    Write-Host "  - Missing using statements"
}