# Deployment

## Local Development

API:

```powershell
dotnet run --project .\apps\api\BasvuruAkis.Api.csproj
```

Web:

```powershell
pnpm --dir .\apps\web dev
```

## Docker Compose

1. `infrastructure/.env.example` dosyasını `infrastructure/.env` olarak kopyala.
2. Secret değerlerini değiştir. Compose dosyası production-like çalışır; Turnstile site/secret ve `http-json` SMS endpoint değerleri verilmeden config üretmez.
3. Çalıştır:

```powershell
docker compose -f .\infrastructure\docker-compose.yml --env-file .\infrastructure\.env up --build
```

Servisler:

- Web/Nginx: `http://localhost:8080`
- API direct: `http://localhost:5000`
- MinIO console: `http://localhost:9001`

## Render Blueprint

Repository root'undaki `render.yaml`, demo/staging amaçlı Render kurulumu için üç kaynak oluşturur:

- `basvuruakis-api`: Docker tabanlı ASP.NET Core API.
- `basvuruakis-web`: Docker tabanlı Next.js web arayüzü.
- `basvuruakis-db`: Render Postgres.

Kurulum:

1. Render Dashboard içinde `New > Blueprint` seç.
2. `yoxbensoftware/basvuruakis` repository'sini ve `main` branch'ini bağla.
3. Blueprint sync işlemini başlat.

Canlı demo/staging ortamı:

- Web: `https://basvuruakis-web.onrender.com`
- API: `https://basvuruakis-api.onrender.com`
- API health: `/health/live`, `/health/ready`

Notlar:

- Blueprint free plan ile tanımlıdır; Render free servisleri boşta uyuyabilir ve ilk istek yavaş gelebilir.
- API `ASPNETCORE_ENVIRONMENT=Staging` ile çalışır. Bu, müşteri demosu için sahte OTP/CAPTCHA adapter'larını ve otomatik veritabanı şema oluşturmayı aktif tutar.
- Production'a geçerken gerçek SMS/CAPTCHA/object storage ayarları, `AdminBootstrap__Email`, `AdminBootstrap__Password`, Base32 `AdminBootstrap__MfaSecret`, aktif KVKK/açık rıza metinleri, lokasyon referans verisi, aktif default temsilcilik ve kontrollü migration job uygulanmalıdır.
- Web, API host bilgisini Render'ın `RENDER_EXTERNAL_HOSTNAME` değerinden alır. API CORS origin'i web servisinin `RENDER_EXTERNAL_URL` değerine bağlanır.
- Render Blueprint staging ortamında `NEXT_PUBLIC_CAPTCHA_PROVIDER=development` kullanır; production web build'de `turnstile` ve `NEXT_PUBLIC_TURNSTILE_SITE_KEY` verilmelidir.

## Production Migration

Production API startup otomatik `EnsureCreated` veya `Migrate` çalıştırmaz. Migration ayrı ve kontrollü release job olarak çalıştırılmalıdır.

Araçları geri yükle:

```powershell
dotnet tool restore
```

Idempotent SQL script üret:

```powershell
dotnet tool run dotnet-ef migrations script --idempotent --project .\apps\api\BasvuruAkis.Api.csproj --startup-project .\apps\api\BasvuruAkis.Api.csproj --context AppDbContext --output .\artifacts\migrations\basvuruakis.sql
```

Kontrollü ortamda doğrudan uygula:

```powershell
dotnet tool run dotnet-ef database update --project .\apps\api\BasvuruAkis.Api.csproj --startup-project .\apps\api\BasvuruAkis.Api.csproj --context AppDbContext --connection "<production-postgres-connection-string>"
```

Production uygulamadan önce backup alınmalı, script review edilmeli ve rollback planı hazır olmalıdır.

## Production Notları

- Production’da SQLite kullanılmaz; PostgreSQL zorunludur.
- Migration deployment pipeline içinde API deploy'undan ayrı job olarak kontrollü çalıştırılmalıdır.
- `/health/ready`, production ortamında veritabanına ek olarak başvuru alabilmek için zorunlu operasyon verilerini de kontrol eder: aktif KVKK metni, aktif açık rıza metni, en az bir mahalle ve aktif default temsilcilik.
- Production SMS için `Sms__Provider=http-json`, HTTPS `Sms__Endpoint`, `Sms__ApiKey`, `Sms__Sender` ve `{code}` içeren `Sms__MessageTemplate` gerekir.
- Compose dosyası production-like local/staging smoke için uygundur. Yüksek trafik production’da managed PostgreSQL, managed Redis, object storage ve edge WAF tercih edilmelidir.
- Backup job, `backup-postgres.ps1` çıktısını checksum ve metadata dosyalarıyla birlikte bağımsız S3 uyumlu hedefe taşımalı; restore smoke release öncesi doğrulanmalıdır.
- Zero/low downtime için API ve web image’ları ayrı taglenmeli, migration ayrı job olarak koşturulmalı, rollback imaj tag’i hazır tutulmalıdır.
