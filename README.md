# BaşvuruAkış

KVKK uyumlu başvuru toplama, OTP doğrulama, otomatik temsilcilik atama ve yönetim paneli sunan monorepo.

## Hedef Mimari

- `apps/api`: ASP.NET Core Web API.
- `apps/web`: Next.js + TypeScript public site ve admin paneli.
- `tests`: unit, integration ve e2e testleri.
- `infrastructure`: Docker Compose, Nginx, backup, deployment ve monitoring.
- `docs`: mimari, güvenlik, KVKK, test ve operasyon dokümantasyonu.

## Development Komutları

## Canlı Demo/Staging

- Web: `https://basvuruakis-web.onrender.com`
- API: `https://basvuruakis-api.onrender.com`
- API health: `https://basvuruakis-api.onrender.com/health/ready`

API:

```powershell
dotnet build .\BasvuruAkis.slnx
dotnet test .\BasvuruAkis.slnx
dotnet run --project .\apps\api\BasvuruAkis.Api.csproj
```

Web:

```powershell
cd .\apps\web
pnpm install
pnpm lint
pnpm typecheck
pnpm build
pnpm dev
```

Docker development ortamı:

```powershell
docker compose -f .\infrastructure\docker-compose.yml up --build
```

## Güvenlik Notu

Production ortamında aşağıdaki değerler environment/secret store üzerinden verilmeden uygulama yayınlanmamalıdır:

- Alan şifreleme anahtarı.
- Arama/mükerrer HMAC anahtarı.
- JWT signing key.
- Admin bootstrap secret.
- CAPTCHA secret.
- SMS provider credential.
- S3/object storage credential.

Development fake provider’lar yalnızca development ortamında aktiftir.
