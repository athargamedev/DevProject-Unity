$apiKey = "sbk_229f313179e67cdc1cb7af550ed248986b7c9595fbf5dca7380d8c2b42dad493"
$headers = @{
    Authorization = "Bearer $apiKey"
    Accept = "application/json, text/event-stream"
}

Write-Host "=== Checking if repositories are indexed ===" -ForegroundColor Cyan

# Test 1: List repositories
try {
    $repos = Invoke-RestMethod -Uri "http://127.0.0.1:8090/api/repos" -Headers $headers
    Write-Host "Indexed repositories:" -ForegroundColor Green
    $repos.repositories | ForEach-Object { Write-Host "  - $($_.name) ($($_.id))" }
} catch {
    Write-Host "ERROR listing repos: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 2: Search for a recent commit message pattern
Write-Host "`n=== Searching for recent changes ===" -ForegroundColor Cyan
$searchBody = @{ query = "diagnosis system"; matches = 5 } | ConvertTo-Json
try {
    $results = Invoke-RestMethod -Uri "http://127.0.0.1:8090/api/search" -Method Post -ContentType "application/json" -Body $searchBody -Headers $headers
    Write-Host "Recent 'diagnosis system' hits:" -ForegroundColor Green
    Write-Host "  Files: $($results.stats.fileCount)"
    Write-Host "  Matches: $($results.stats.totalMatchCount)"
    $results.files | ForEach-Object { Write-Host "    - $($_.fileName.text)" }
} catch {
    Write-Host "ERROR searching: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3: Check if sym: search works (ctags)
Write-Host "`n=== Testing symbol search ===" -ForegroundColor Cyan
$symBody = @{ query = "sym:NetworkDialogueService"; matches = 5 } | ConvertTo-Json
try {
    $symResults = Invoke-RestMethod -Uri "http://127.0.0.1:8090/api/search" -Method Post -ContentType "application/json" -Body $symBody -Headers $headers
    Write-Host "Symbol search results:" -ForegroundColor Green
    Write-Host "  Files: $($symResults.stats.fileCount)"
    Write-Host "  Matches: $($symResults.stats.totalMatchCount)"
    $symResults.files | ForEach-Object { Write-Host "    - $($_.fileName.text)" }
} catch {
    Write-Host "ERROR symbol search: $($_.Exception.Message)" -ForegroundColor Red
}
