param(
    [string]$Container = "basvuruakis-postgres-1",
    [string]$Database = "basvuruakis",
    [string]$User = "basvuruakis",
    [string]$OutputDirectory = ".\backups",
    [int]$RetentionDays = 7
)

$ErrorActionPreference = "Stop"

function Invoke-DockerDump {
    param(
        [string]$Container,
        [string]$Database,
        [string]$User,
        [string]$OutputFile
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new("docker")
    foreach ($argument in @("exec", $Container, "pg_dump", "-U", $User, "-d", $Database, "-Fc")) {
        $startInfo.ArgumentList.Add($argument)
    }
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.UseShellExecute = $false

    $process = [System.Diagnostics.Process]::Start($startInfo)
    try {
        $fileStream = [System.IO.File]::Open($OutputFile, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        try {
            $process.StandardOutput.BaseStream.CopyTo($fileStream)
        }
        finally {
            $fileStream.Dispose()
        }

        $stderr = $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) {
            throw "pg_dump failed with exit code $($process.ExitCode). $stderr"
        }
    }
    finally {
        $process.Dispose()
    }
}

if ($RetentionDays -lt 1) {
    throw "RetentionDays must be at least 1."
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$outputRoot = (Resolve-Path -LiteralPath $OutputDirectory).Path
$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$outputFile = Join-Path $outputRoot "$Database-$timestamp.dump"
$checksumFile = "$outputFile.sha256"
$metadataFile = "$outputFile.json"

Invoke-DockerDump -Container $Container -Database $Database -User $User -OutputFile $outputFile

$backup = Get-Item -LiteralPath $outputFile
if ($backup.Length -le 0) {
    throw "Backup file is empty: $outputFile"
}

$hash = Get-FileHash -LiteralPath $outputFile -Algorithm SHA256
"$($hash.Hash)  $($backup.Name)" | Set-Content -LiteralPath $checksumFile -Encoding UTF8

[ordered]@{
    database = $Database
    container = $Container
    user = $User
    createdAtUtc = (Get-Date).ToUniversalTime().ToString("O")
    file = $backup.Name
    bytes = $backup.Length
    sha256 = $hash.Hash
} | ConvertTo-Json | Set-Content -LiteralPath $metadataFile -Encoding UTF8

$cutoff = (Get-Date).AddDays(-$RetentionDays)
Get-ChildItem -Path (Join-Path $outputRoot "$Database-*.dump*") -File |
    Where-Object { $_.LastWriteTime -lt $cutoff } |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }

Write-Output $outputFile
