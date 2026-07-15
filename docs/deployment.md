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
2. Secret değerlerini değiştir.
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
- Production'a geçerken `ASPNETCORE_ENVIRONMENT=Production` yapılmalı, gerçek SMS/CAPTCHA/object storage ayarları eklenmeli ve migration süreci kontrollü yürütülmelidir.
- Web, API host bilgisini Render'ın `RENDER_EXTERNAL_HOSTNAME` değerinden alır. API CORS origin'i web servisinin `RENDER_EXTERNAL_URL` değerine bağlanır.

## Production Notları

- Production’da SQLite kullanılmaz; PostgreSQL zorunludur.
- Migration stratejisi deployment pipeline içinde kontrollü çalıştırılmalıdır.
- Compose dosyası staging/demo için uygundur. Yüksek trafik production’da managed PostgreSQL, managed Redis, object storage ve edge WAF tercih edilmelidir.
- Zero/low downtime için API ve web image’ları ayrı taglenmeli, migration ayrı job olarak koşturulmalı, rollback imaj tag’i hazır tutulmalıdır.
