[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PrivacyNoticeFile,
    [Parameter(Mandatory = $true)]
    [string]$ExplicitConsentFile,
    [string]$CookiePolicyFile,
    [string]$LegalVersion = (Get-Date -Format "yyyy-MM-dd"),
    [int]$RegionId = 1,
    [string]$RegionName = "Marmara",
    [int]$ProvinceId = 34,
    [string]$ProvinceName = "Istanbul",
    [int]$DistrictId = 3401,
    [string]$DistrictName = "Kadikoy",
    [int]$NeighborhoodId = 340101,
    [string]$NeighborhoodName = "Caferaga",
    [int]$DefaultOfficeId = 1,
    [string]$DefaultOfficeName = "Genel Merkez",
    [string]$OutputPath = "artifacts/production/operational-seed.sql"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Read-RequiredFile {
    param(
        [string]$Path,
        [string]$Label
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Label file not found: $Path"
    }

    $content = Get-Content -LiteralPath $Path -Raw
    if ([string]::IsNullOrWhiteSpace($content)) {
        throw "$Label file is empty: $Path"
    }

    return $content.Trim()
}

function Sql-Literal {
    param([string]$Value)
    return "'" + $Value.Replace("'", "''") + "'"
}

$privacyNotice = Read-RequiredFile $PrivacyNoticeFile "Privacy notice"
$explicitConsent = Read-RequiredFile $ExplicitConsentFile "Explicit consent"
$cookiePolicy = if ([string]::IsNullOrWhiteSpace($CookiePolicyFile)) {
    "Zorunlu cerezler oturum guvenligi ve tercihlerin saklanmasi icin kullanilir. Zorunlu olmayan cerezler kullanici onayi olmadan calistirilmaz."
} else {
    Read-RequiredFile $CookiePolicyFile "Cookie policy"
}

$nowMs = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
$privacyId = [Guid]::NewGuid()
$consentId = [Guid]::NewGuid()
$cookieId = [Guid]::NewGuid()
$assignmentNowMs = $nowMs

$sql = @"
-- BasvuruAkis production operational seed.
-- Review and run after EF migrations, before opening traffic.
-- This script is idempotent for the configured reference data and legal text version.

BEGIN;

INSERT INTO "Regions" ("Id", "Name")
VALUES ($RegionId, $(Sql-Literal $RegionName))
ON CONFLICT ("Id") DO UPDATE SET "Name" = EXCLUDED."Name";

INSERT INTO "Provinces" ("Id", "Name", "RegionId")
VALUES ($ProvinceId, $(Sql-Literal $ProvinceName), $RegionId)
ON CONFLICT ("Id") DO UPDATE SET "Name" = EXCLUDED."Name", "RegionId" = EXCLUDED."RegionId";

INSERT INTO "Districts" ("Id", "ProvinceId", "Name")
VALUES ($DistrictId, $ProvinceId, $(Sql-Literal $DistrictName))
ON CONFLICT ("Id") DO UPDATE SET "ProvinceId" = EXCLUDED."ProvinceId", "Name" = EXCLUDED."Name";

INSERT INTO "Neighborhoods" ("Id", "DistrictId", "Name")
VALUES ($NeighborhoodId, $DistrictId, $(Sql-Literal $NeighborhoodName))
ON CONFLICT ("Id") DO UPDATE SET "DistrictId" = EXCLUDED."DistrictId", "Name" = EXCLUDED."Name";

UPDATE "RepresentativeOffices" SET "IsDefault" = FALSE WHERE "Id" <> $DefaultOfficeId;

INSERT INTO "RepresentativeOffices" ("Id", "Name", "IsDefault", "IsActive")
VALUES ($DefaultOfficeId, $(Sql-Literal $DefaultOfficeName), TRUE, TRUE)
ON CONFLICT ("Id") DO UPDATE
SET "Name" = EXCLUDED."Name", "IsDefault" = TRUE, "IsActive" = TRUE;

UPDATE "LegalTexts" SET "IsActive" = FALSE WHERE "Type" = 'PrivacyNotice' AND "IsActive" = TRUE;
INSERT INTO "LegalTexts" ("Id", "Type", "Version", "Title", "Body", "IsActive", "PublishedAt")
VALUES ('$privacyId', 'PrivacyNotice', $(Sql-Literal $LegalVersion), 'KVKK Aydınlatma Metni', $(Sql-Literal $privacyNotice), TRUE, $nowMs)
ON CONFLICT ("Type", "Version") DO UPDATE
SET "Title" = EXCLUDED."Title", "Body" = EXCLUDED."Body", "IsActive" = TRUE, "PublishedAt" = EXCLUDED."PublishedAt";

UPDATE "LegalTexts" SET "IsActive" = FALSE WHERE "Type" = 'ExplicitConsent' AND "IsActive" = TRUE;
INSERT INTO "LegalTexts" ("Id", "Type", "Version", "Title", "Body", "IsActive", "PublishedAt")
VALUES ('$consentId', 'ExplicitConsent', $(Sql-Literal $LegalVersion), 'Açık Rıza Metni', $(Sql-Literal $explicitConsent), TRUE, $nowMs)
ON CONFLICT ("Type", "Version") DO UPDATE
SET "Title" = EXCLUDED."Title", "Body" = EXCLUDED."Body", "IsActive" = TRUE, "PublishedAt" = EXCLUDED."PublishedAt";

UPDATE "LegalTexts" SET "IsActive" = FALSE WHERE "Type" = 'CookiePolicy' AND "IsActive" = TRUE;
INSERT INTO "LegalTexts" ("Id", "Type", "Version", "Title", "Body", "IsActive", "PublishedAt")
VALUES ('$cookieId', 'CookiePolicy', $(Sql-Literal $LegalVersion), 'Çerez Politikası', $(Sql-Literal $cookiePolicy), TRUE, $nowMs)
ON CONFLICT ("Type", "Version") DO UPDATE
SET "Title" = EXCLUDED."Title", "Body" = EXCLUDED."Body", "IsActive" = TRUE, "PublishedAt" = EXCLUDED."PublishedAt";

INSERT INTO "AssignmentRules" ("Scope", "ScopeId", "RepresentativeOfficeId", "Priority", "IsActive", "CreatedAt")
SELECT 'Default', NULL, $DefaultOfficeId, 999, TRUE, $assignmentNowMs
WHERE NOT EXISTS (
    SELECT 1 FROM "AssignmentRules" WHERE "Scope" = 'Default' AND "IsActive" = TRUE
);

SELECT setval(pg_get_serial_sequence('"Regions"', 'Id'), GREATEST((SELECT COALESCE(MAX("Id"), 1) FROM "Regions"), 1), TRUE);
SELECT setval(pg_get_serial_sequence('"Provinces"', 'Id'), GREATEST((SELECT COALESCE(MAX("Id"), 1) FROM "Provinces"), 1), TRUE);
SELECT setval(pg_get_serial_sequence('"Districts"', 'Id'), GREATEST((SELECT COALESCE(MAX("Id"), 1) FROM "Districts"), 1), TRUE);
SELECT setval(pg_get_serial_sequence('"Neighborhoods"', 'Id'), GREATEST((SELECT COALESCE(MAX("Id"), 1) FROM "Neighborhoods"), 1), TRUE);
SELECT setval(pg_get_serial_sequence('"RepresentativeOffices"', 'Id'), GREATEST((SELECT COALESCE(MAX("Id"), 1) FROM "RepresentativeOffices"), 1), TRUE);

COMMIT;
"@

$resolvedOutput = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
$directory = Split-Path -Parent $resolvedOutput
if (-not [string]::IsNullOrWhiteSpace($directory)) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}

Set-Content -LiteralPath $resolvedOutput -Value $sql -Encoding utf8NoBOM
Write-Output "Generated operational seed SQL: $resolvedOutput"
