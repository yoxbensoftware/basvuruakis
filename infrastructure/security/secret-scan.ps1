[CmdletBinding()]
param(
    [string]$Root = (Resolve-Path (Join-Path (Join-Path $PSScriptRoot "..") "..")).Path
)

$ErrorActionPreference = "Stop"

Push-Location -LiteralPath $Root
try {
    $trackedFiles = (& git ls-files -z) -split "`0" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $findings = [System.Collections.Generic.List[string]]::new()

    $blockedFileNamePatterns = @(
        '(^|/)\.env(\..+)?$',
        '\.(pem|pfx|p12|key)$'
    )
    $allowedFileNamePatterns = @(
        '(^|/)infrastructure/\.env\.example$'
    )

    foreach ($file in $trackedFiles) {
        $normalized = $file -replace '\\', '/'
        $isAllowed = $allowedFileNamePatterns | Where-Object { $normalized -match $_ } | Select-Object -First 1
        $isBlocked = $blockedFileNamePatterns | Where-Object { $normalized -match $_ } | Select-Object -First 1
        if ($isBlocked -and -not $isAllowed) {
            $findings.Add("Blocked secret-like file tracked by git: $file")
        }
    }

    $contentPatterns = @(
        @{ Name = "private-key"; Pattern = '-----BEGIN (?:RSA |OPENSSH |EC |DSA )?PRIVATE KEY-----' },
        @{ Name = "aws-access-key"; Pattern = 'AKIA[0-9A-Z]{16}' },
        @{ Name = "github-token"; Pattern = '(ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9_]{20,}' },
        @{ Name = "github-pat"; Pattern = 'github_pat_[A-Za-z0-9_]{20,}' },
        @{ Name = "openai-key"; Pattern = 'sk-(?:proj-)?[A-Za-z0-9_-]{20,}' },
        @{ Name = "stripe-live-key"; Pattern = 'sk_live_[A-Za-z0-9]{20,}' },
        @{ Name = "slack-token"; Pattern = 'xox[baprs]-[A-Za-z0-9-]{10,}' },
        @{ Name = "google-api-key"; Pattern = 'AIza[0-9A-Za-z_-]{35}' }
    )

    foreach ($file in $trackedFiles) {
        $item = Get-Item -LiteralPath $file -ErrorAction SilentlyContinue
        if ($null -eq $item -or $item.Length -gt 1MB) {
            continue
        }

        $content = Get-Content -LiteralPath $file -Raw -ErrorAction SilentlyContinue
        if ($null -eq $content) {
            continue
        }

        foreach ($pattern in $contentPatterns) {
            if ($content -match $pattern.Pattern) {
                $findings.Add("Potential $($pattern.Name) in $file")
            }
        }
    }

    if ($findings.Count -gt 0) {
        Write-Error ("Secret scan failed:`n" + ($findings -join "`n"))
        exit 1
    }

    Write-Host "Secret scan passed for $($trackedFiles.Count) tracked files."
}
finally {
    Pop-Location
}
