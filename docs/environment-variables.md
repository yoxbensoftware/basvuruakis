# Environment Variables

## API

| Değişken | Zorunlu ortam | Açıklama |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | Tüm ortamlar | `Development`, `Staging` veya `Production`. |
| `Database__Provider` | Tüm ortamlar | `Postgres` veya development için `Sqlite`. Production varsayılanı `Postgres`. |
| `ConnectionStrings__Postgres` | Production/Staging | PostgreSQL bağlantı cümlesi. |
| `Security__EncryptionKey` | Production/Staging | AES-256-GCM alan şifreleme anahtar materyali. |
| `Security__LookupKey` | Production/Staging | HMAC-SHA-256 arama/mükerrer kontrol anahtar materyali. Encryption key ile aynı olamaz. |
| `Jwt__SigningKey` | Production/Staging | Admin access token imzalama anahtarı. |
| `AdminBootstrap__Email` | Production | Veritabanında admin yoksa oluşturulacak ilk admin e-posta adresi. |
| `AdminBootstrap__Password` | Production | İlk admin parolası. En az 14 karakter; büyük harf, küçük harf, rakam ve sembol içermelidir. |
| `AdminBootstrap__MfaSecret` | Production | İlk admin için Base32 TOTP secret. En az 16 Base32 karakter olmalıdır. |
| `Captcha__TurnstileSecret` | Production | Cloudflare Turnstile secret. |
| `Sms__Provider` | Production | SMS provider adapter adı. |
| `Sms__ApiKey` | Production | SMS provider credential. |
| `Cors__AllowedOrigins__0` | Production/Staging | Web origin allowlist değeri. |
| `Otp__MaxRequestsPerIpPerHour` | Tüm ortamlar | Aynı IP için saatlik OTP request üst limiti. Varsayılan `20`. |
| `Otp__MaxRequestsPerDevicePerHour` | Tüm ortamlar | Aynı cihaz kimliği için saatlik OTP request üst limiti. Varsayılan `10`. |

Development ortamında eksik secret’lar development-only fallback ile çalışır. Production ortamında eksik kritik secret veya MFA’sız admin hesabı uygulama başlangıcını durdurur.

## Web

| Değişken | Açıklama |
| --- | --- |
| `NEXT_PUBLIC_API_URL` | Browser’dan erişilecek API base URL. Local geliştirmede `http://localhost:5000`. Nginx arkasında aynı origin kullanılacaksa boş bırakılabilir veya edge URL verilir. |

## Infrastructure

`infrastructure/.env.example` dosyası Compose için minimum değişkenleri içerir. Gerçek değerler `.env` dosyasında veya secret manager’da tutulmalıdır.
