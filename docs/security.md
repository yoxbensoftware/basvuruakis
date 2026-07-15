# Security Notes

## Uygulanan Kontroller

- JWT access token ve refresh token rotation.
- BCrypt parola hashleme.
- Production ilk admin bootstrap'i environment secret değerleriyle yapılır ve MFA zorunlu tutulur.
- Permission bazlı admin endpoint koruması.
- Başarısız login, CAPTCHA hatası, detay görüntüleme, export ve atama için log altyapısı.
- AES-256-GCM alan şifreleme.
- Ayrı HMAC-SHA-256 lookup hash.
- OTP hash saklama, 3 dakika süre, 5 deneme, 60 saniye resend cooldown, IP ve cihaz bazlı saatlik rate limit.
- Başarısız CAPTCHA/OTP doğrulamaları ve OTP rate-limit olayları security log’a yazılır.
- CAPTCHA production’da Turnstile secret ve web build tarafında Turnstile site key gerektirir.
- Production SMS provider `http-json` HTTPS adapter ile gerçek provider çağrısı yapar; log-only/stub provider kabul edilmez.
- Development fake provider production ortamında bypass sağlamaz.
- CSV/XLSX formula injection sanitization.
- Security headers: `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`, temel CSP.
- NuGet ve pnpm dependency sürümleri pinlidir.

## Production Sertleştirme Adımları

- TLS termination ve HSTS edge veya Nginx üzerinde etkinleştirilmeli.
- Cloudflare WAF, Turnstile, rate limit ve origin erişim kısıtları kurulmalı.
- Admin MFA operasyonel olarak zorunlu tutulmalı; yeni production admin hesapları MFA'sız bırakılmamalı.
- Secret store kullanılmalı; `.env` dosyası production sunucuda kalıcı operasyon standardı olmamalı.
- Container image digest pinleme ve imaj zafiyet taraması CI/CD’ye bağlanmalı.
- DAST ve manuel OWASP ASVS kontrolü production yayını öncesi çalıştırılmalı.
