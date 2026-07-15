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
dotnet tool restore
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
docker compose -f .\infrastructure\docker-compose.yml --env-file .\infrastructure\.env up --build
```

## Güvenlik Notu

Production ortamında aşağıdaki değerler environment/secret store üzerinden verilmeden uygulama yayınlanmamalıdır:

- Alan şifreleme anahtarı.
- Arama/mükerrer HMAC anahtarı.
- JWT signing key.
- Admin bootstrap secret.
- Admin bootstrap MFA secret.
- CAPTCHA secret ve Turnstile site key.
- SMS provider credential, HTTPS endpoint ve sender.
- S3/object storage credential.

Development fake provider’lar yalnızca development ortamında aktiftir.

Production geçişi için uygulanacak komutlu runbook:

- `docs/production-cutover-checklist.md`
- `infrastructure/production/new-production-secrets.ps1`
- `infrastructure/production/test-production-env.ps1`
- `infrastructure/production/invoke-production-migration.ps1`
- `infrastructure/production/new-operational-seed-sql.ps1`
