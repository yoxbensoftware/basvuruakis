[CmdletBinding()]
param(
    [string]$WebBaseUrl = "https://basvuruakis-web.onrender.com",
    [string]$ApiBaseUrl = "https://basvuruakis-api.onrender.com",
    [string]$AdminEmail = "admin@basvuruakis.local",
    [string]$AdminPassword = "ChangeMe!12345",
    [string]$CaptchaToken = "presentation-ok",
    [int]$TimeoutSec = 30,
    [switch]$SkipFullSmoke
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Invoke-Json {
    param(
        [string]$Method,
        [string]$Uri,
        [object]$Body,
        [hashtable]$Headers
    )

    $parameters = @{
        Method = $Method
        Uri = $Uri
        TimeoutSec = $TimeoutSec
        ContentType = "application/json"
    }
    if ($null -ne $Body) {
        $parameters.Body = ($Body | ConvertTo-Json -Depth 6)
    }
    if ($Headers) {
        $parameters.Headers = $Headers
    }

    return Invoke-RestMethod @parameters
}

function New-Tckn {
    param([int]$Seed)

    $digits = $Seed.ToString().PadLeft(9, "0").Substring($Seed.ToString().PadLeft(9, "0").Length - 9).ToCharArray() | ForEach-Object { [int]::Parse($_) }
    if ($digits[0] -eq 0) {
        $digits[0] = 1
    }

    $odd = $digits[0] + $digits[2] + $digits[4] + $digits[6] + $digits[8]
    $even = $digits[1] + $digits[3] + $digits[5] + $digits[7]
    $tenth = (($odd * 7 - $even) % 10 + 10) % 10
    $eleventh = (($digits | Measure-Object -Sum).Sum + $tenth) % 10
    return (($digits -join "") + $tenth + $eleventh)
}

$webBase = $WebBaseUrl.TrimEnd("/")
$apiBase = $ApiBaseUrl.TrimEnd("/")

$homeResp = Invoke-WebRequest -UseBasicParsing "$webBase/" -TimeoutSec $TimeoutSec
$applicationPage = Invoke-WebRequest -UseBasicParsing "$webBase/basvuru" -TimeoutSec $TimeoutSec
$adminPage = Invoke-WebRequest -UseBasicParsing "$webBase/admin" -TimeoutSec $TimeoutSec
$live = Invoke-WebRequest -UseBasicParsing "$apiBase/health/live" -TimeoutSec $TimeoutSec
$ready = Invoke-WebRequest -UseBasicParsing "$apiBase/health/ready" -TimeoutSec $TimeoutSec
$legalTexts = Invoke-WebRequest -UseBasicParsing "$apiBase/api/legal-texts/active" -TimeoutSec $TimeoutSec

Assert-True ($homeResp.StatusCode -eq 200) "Home page did not return 200."
Assert-True ($applicationPage.StatusCode -eq 200) "Application page did not return 200."
Assert-True ($adminPage.StatusCode -eq 200) "Admin page did not return 200."
Assert-True ($live.StatusCode -eq 200) "API live health did not return 200."
Assert-True ($ready.StatusCode -eq 200) "API ready health did not return 200."
Assert-True ($legalTexts.StatusCode -eq 200) "Legal texts endpoint did not return 200."
Assert-True ($homeResp.Headers["X-Frame-Options"] -contains "DENY") "X-Frame-Options header is missing or unsafe."
Assert-True ($homeResp.Headers["X-Content-Type-Options"] -contains "nosniff") "X-Content-Type-Options header is missing or unsafe."
Assert-True ($homeResp.Content.Contains("Sunum ortamı canlı")) "Demo release copy is not visible on home page."
Assert-True (-not $homeResp.Content.Contains("Production geçişi")) "Old production transition copy is still visible."
Assert-True (-not $legalTexts.Content.Contains("demo teknik")) "Old legal placeholder copy is still visible."

if (-not $SkipFullSmoke) {
    $seed = [int]((Get-Date).Ticks % 1000000000)
    $phone = "0532" + $seed.ToString().PadLeft(7, "0").Substring(0, 7)
    $deviceId = "demo-release-$seed"
    $email = "demo-release-$seed@example.test"
    $nationalId = New-Tckn $seed

    $otp = Invoke-Json -Method Post -Uri "$apiBase/api/otp/request" -Body @{
        phone = $phone
        captchaToken = $CaptchaToken
        deviceId = $deviceId
    }
    Assert-True (-not [string]::IsNullOrWhiteSpace($otp.developmentCode)) "Mock OTP code was not returned by demo environment."

    $verify = Invoke-Json -Method Post -Uri "$apiBase/api/otp/verify" -Body @{
        phone = $phone
        code = $otp.developmentCode
        deviceId = $deviceId
    }
    Assert-True (-not [string]::IsNullOrWhiteSpace($verify.verificationToken)) "OTP verification token was not returned."

    $application = Invoke-Json -Method Post -Uri "$apiBase/api/applications" -Body @{
        firstName = "Demo"
        lastName = "Release"
        nationalId = $nationalId
        phone = $phone
        email = $email
        provinceId = 34
        districtId = 3401
        neighborhoodId = 340101
        address = "Render demo release smoke adresi No:1"
        postalCode = "34710"
        privacyNoticeAccepted = $true
        explicitConsentAccepted = $true
        verificationToken = $verify.verificationToken
        idempotencyKey = "demo-release-$seed"
    }
    Assert-True ($application.status -eq "Assigned") "Application was not assigned."

    $login = Invoke-Json -Method Post -Uri "$apiBase/api/admin/auth/login" -Body @{
        email = $AdminEmail
        password = $AdminPassword
        totpCode = $null
    }
    Assert-True (-not [string]::IsNullOrWhiteSpace($login.accessToken)) "Admin access token was not returned."

    $authHeaders = @{ Authorization = "Bearer $($login.accessToken)" }
    $listed = Invoke-RestMethod -Method Get -Uri "$apiBase/api/admin/applications?page=1&pageSize=10&email=$([uri]::EscapeDataString($email))" -Headers $authHeaders -TimeoutSec $TimeoutSec
    Assert-True ($listed.total -ge 1) "Created application was not listed in admin search."

    $exportBody = @{
        format = 0
        filters = @{
            page = $null
            pageSize = $null
            sort = $null
            desc = $null
            status = $null
            firstName = $null
            lastName = $null
            nationalId = $null
            phone = $null
            email = $email
            provinceId = $null
            districtId = $null
            neighborhoodId = $null
            representativeOfficeId = $null
            isAssigned = $null
            isPhoneVerified = $null
            from = $null
            to = $null
        }
    } | ConvertTo-Json -Depth 6
    $export = Invoke-WebRequest -UseBasicParsing -Method Post -Uri "$apiBase/api/admin/exports" -ContentType "application/json" -Headers $authHeaders -Body $exportBody -TimeoutSec $TimeoutSec
    Assert-True ($export.StatusCode -eq 200) "Admin CSV export did not return 200."
    Assert-True ($export.Content.Contains($email)) "Admin CSV export does not contain the smoke row."

    Write-Output "Full demo release smoke passed. Reference: $($application.referenceNumber)"
} else {
    Write-Output "Basic demo release smoke passed."
}
