# Delivery Notes

Bu dosya geliştirme sırasında tamamlananları, doğrulama sonuçlarını ve production için manuel konfigürasyon gerektiren noktaları kaydeder.

## Teknik Kararlar

- Monorepo, gereksinim dokümanındaki öneriye uygun olarak ASP.NET Core API ve Next.js web uygulaması şeklinde kurulacaktır.
- Dış servisler adapter arkasında soyutlanacaktır. Development/test ortamında fake provider kullanılabilir; production ortamı gerçek secret ve provider konfigürasyonu olmadan başlamamalıdır.
- PostgreSQL ana veritabanıdır. Testlerde hızlı ve izole doğrulama için SQLite/in-memory yaklaşımı kullanılabilir.
- Redis development Compose içinde bulunur; kritik kısa ömürlü state servisleri önce uygulama içi test edilebilir abstraction ile kurulup production deployment’da Redis’e bağlanır.
- Next/React/TypeScript/ESLint paketleri exact pinlendi. `postcss` transitive güvenlik bulgusu `pnpm-workspace.yaml` override ile `8.5.10` sürümüne çekildi.
- pnpm build script onayı yalnızca `sharp` ve `unrs-resolver` için verildi.
- Admin başvuru listesi ve CSV/XLSX export aynı filtre yardımcısını kullanır. Hassas TCKN/telefon/e-posta aramaları normalize HMAC lookup hash ile yapılır; temsilcilik filtresi en son atama kaydını baz alır.
- Production ilk admin bootstrap'i environment secret değerlerine bağlandı ve MFA'sız admin hesabıyla production başlangıcı engellendi.
- Public başvuru formu Turnstile token akışını destekler; production-like Compose `NEXT_PUBLIC_CAPTCHA_PROVIDER=turnstile` ve site key olmadan build edilmez.
- Production SMS gönderimi `http-json` adapter ile gerçek HTTPS endpoint çağrısı yapar; log-only/stub provider production’da kabul edilmez.
- PostgreSQL `InitialCreate` EF migration dosyası üretildi; production DB şeması uygulama startup'ından ayrı migration job ile kurulacak şekilde dokümante edildi.
- PostgreSQL `AddRelationalConstraints` EF migration dosyası ile uygulama, lokasyon, temsilcilik, admin token, audit/security/export ve başvuru geçmişi ilişkileri foreign key seviyesinde bağlandı.
- Public başvuru akışı aktif KVKK/açık rıza, lokasyon hiyerarşisi ve aktif default temsilcilik kontrollerini OTP verification token tüketmeden önce yapar.
- Başvuru referans numarası üretimi servis arkasına alındı; çakışma durumunda güvenli yeniden deneme ve unique constraint yarışları için kontrollü API yanıtı eklendi.
- Admin MFA başarısız denemeleri parola hatalarıyla aynı kilit politikasına bağlandı ve security log'a yazıldı.
- Production readiness kontrolü başvuru alabilmek için zorunlu operasyon verileri eksikse 503 dönecek şekilde sıkılaştırıldı.
- Backup/restore scriptleri SHA-256 checksum, metadata, retention temizliği ve restore için zorunlu `-Force` koruması ile sertleştirildi.
- Web response security header'ları ve API production HSTS eklendi.

## Production Manuel Konfigürasyonları

- Cloudflare domain, DNS, WAF, Turnstile ve rate limit kuralları.
- SMS provider hesabı, `http-json` uyumlu endpoint, gönderici adı, maliyet limiti ve credential tanımı.
- S3 uyumlu object storage bucket, lifecycle policy ve credential tanımı.
- Secret store veya hosting platform secret manager.
- TLS sertifikası, production domain ve Nginx/edge yönlendirme.
- PostgreSQL yedekleme hedefi, saklama politikası ve restore testi.
- Monitoring alarm kanalları.
- KVKK metinlerinin hukuk onayı ve saklama sürelerinin veri sorumlusu tarafından kesinleştirilmesi.
- Production image digest pinleme ve container scan.
- Tam staging yük testi.
- İlk admin bootstrap secret değerlerinin ve MFA secret üretim prosedürünün secret manager üzerinden yönetilmesi.
- Aktif KVKK/açık rıza metinleri, lokasyon referans verisi ve aktif default temsilcilik production migration/seed sürecinde yüklenmelidir.

## Doğrulama Günlüğü

15 Temmuz 2026 doğrulama sonuçları:

- `dotnet test .\BasvuruAkis.slnx`: başarılı, 29/29 test geçti.
- `pnpm --dir .\apps\web lint`: başarılı.
- `pnpm --dir .\apps\web typecheck`: başarılı.
- `pnpm --dir .\apps\web build`: başarılı.
- `dotnet list ... package --vulnerable --include-transitive`: API ve test projesinde bilinen vulnerable NuGet paketi yok.
- `pnpm audit --audit-level moderate`: bilinen vulnerability yok.
- `pnpm peers check`: peer dependency sorunu yok.
- `docker compose -f .\infrastructure\docker-compose.yml config`: örnek secret env değerleriyle başarılı.
- `docker build -f .\apps\api\Dockerfile -t basvuruakis-api:verify .`: başarılı.
- `docker build -f .\apps\web\Dockerfile --build-arg NEXT_PUBLIC_API_HOST=basvuruakis-api.onrender.com --build-arg NEXT_PUBLIC_CAPTCHA_PROVIDER=development -t basvuruakis-web:verify .`: başarılı.
- `dotnet tool restore`: başarılı.
- `dotnet tool run dotnet-ef database update ... --connection <temporary-postgres>`: temiz PostgreSQL container üzerinde başarılı; `InitialCreate` ve `AddRelationalConstraints` uygulandı.
- `backup-postgres.ps1` + `restore-postgres.ps1 -Force`: geçici PostgreSQL container üzerinde checksum/metadata ve restore smoke başarılı.
- `basvuruakis-web:verify` container header smoke: CSP, HSTS, `X-Frame-Options=DENY`, `X-Content-Type-Options=nosniff` başarılı.
- `render.yaml` resmi Render schema ile doğrulandı.
- Render demo/staging canlı kontrolü başarılı: API `/health/live` ve `/health/ready` 200, web `/`, `/basvuru`, `/admin` 200.
