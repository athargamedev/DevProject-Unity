$apiKey = "sbk_229f313179e67cdc1cb7af550ed248986b7c9595fbf5dca7380d8c2b42dad493"
$headers = @{
    Authorization = "Bearer $apiKey"
    Accept = "application/json, text/event-stream"
}

Write-Host "=== Checking repository details ===" -ForegroundColor Cyan

# Get detailed repository information
try {
    $repos = Invoke-RestMethod -Uri "http://127.0.0.1:8090/api/repos" -Headers $headers
    Write-Host "Found $($repos.repositories.Count) repositories:" -ForegroundColor Green
    foreach ($repo in $repos.repositories) {
        Write-Host "  ID: $($repo.id), Name: '$($repo.name)', URL: '$($repo.url)'" -ForegroundColor Yellow
        Write-Host "  Default branch: $($repo.defaultBranch)" -ForegroundColor Gray
        Write-Host "  Last indexed: $($repo.lastIndexedAt)" -ForegroundColor Gray
        Write-Host ""
    }
} catch {
    Write-Host "ERROR getting repos: $($_.Exception.Message)" -ForegroundColor Red
}

# Check if we can trigger a manual sync
Write-Host "=== Attempting to trigger repository sync ===" -ForegroundColor Cyan

# Try to list repository revisions/branches to see if they're accessible
foreach ($repo in $repos.repositories) {
    if ($repo.id) {
        try {
            Write-Host "Checking repository $($repo.id)..." -ForegroundColor Yellow
            $tree = Invoke-RestMethod -Uri "http://127.0.0.1:8090/~/browse/$($repo.name)@refs/heads/$($repo.defaultBranch)/-/tree/" -Headers $headers
            Write-Host "  Tree access: OK" -ForegroundColor Green
        } catch {
            Write-Host "  Tree access failed: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

# Test searching for a known file
Write-Host "`n=== Testing search for known files ===" -ForegroundColor Cyan
$searchBody = @{ query = "NetworkDialogueService.cs"; matches = 5 } | ConvertTo-Json
try {
    $results = Invoke-RestMethod -Uri "http://127.0.0.1:8090/api/search" -Method Post -ContentType "application/json" -Body $searchBody -Headers $headers
    Write-Host "Files matching 'NetworkDialogueService.cs':" -ForegroundColor Green
    Write-Host "  Files: $($results.stats.fileCount)"
    Write-Host "  Matches: $($results.stats.totalMatchCount)"
    $results.files | ForEach-Object { Write-Host "    - $($_.fileName.text)" }
} catch {
    Write-Host "ERROR searching: $($_.Exception.Message)" -ForegroundColor Red
}
