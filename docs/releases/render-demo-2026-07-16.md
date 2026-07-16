# Render Demo Release - 2026-07-16

## Release

- Type: Prototype/demo release.
- Hosting: Render.
- Web: `https://basvuruakis-web.onrender.com`
- API: `https://basvuruakis-api.onrender.com`
- Environment: `Staging`

## Mocked Production Items

- Domain/DNS/TLS: Render `onrender.com` domain and managed TLS.
- WAF/rate limit: App-level OTP IP/device limits, security headers and Render edge are used for demo.
- CAPTCHA: `NEXT_PUBLIC_CAPTCHA_PROVIDER=development`; real Turnstile is deferred.
- SMS: non-production mock OTP provider returns `developmentCode`; real provider is deferred.
- PostgreSQL: Render Postgres demo database.
- Secret manager: Render generated values for app secrets; real production secret manager is deferred.
- KVKK/legal texts: presentation-safe dummy legal texts seeded by the app.
- Backup/object storage: production external backup target is deferred; backup scripts are ready.
- Monitoring: Render health check plus demo smoke script.
- MFA: production MFA path is implemented; demo admin login uses seeded demo admin without MFA.

## Release Gates

- GitHub CI must be green.
- `pwsh ./infrastructure/security/secret-scan.ps1`
- `dotnet test ./BasvuruAkis.slnx`
- `pnpm --dir ./apps/web lint`
- `pnpm --dir ./apps/web typecheck`
- `pnpm --dir ./apps/web build`
- `./infrastructure/production/test-demo-release.ps1`

## Rollback

Rollback for the demo release is a Render redeploy of the previous known-good commit or tag.

1. In Render, open API and Web services.
2. Use Manual Deploy and select the previous commit.
3. Verify `/health/ready`, `/`, `/basvuru`, `/admin`.
4. Run `./infrastructure/production/test-demo-release.ps1 -SkipFullSmoke`.

Production rollback must additionally include DNS, database migration and backup/restore decisions from `docs/production-cutover-checklist.md`.
