# Delivery Notes

Bu dosya geliştirme sırasında tamamlananları, doğrulama sonuçlarını ve production için manuel konfigürasyon gerektiren noktaları kaydeder.

## Teknik Kararlar

- Monorepo, gereksinim dokümanındaki öneriye uygun olarak ASP.NET Core API ve Next.js web uygulaması şeklinde kurulacaktır.
- Dış servisler adapter arkasında soyutlanacaktır. Development/test ortamında fake provider kullanılabilir; production ortamı gerçek secret ve provider konfigürasyonu olmadan başlamamalıdır.
- PostgreSQL ana veritabanıdır. Testlerde hızlı ve izole doğrulama için SQLite/in-memory yaklaşımı kullanılabilir.
- Redis development Compose içinde bulunur; kritik kısa ömürlü state servisleri önce uygulama içi test edilebilir abstraction ile kurulup production deployment’da Redis’e bağlanır.
- Next/React/TypeScript/ESLint paketleri exact pinlendi. `postcss` transitive güvenlik bulgusu `pnpm-workspace.yaml` override ile `8.5.10` sürümüne çekildi.
- pnpm build script onayı yalnızca `sharp` ve `unrs-resolver` için verildi.

## Production Manuel Konfigürasyonları

- Cloudflare domain, DNS, WAF, Turnstile ve rate limit kuralları.
- SMS provider hesabı, gönderici adı, maliyet limiti ve credential tanımı.
- S3 uyumlu object storage bucket, lifecycle policy ve credential tanımı.
- Secret store veya hosting platform secret manager.
- TLS sertifikası, HSTS ve Nginx/edge yönlendirme.
- PostgreSQL yedekleme hedefi, saklama politikası ve restore testi.
- Monitoring alarm kanalları.
- KVKK metinlerinin hukuk onayı ve saklama sürelerinin veri sorumlusu tarafından kesinleştirilmesi.
- Production image digest pinleme ve container scan.
- Tam staging yük testi.
- Admin MFA zorunluluğu ve ilk admin bootstrap prosedürünün operasyonel hale getirilmesi.

## Doğrulama Günlüğü

15 Temmuz 2026 doğrulama sonuçları:

- `dotnet test .\BasvuruAkis.slnx`: başarılı, 14/14 test geçti.
- `pnpm --dir .\apps\web lint`: başarılı.
- `pnpm --dir .\apps\web typecheck`: başarılı.
- `pnpm --dir .\apps\web build`: başarılı.
- `dotnet list ... package --vulnerable --include-transitive`: API ve test projesinde bilinen vulnerable NuGet paketi yok.
- `pnpm audit --audit-level moderate`: bilinen vulnerability yok.
- `pnpm peers check`: peer dependency sorunu yok.
- `docker compose -f .\infrastructure\docker-compose.yml config`: örnek secret env değerleriyle başarılı.
