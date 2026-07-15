param(
    [Parameter(Mandatory = $true)]
    [string]$BackupFile,
    [string]$Container = "basvuruakis-postgres-1",
    [string]$Database = "basvuruakis",
    [string]$User = "basvuruakis",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

function Invoke-DockerRestore {
    param(
        [string]$Container,
        [string]$Database,
        [string]$User,
        [string]$BackupFile
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new("docker")
    foreach ($argument in @("exec", "-i", $Container, "pg_restore", "-U", $User, "-d", $Database, "--clean", "--if-exists", "--no-owner", "--no-privileges")) {
        $startInfo.ArgumentList.Add($argument)
    }
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.UseShellExecute = $false

    $process = [System.Diagnostics.Process]::Start($startInfo)
    try {
        $fileStream = [System.IO.File]::OpenRead($BackupFile)
        try {
            $fileStream.CopyTo($process.StandardInput.BaseStream)
        }
        finally {
            $fileStream.Dispose()
            $process.StandardInput.Close()
        }

        $stderr = $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) {
            throw "pg_restore failed with exit code $($process.ExitCode). $stderr"
        }
    }
    finally {
        $process.Dispose()
    }
}

if (-not $Force) {
    throw "Restore is destructive. Re-run with -Force after verifying the target database and backup file."
}

if (-not (Test-Path -LiteralPath $BackupFile)) {
    throw "Backup file not found: $BackupFile"
}

$backup = Get-Item -LiteralPath $BackupFile
if ($backup.Length -le 0) {
    throw "Backup file is empty: $BackupFile"
}

$checksumFile = "$BackupFile.sha256"
if (Test-Path -LiteralPath $checksumFile) {
    $expectedHash = ((Get-Content -LiteralPath $checksumFile -TotalCount 1) -split "\s+")[0]
    $actualHash = (Get-FileHash -LiteralPath $BackupFile -Algorithm SHA256).Hash
    if (-not $actualHash.Equals($expectedHash, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Backup checksum mismatch for $BackupFile"
    }
}
else {
    Write-Warning "Checksum file not found: $checksumFile"
}

Invoke-DockerRestore -Container $Container -Database $Database -User $User -BackupFile $BackupFile
