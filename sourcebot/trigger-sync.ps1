$apiKey = "sbk_229f313179e67cdc1cb7af550ed248986b7c9595fbf5dca7380d8c2b42dad493"
$headers = @{
    Authorization = "Bearer $apiKey"
    Accept = "application/json, text/event-stream"
}

Write-Host "=== Checking raw repository data from config ===" -ForegroundColor Cyan

# Check what the backend sees as connections
try {
    $response = Invoke-WebRequest -Uri "http://127.0.0.1:8090/api/connections" -Headers $headers -UseBasicParsing
    Write-Host "Connections response status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "Raw response length: $($response.RawContentLength)" -ForegroundColor Yellow
} catch {
    Write-Host "Connections API error: $($_.Exception.Message)" -ForegroundColor Red
}

# Try to access the internal API for repository management
Write-Host "`n=== Trying internal repository sync endpoints ===" -ForegroundColor Cyan

# Check if there are any sync/reindex endpoints
$endpoints = @(
    "http://127.0.0.1:8090/api/sync",
    "http://127.0.0.1:8090/api/index",
    "http://127.0.0.1:8090/api/repos/sync",
    "http://127.0.0.1:8090/api/repos/resync",
    "http://127.0.0.1:8090/api/connection/sync"
)

foreach ($endpoint in $endpoints) {
    try {
        $response = Invoke-WebRequest -Uri $endpoint -Headers $headers -Method Get -UseBasicParsing
        Write-Host "  $endpoint - Status: $($response.StatusCode)" -ForegroundColor Green
    } catch {
        # Common to get 404 or 405, just informational
        Write-Host "  $endpoint - $($_.Exception.Response.StatusCode): $($_.Exception.Message)" -ForegroundColor Gray
    }
}

# Check if we can access the web UI API endpoints
Write-Host "`n=== Checking web UI API endpoints ===" -ForegroundColor Cyan

try {
    $response = Invoke-WebRequest -Uri "http://127.0.0.1:8090/~/api/repos" -Headers $headers -UseBasicParsing
    Write-Host "Web API /~/api/repos - Status: $($response.StatusCode)" -ForegroundColor Green
    $repos = $response.Content | ConvertFrom-Json
    Write-Host "Found $($repos.repositories.Count) repos via web API" -ForegroundColor Yellow
    $repos.repositories | ForEach-Object { Write-Host "  - $($_.name) ($($_.id))" }
} catch {
    Write-Host "Web API error: $($_.Exception.Message)" -ForegroundColor Red
}

# Check if we can trigger a search with debug info
Write-Host "`n=== Testing search with a recent commit pattern ===" -ForegroundColor Cyan

# Search for the most recent commit message
$searchBody = @{ query = "bb7e806"; matches = 10 } | ConvertTo-Json  # From your git log: "bb7e806 diagnosis system"
try {
    $results = Invoke-RestMethod -Uri "http://127.0.0.1:8090/api/search" -Method Post -ContentType "application/json" -Body $searchBody -Headers $headers
    Write-Host "Search for 'bb7e806' (recent commit):" -ForegroundColor Green
    Write-Host "  Files: $($results.stats.fileCount)"
    Write-Host "  Matches: $($results.stats.totalMatchCount)"
    $results.files | ForEach-Object { Write-Host "    - $($_.fileName.text)" }
} catch {
    Write-Host "Search error: $($_.Exception.Message)" -ForegroundColor Red
}

# Also try searching for another recent commit
$searchBody2 = @{ query = "dialogue optimize"; matches = 10 } | ConvertTo-Json  # From your git log: "254babb dialogue optimize"
try {
    $results2 = Invoke-RestMethod -Uri "http://127.0.0.1:8090/api/search" -Method Post -ContentType "application/json" -Body $searchBody2 -Headers $headers
    Write-Host "`nSearch for 'dialogue optimize' (recent commit):" -ForegroundColor Green
    Write-Host "  Files: $($results2.stats.fileCount)"
    Write-Host "  Matches: $($results2.stats.totalMatchCount)"
    $results2.files | ForEach-Object { Write-Host "    - $($_.fileName.text)" }
} catch {
    Write-Host "Search error: $($_.Exception.Message)" -ForegroundColor Red
}
