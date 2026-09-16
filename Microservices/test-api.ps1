param(
    [string]$BaseUrl = "https://c02zy1d2ch.execute-api.us-east-1.amazonaws.com"
)

# Smoke test for the DDAC Admin microservices behind API Gateway.
# Usage: .\test-api.ps1 -BaseUrl "https://abc123xyz.execute-api.us-east-1.amazonaws.com"

$BaseUrl = $BaseUrl.TrimEnd('/')
$ErrorActionPreference = 'Continue'

function Invoke-Route {
    param([string]$Method, [string]$Path, $Body = $null)

    $url = "$BaseUrl$Path"
    try {
        $args = @{ Method = $Method; Uri = $url; TimeoutSec = 30 }
        if ($null -ne $Body) {
            $args.Body = ($Body | ConvertTo-Json -Compress)
            $args.ContentType = 'application/json'
        }
        $resp = Invoke-WebRequest @args
        Write-Host ("{0,-6} {1,-32} {2}" -f $Method, $Path, $resp.StatusCode) -ForegroundColor Green
        return ($resp.Content | ConvertFrom-Json)
    }
    catch {
        $code = $_.Exception.Response.StatusCode.value__
        Write-Host ("{0,-6} {1,-32} {2}" -f $Method, $Path, $(if ($code) { $code } else { 'TIMEOUT/ERR' })) -ForegroundColor Red
        if ($_.ErrorDetails.Message) { Write-Host "       $($_.ErrorDetails.Message)" -ForegroundColor DarkGray }
        return $null
    }
}

Write-Host "`nTesting $BaseUrl`n"

# --- report-list (S3 only, no DB) - simplest, test first
$reports = Invoke-Route GET '/reports'
if ($reports) { Write-Host "       $($reports.Count) report(s) in S3 archive" -ForegroundColor DarkGray }

# --- report-request (SQS only)
$queued = Invoke-Route POST '/reports' @{ ReportType = 'Employment'; RequestedBy = 'smoke-test' }
if ($queued) { Write-Host "       queued request $($queued.RequestID)" -ForegroundColor DarkGray }

# --- employer-verification (RDS)
$pending = Invoke-Route GET '/employer-verification'
if ($null -ne $pending) { Write-Host "       $($pending.Count) employer(s) pending" -ForegroundColor DarkGray }

# --- announcement-service (RDS + SNS) - full CRUD round trip
$list = Invoke-Route GET '/announcements'
if ($null -ne $list) { Write-Host "       $($list.Count) announcement(s)" -ForegroundColor DarkGray }

$created = Invoke-Route POST '/announcements' @{
    AdminID = 1; Title = 'Smoke test'; Content = 'Created by test-api.ps1'; Status = 'Draft'
}
if ($created) {
    $id = $created.AnnouncementID
    Write-Host "       created AnnouncementID $id" -ForegroundColor DarkGray
    Invoke-Route GET "/announcements/$id" | Out-Null
    Invoke-Route PUT "/announcements/$id" @{ Title = 'Smoke test (edited)'; Content = 'Still a draft'; Status = 'Draft' } | Out-Null
    Invoke-Route DELETE "/announcements/$id" | Out-Null
}

Write-Host "`nIf the POST /reports line was green, wait ~10 seconds then re-run GET /reports - a new file should appear.`n"
