param(
    [string]$Container = "basvuruakis-postgres-1",
    [string]$Database = "basvuruakis",
    [string]$User = "basvuruakis",
    [string]$OutputDirectory = ".\backups"
)

$ErrorActionPreference = "Stop"

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$outputFile = Join-Path $OutputDirectory "$Database-$timestamp.dump"

docker exec $Container pg_dump -U $User -d $Database -Fc | Set-Content -Encoding Byte -Path $outputFile

Write-Output $outputFile
