param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern("^https?://")]
    [string]$BaseUrl
)

$ErrorActionPreference = "Stop"
$base = $BaseUrl.TrimEnd("/")

function Assert-Response {
    param(
        [string]$Path,
        [int]$ExpectedStatus,
        [string]$ExpectedContent
    )

    try {
        $response = Invoke-WebRequest -Uri "$base$Path" -SkipHttpErrorCheck
    }
    catch {
        throw "Request to $Path failed: $($_.Exception.Message)"
    }

    if ($response.StatusCode -ne $ExpectedStatus) {
        throw "$Path returned $($response.StatusCode); expected $ExpectedStatus."
    }

    if ($ExpectedContent -and $response.Content -notmatch [regex]::Escape($ExpectedContent)) {
        throw "$Path did not contain expected content: $ExpectedContent"
    }

    Write-Host "PASS $Path ($ExpectedStatus)"
}

Assert-Response -Path "/" -ExpectedStatus 200 -ExpectedContent "Angular and ASP.NET Core full-stack app"
Assert-Response -Path "/products/widget-1" -ExpectedStatus 200 -ExpectedContent "Angular and ASP.NET Core full-stack app"
Assert-Response -Path "/unknown-client-route" -ExpectedStatus 200 -ExpectedContent "Angular and ASP.NET Core full-stack app"
Assert-Response -Path "/version.json" -ExpectedStatus 200 -ExpectedContent '"app": "fullstack-angular-dotnet-web"'
Assert-Response -Path "/api/health" -ExpectedStatus 200 -ExpectedContent '"status":"ok"'
Assert-Response -Path "/api/status" -ExpectedStatus 200 -ExpectedContent '"status":"available"'
Assert-Response -Path "/api/version" -ExpectedStatus 200 -ExpectedContent '"app":"fullstack-angular-dotnet-api"'
Assert-Response -Path "/api/missing" -ExpectedStatus 404 -ExpectedContent '"error":"not_found"'

Write-Host "All full-stack routing checks passed."

