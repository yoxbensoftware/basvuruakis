param(
    [Parameter(Mandatory = $true)]
    [string]$BackupFile,
    [string]$Container = "basvuruakis-postgres-1",
    [string]$Database = "basvuruakis",
    [string]$User = "basvuruakis"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $BackupFile)) {
    throw "Backup file not found: $BackupFile"
}

Get-Content -Encoding Byte -Path $BackupFile | docker exec -i $Container pg_restore -U $User -d $Database --clean --if-exists
