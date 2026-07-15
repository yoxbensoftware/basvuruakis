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
| `Captcha__TurnstileSecret` | Production | Cloudflare Turnstile secret. |
| `Sms__Provider` | Production | SMS provider adapter adı. |
| `Sms__ApiKey` | Production | SMS provider credential. |
| `Cors__AllowedOrigins__0` | Production/Staging | Web origin allowlist değeri. |

Development ortamında eksik secret’lar development-only fallback ile çalışır. Production ortamında eksik kritik secret uygulama başlangıcını durdurur.

## Web

| Değişken | Açıklama |
| --- | --- |
| `NEXT_PUBLIC_API_URL` | Browser’dan erişilecek API base URL. Local geliştirmede `http://localhost:5000`. Nginx arkasında aynı origin kullanılacaksa boş bırakılabilir veya edge URL verilir. |

## Infrastructure

`infrastructure/.env.example` dosyası Compose için minimum değişkenleri içerir. Gerçek değerler `.env` dosyasında veya secret manager’da tutulmalıdır.
