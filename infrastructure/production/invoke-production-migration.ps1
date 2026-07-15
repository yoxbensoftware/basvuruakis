[CmdletBinding()]
param(
    [string]$ConnectionString,
    [string]$OutputPath = "artifacts/migrations/basvuruakis.sql",
    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$resolvedOutput = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath((Join-Path $repoRoot $OutputPath))
$outputDirectory = Split-Path -Parent $resolvedOutput
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

Push-Location $repoRoot
try {
    dotnet tool restore
    dotnet tool run dotnet-ef migrations script --idempotent `
        --project .\apps\api\BasvuruAkis.Api.csproj `
        --startup-project .\apps\api\BasvuruAkis.Api.csproj `
        --context AppDbContext `
        --output $resolvedOutput

    Write-Output "Generated idempotent migration SQL: $resolvedOutput"

    if ($Apply) {
        if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
            throw "ConnectionString is required when -Apply is used."
        }

        dotnet tool run dotnet-ef database update `
            --project .\apps\api\BasvuruAkis.Api.csproj `
            --startup-project .\apps\api\BasvuruAkis.Api.csproj `
            --context AppDbContext `
            --connection $ConnectionString
        Write-Output "Applied EF migrations to the target database."
    } else {
        Write-Output "Review the SQL and run again with -Apply only after backup and approval."
    }
}
finally {
    Pop-Location
}
