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

## Production Notları

- Production’da SQLite kullanılmaz; PostgreSQL zorunludur.
- Migration stratejisi deployment pipeline içinde kontrollü çalıştırılmalıdır.
- Compose dosyası staging/demo için uygundur. Yüksek trafik production’da managed PostgreSQL, managed Redis, object storage ve edge WAF tercih edilmelidir.
- Zero/low downtime için API ve web image’ları ayrı taglenmeli, migration ayrı job olarak koşturulmalı, rollback imaj tag’i hazır tutulmalıdır.
