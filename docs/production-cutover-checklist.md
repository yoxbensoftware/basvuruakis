# Production Cutover Checklist

Bu checklist production trafiğini açmadan hemen önce uygulanır. Render demo/staging ortamı canlıdır; aşağıdaki adımlar gerçek production için gereklidir.

## 1. Dış Servisler

- Production domain, DNS, TLS ve Cloudflare WAF hazır.
- Cloudflare Turnstile site key ve secret üretildi.
- SMS provider hesabı, HTTPS endpoint, sender, API key, maliyet limiti ve provider rate limitleri hazır.
- Production PostgreSQL ve bağımsız backup hedefi hazır.
- Secret manager hazır; `.env` dosyası kalıcı production standardı olarak kullanılmıyor.

## 2. Secret ve Env Hazırlığı

Production-owned random değerleri üret:

```powershell
.\infrastructure\production\new-production-secrets.ps1 -AdminEmail "admin@example.com" -OutputPath .\artifacts\production\generated.env -Force
```

Değerleri secret manager'a taşı; yerel dosyayı sil.

Production env değerlerini doğrula:

```powershell
.\infrastructure\production\test-production-env.ps1
```

Zorunlu kritik ayarlar:

- `ASPNETCORE_ENVIRONMENT=Production`
- `Database__Provider=Postgres`
- `ConnectionStrings__Postgres`
- `Security__EncryptionKey`
- `Security__LookupKey`
- `Jwt__SigningKey`
- `AdminBootstrap__Email`
- `AdminBootstrap__Password`
- `AdminBootstrap__MfaSecret`
- `Captcha__TurnstileSecret`
- `Sms__Provider=http-json`
- `Sms__ApiKey`
- `Sms__Endpoint`
- `Sms__Sender`
- `Sms__MessageTemplate`
- `Cors__AllowedOrigins__0`
- `NEXT_PUBLIC_CAPTCHA_PROVIDER=turnstile`
- `NEXT_PUBLIC_TURNSTILE_SITE_KEY`
- `NEXT_PUBLIC_API_URL` veya `NEXT_PUBLIC_API_HOST`

## 3. Database Migration

Önce SQL üret ve review et:

```powershell
.\infrastructure\production\invoke-production-migration.ps1
```

Backup ve onay sonrası uygula:

```powershell
.\infrastructure\production\invoke-production-migration.ps1 -ConnectionString "<production-postgres-connection-string>" -Apply
```

## 4. Operasyon Verisi

Hukuk onaylı KVKK/açık rıza metinlerini dosyaya koy. Sonra seed SQL üret:

```powershell
.\infrastructure\production\new-operational-seed-sql.ps1 `
  -PrivacyNoticeFile .\artifacts\legal\privacy-notice.txt `
  -ExplicitConsentFile .\artifacts\legal\explicit-consent.txt `
  -CookiePolicyFile .\artifacts\legal\cookie-policy.txt `
  -LegalVersion "2026-07-16" `
  -OutputPath .\artifacts\production\operational-seed.sql
```

SQL'i review et, production PostgreSQL üzerinde kontrollü çalıştır. `/health/ready` bu veri yoksa production'da 503 döner.

## 5. Backup ve Restore

- İlk production backup alındı.
- Checksum ve metadata üretildi.
- Backup bağımsız S3 uyumlu hedefe aktarıldı.
- Temiz ortamda restore smoke testi geçti.
- Backup failure alarmı aktif.

## 6. Security ve Load Gate

- CI başarılı.
- Container image scan temiz.
- DAST ve manuel OWASP ASVS hızlı kontrol tamamlandı.
- k6 smoke ve hedef staging load testi geçti.
- Admin MFA ile giriş doğrulandı.
- SMS maliyet limitleri ve Cloudflare rate limitleri test edildi.

## 7. Trafik Açma

- API `/health/live` 200.
- API `/health/ready` 200.
- Web `/`, `/basvuru`, `/admin` 200.
- OTP request/verify, başvuru oluşturma, admin liste ve CSV export smoke geçti.
- Rollback image tag'i ve DNS geri dönüş planı hazır.
