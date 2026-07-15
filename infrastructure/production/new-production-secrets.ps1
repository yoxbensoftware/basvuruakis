[CmdletBinding()]
param(
    [string]$AdminEmail = "admin@example.com",
    [string]$SmsSender = "BasvuruAkis",
    [string]$SmsMessageTemplate = "Basvuru dogrulama kodunuz: {code}",
    [string]$OutputPath,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function New-Base64UrlSecret {
    param([int]$ByteCount = 48)

    $bytes = [byte[]]::new($ByteCount)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    return [Convert]::ToBase64String($bytes).TrimEnd("=").Replace("+", "-").Replace("/", "_")
}

function New-Base32Secret {
    param([int]$ByteCount = 20)

    $alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"
    $bytes = [byte[]]::new($ByteCount)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)

    $bits = 0
    $buffer = 0
    $output = New-Object System.Text.StringBuilder
    foreach ($byte in $bytes) {
        $buffer = ($buffer -shl 8) -bor $byte
        $bits += 8
        while ($bits -ge 5) {
            $index = ($buffer -shr ($bits - 5)) -band 31
            [void]$output.Append($alphabet[$index])
            $bits -= 5
        }
    }

    if ($bits -gt 0) {
        $index = ($buffer -shl (5 - $bits)) -band 31
        [void]$output.Append($alphabet[$index])
    }

    return $output.ToString()
}

function New-StrongPassword {
    $upper = "ABCDEFGHJKLMNPQRSTUVWXYZ"
    $lower = "abcdefghijkmnopqrstuvwxyz"
    $digits = "23456789"
    $symbols = "!@#$%^&*()-_=+"
    $all = ($upper + $lower + $digits + $symbols).ToCharArray()

    $chars = @(
        $upper[[System.Security.Cryptography.RandomNumberGenerator]::GetInt32($upper.Length)]
        $lower[[System.Security.Cryptography.RandomNumberGenerator]::GetInt32($lower.Length)]
        $digits[[System.Security.Cryptography.RandomNumberGenerator]::GetInt32($digits.Length)]
        $symbols[[System.Security.Cryptography.RandomNumberGenerator]::GetInt32($symbols.Length)]
    )

    while ($chars.Count -lt 24) {
        $chars += $all[[System.Security.Cryptography.RandomNumberGenerator]::GetInt32($all.Length)]
    }

    return -join ($chars | Sort-Object { [System.Security.Cryptography.RandomNumberGenerator]::GetInt32(0, 1000000) })
}

if ($SmsMessageTemplate -notlike "*{code}*") {
    throw "SmsMessageTemplate must include {code}."
}

$values = [ordered]@{
    "ASPNETCORE_ENVIRONMENT" = "Production"
    "Database__Provider" = "Postgres"
    "Security__EncryptionKey" = New-Base64UrlSecret
    "Security__LookupKey" = New-Base64UrlSecret
    "Jwt__SigningKey" = New-Base64UrlSecret
    "AdminBootstrap__Email" = $AdminEmail
    "AdminBootstrap__Password" = New-StrongPassword
    "AdminBootstrap__MfaSecret" = New-Base32Secret
    "Sms__Provider" = "http-json"
    "Sms__Sender" = $SmsSender
    "Sms__MessageTemplate" = $SmsMessageTemplate
    "NEXT_PUBLIC_CAPTCHA_PROVIDER" = "turnstile"
}

$lines = @(
    "# Generated production-owned values. Store them in the hosting secret manager."
    "# Fill provider-specific values separately: database connection string, Turnstile secret/site key, SMS API key/endpoint, CORS/web/API URLs."
) + ($values.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" })

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $lines
    return
}

$resolvedOutput = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
if ((Test-Path -LiteralPath $resolvedOutput) -and -not $Force) {
    throw "OutputPath already exists. Pass -Force to overwrite: $resolvedOutput"
}

$directory = Split-Path -Parent $resolvedOutput
if (-not [string]::IsNullOrWhiteSpace($directory)) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}

Set-Content -LiteralPath $resolvedOutput -Value $lines -Encoding utf8NoBOM
Write-Output "Generated production secret skeleton: $resolvedOutput"
Write-Output "Do not commit this file. Move values to the secret manager and delete the local copy."
