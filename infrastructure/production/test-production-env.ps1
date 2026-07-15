[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$errors = New-Object System.Collections.Generic.List[string]

function Get-ConfigValue {
    param([string[]]$Names)

    foreach ($name in $Names) {
        $value = [Environment]::GetEnvironmentVariable($name)
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return $value.Trim()
        }
    }

    return $null
}

function Require-ConfigValue {
    param(
        [string]$Label,
        [string[]]$Names
    )

    $value = Get-ConfigValue -Names $Names
    if ([string]::IsNullOrWhiteSpace($value)) {
        $errors.Add("$Label is required. Checked: $($Names -join ', ')")
    }

    return $value
}

function Test-HttpsUri {
    param(
        [string]$Label,
        [string]$Value
    )

    $uri = $null
    if (-not [Uri]::TryCreate($Value, [UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -ne [Uri]::UriSchemeHttps) {
        $errors.Add("$Label must be an absolute HTTPS URL.")
    }
}

$environment = Require-ConfigValue "ASPNETCORE_ENVIRONMENT" @("ASPNETCORE_ENVIRONMENT")
if ($environment -and $environment -ne "Production") {
    $errors.Add("ASPNETCORE_ENVIRONMENT must be Production.")
}

$databaseProvider = Require-ConfigValue "Database provider" @("Database__Provider", "DATABASE_PROVIDER")
if ($databaseProvider -and $databaseProvider -ne "Postgres") {
    $errors.Add("Database provider must be Postgres in production.")
}

$postgres = Require-ConfigValue "Postgres connection string" @("ConnectionStrings__Postgres", "CONNECTIONSTRINGS__POSTGRES", "POSTGRES_CONNECTION_STRING")
$encryptionKey = Require-ConfigValue "Security encryption key" @("Security__EncryptionKey", "SECURITY_ENCRYPTION_KEY")
$lookupKey = Require-ConfigValue "Security lookup key" @("Security__LookupKey", "SECURITY_LOOKUP_KEY")
$jwtKey = Require-ConfigValue "JWT signing key" @("Jwt__SigningKey", "JWT_SIGNING_KEY")
$adminEmail = Require-ConfigValue "Admin bootstrap email" @("AdminBootstrap__Email", "ADMIN_BOOTSTRAP_EMAIL")
$adminPassword = Require-ConfigValue "Admin bootstrap password" @("AdminBootstrap__Password", "ADMIN_BOOTSTRAP_PASSWORD")
$adminMfa = Require-ConfigValue "Admin bootstrap MFA secret" @("AdminBootstrap__MfaSecret", "ADMIN_BOOTSTRAP_MFA_SECRET")
$turnstileSecret = Require-ConfigValue "Turnstile secret" @("Captcha__TurnstileSecret", "TURNSTILE_SECRET")
$smsProvider = Require-ConfigValue "SMS provider" @("Sms__Provider", "SMS_PROVIDER")
$smsApiKey = Require-ConfigValue "SMS API key" @("Sms__ApiKey", "SMS_API_KEY")
$smsEndpoint = Require-ConfigValue "SMS endpoint" @("Sms__Endpoint", "SMS_ENDPOINT")
$smsSender = Require-ConfigValue "SMS sender" @("Sms__Sender", "SMS_SENDER")
$smsTemplate = Require-ConfigValue "SMS message template" @("Sms__MessageTemplate", "SMS_MESSAGE_TEMPLATE")
$corsOrigin = Require-ConfigValue "CORS origin" @("Cors__AllowedOrigins__0", "CORS_ALLOWED_ORIGINS_0")
$captchaProvider = Require-ConfigValue "Web CAPTCHA provider" @("NEXT_PUBLIC_CAPTCHA_PROVIDER")
$turnstileSiteKey = Require-ConfigValue "Turnstile site key" @("NEXT_PUBLIC_TURNSTILE_SITE_KEY")
$apiUrl = Get-ConfigValue @("NEXT_PUBLIC_API_URL")
$apiHost = Get-ConfigValue @("NEXT_PUBLIC_API_HOST")

if ($encryptionKey -and $lookupKey -and $encryptionKey -eq $lookupKey) {
    $errors.Add("Security encryption and lookup keys must be different.")
}

foreach ($secret in @(
    @{ Label = "Security encryption key"; Value = $encryptionKey },
    @{ Label = "Security lookup key"; Value = $lookupKey },
    @{ Label = "JWT signing key"; Value = $jwtKey }
)) {
    if ($secret.Value -and $secret.Value.Length -lt 32) {
        $errors.Add("$($secret.Label) must be at least 32 characters.")
    }
}

if ($adminEmail -and (-not $adminEmail.Contains("@") -or $adminEmail.Length -gt 320)) {
    $errors.Add("Admin bootstrap email must be a valid email address.")
}

if ($adminPassword -and (
    $adminPassword.Length -lt 14 -or
    $adminPassword -notmatch "[A-Z]" -or
    $adminPassword -notmatch "[a-z]" -or
    $adminPassword -notmatch "[0-9]" -or
    $adminPassword -notmatch "[^A-Za-z0-9]"
)) {
    $errors.Add("Admin bootstrap password must be at least 14 characters and include uppercase, lowercase, digit and symbol characters.")
}

if ($adminMfa -and ($adminMfa -notmatch "^[A-Z2-7]{16,}$")) {
    $errors.Add("Admin bootstrap MFA secret must be Base32 with at least 16 characters.")
}

if ($smsProvider -and $smsProvider -ne "http-json") {
    $errors.Add("SMS provider must be http-json.")
}

if ($smsEndpoint) {
    Test-HttpsUri "SMS endpoint" $smsEndpoint
}

if ($smsTemplate -and $smsTemplate -notlike "*{code}*") {
    $errors.Add("SMS message template must include {code}.")
}

if ($corsOrigin) {
    Test-HttpsUri "CORS origin" $corsOrigin
}

if ($captchaProvider -and $captchaProvider -ne "turnstile") {
    $errors.Add("NEXT_PUBLIC_CAPTCHA_PROVIDER must be turnstile for production.")
}

if (-not $apiUrl -and -not $apiHost) {
    $errors.Add("Either NEXT_PUBLIC_API_URL or NEXT_PUBLIC_API_HOST is required for the web build.")
}

if ($apiUrl) {
    Test-HttpsUri "NEXT_PUBLIC_API_URL" $apiUrl
}

if ($errors.Count -gt 0) {
    throw "Production environment check failed:`n - $($errors -join "`n - ")"
}

Write-Output "Production environment configuration check passed."
