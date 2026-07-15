# BaşvuruAkış Proje Talimatları

Bu dosya, bu repository içinde çalışan tüm Codex/agent oturumları için proje özel kuralları tanımlar.

## Dil ve Teslim Prensibi

- Kullanıcıya Türkçe yanıt ver.
- Öncelik sırası: veri bütünlüğü, güvenlik, basitlik, çalışan çıktı, test edilebilirlik.
- Kapsam dışı özellik ekleme. Gereksinim dokümanındaki modüller dışında ürün davranışı uydurma.
- Sunum/MVP hedefinde dış servisler adapter arkasında fake development provider ile çalışabilir; production build içinde bypass veya sabit gizli bilgi bulunamaz.
- Gerçek SMS, CAPTCHA, object storage, WAF, domain, TLS, monitoring ve secret store değerleri environment/operasyon dokümantasyonunda bırakılır.

## Mimari

- Monorepo yapısı:
  - `apps/api`: ASP.NET Core Web API.
  - `apps/web`: Next.js + TypeScript public site ve admin arayüzü.
  - `tests`: API unit/integration ve web/e2e testleri.
  - `infrastructure`: Docker, Nginx, backup, monitoring ve deployment dosyaları.
  - `docs`: mimari, test, güvenlik, KVKK ve operasyon dokümanları.
- API modüler monolit olarak kalır. Yeni mikroservis, message broker veya ayrı deployment birimi yalnızca somut gereksinim varsa eklenir.
- Provider bağımlılıkları adapter arkasında tutulur: SMS, CAPTCHA, storage, secret store, clock, background job.
- Admin authorization sunucu tarafında permission bazlı uygulanır. UI kontrolü tek başına yetki sayılmaz.
- Hassas alanlar için şifreleme ve deterministik arama/mükerrer hash ayrı anahtarlarla yapılır.

## Güvenlik ve KVKK

- Repository içinde gerçek secret, production credential, kişisel veri veya müşteri verisi tutulmaz.
- Development secret üretilebilir ancak production ortamında zorunlu environment variable eksikse uygulama güvenli şekilde başlamamalıdır.
- TCKN, telefon, e-posta ve açık adres düz metin persist edilmez.
- Audit/security log hassas veri içermez; kim, ne yaptı, ne zaman, hangi correlation ID ile yaptı bilgisine odaklanır.
- CSV/XLSX export içinde formula injection önlenir.
- OTP kodları düz metin saklanmaz; hashlenir ve süre/deneme/cooldown kurallarına uyar.

## Test ve Kalite Kapısı

- Her davranış değişikliğinde en dar ilgili test eklenir veya güncellenir.
- Minimum kalite kapısı:
  - API build.
  - API unit/integration testleri.
  - Web type-check/lint/build.
  - Kritik güvenlik yardımcıları için unit test.
- Çalıştırılmayan kontrol başarılı gibi raporlanmaz.
- Flaky test, test zayıflatma veya gerçek davranışı gizleyen mock ekleme kabul edilmez.

## Git ve Dosya Güvenliği

- Kullanıcı açıkça istemedikçe commit, push, merge, deploy veya publish yapılmaz.
- İlgisiz dosyalar değiştirilmez.
- Build çıktısı, geçici dosya, secret ve local environment dosyaları commit kapsamına alınmaz.

## Tamamlanma Kriteri

Bir aşama tamamlandı sayılmadan önce:

- İlgili endpoint/UI akışı çalışır.
- Veri doğrulama, yetki ve audit etkileri uygulanmıştır.
- Testler ve build komutları koşulmuştur veya neden koşulamadığı yazılmıştır.
- Production için manuel konfigürasyon gerektiren noktalar `docs/delivery-notes.md` içinde açıkça belirtilmiştir.
