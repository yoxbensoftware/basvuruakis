# BaşvuruAkış Geliştirme Skill Haritası

Bu dosya, projeyi hızlı ve kontrollü geliştirmek için kullanılacak iç çalışma becerilerini tanımlar.

## 1. Gereksinim İzleme

- Kaynak doküman: `BasvuruAkis_Tam_Kapsam_Gereksinim_ve_Codex_Gelistirme_Dokumani.pdf`.
- Her modül, `docs/test-scenarios.md` ve `docs/delivery-notes.md` ile izlenebilir olmalıdır.
- Kapsam değişikliği gerektiğinde ürün davranışı varsayımı yapılmaz; kullanıcı onayı gerektiren karar teslim notuna alınır.

## 2. Güvenli API Geliştirme

- ASP.NET Core endpointleri request DTO + validation + permission policy + audit etkisi ile tasarlanır.
- Hassas alanlar domain servisleri üzerinden şifrelenir/maskelenir; endpoint içinde kripto detayı dağıtılmaz.
- Public endpointlerde rate limit, CAPTCHA/OTP/idempotency gibi abuse kontrolleri varsayılan kabul edilir.

## 3. Admin Panel Geliştirme

- Admin ekranlarında veri listeleme server-side pagination/filtering/sorting ile yapılır.
- Liste ekranlarında hassas veri maskelidir.
- Detay görüntüleme, dışa aktarma, manuel atama, silme ve anonimleştirme ayrı permission gerektirir.

## 4. Test Tasarımı

- Unit test: saf domain kuralları ve güvenlik yardımcıları.
- Integration test: endpoint + DB + authorization + audit davranışı.
- E2E/smoke test: public başvuru akışı, admin login, listeleme, detay, export.
- Test verisi sentetik olmalı; gerçek TCKN/telefon/e-posta kullanılmamalıdır.

## 5. Operasyon ve Deployment

- Development Docker Compose ücretsiz/yerel servislerle çalışır: PostgreSQL, Redis, MinIO, Nginx.
- Production için ücretsiz/ucuz seçenekler dokümante edilir: Cloudflare Free, object storage uyumlu düşük maliyetli sağlayıcı, managed PostgreSQL free tier uygunluğu.
- Backup/restore scriptleri dry-run veya local mode ile test edilebilir olmalıdır.
