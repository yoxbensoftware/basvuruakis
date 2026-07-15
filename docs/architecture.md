# Mimari Notlar

## Sistem Sınırları

BaşvuruAkış; public web, admin web ve API’den oluşan modüler monolit olarak tasarlanır. Veri bütünlüğü gerektiren işlemler aynı API ve veritabanı transaction sınırında yürütülür.

## Ana Modüller

- Kimlik ve yetki: admin kullanıcı, rol, permission, MFA, refresh token rotation.
- Lokasyon ve yönlendirme: il, ilçe, mahalle, bölge, temsilcilik, atama kuralı.
- İçerik: kurumsal sayfalar, haberler, duyurular, medya dosyaları.
- KVKK: versiyonlu hukuki metinler, onay kayıtları, veri sahibi talepleri.
- Başvuru: OTP/CAPTCHA doğrulama, idempotency, alan şifreleme, mükerrer kontrol.
- Atama: otomatik ve manuel temsilcilik ataması, tarihçe.
- Admin operasyonu: listeleme, filtreleme, dashboard, export, audit/security log.
- Operasyon: backup/restore, health check, monitoring, load test.

## Veri Güvenliği

- TCKN, telefon, e-posta ve adres AES-256-GCM ile şifrelenir.
- TCKN, telefon ve e-posta için normalize HMAC-SHA-256 hash alanları tutulur.
- Şifreleme ve HMAC anahtarları farklıdır.
- Liste ekranlarında kişisel veri maskelenir.
- Detay görüntüleme permission ve audit gerektirir.

## Transaction Sınırları

- Başvuru oluşturma, onay kaydı, mükerrer kontrol ve otomatik atama tek transaction içinde tamamlanır.
- Manuel atama değişikliği assignment history ve audit log ile birlikte atomik kaydedilir.
- Export request logu, job durumu ve dosya metadata kaydı tutarlı güncellenir.

## Ücretsiz/Minimum Maliyetli Development Bağımlılıkları

- PostgreSQL: Docker Compose.
- Redis: Docker Compose.
- Object storage: MinIO.
- CAPTCHA: Cloudflare Turnstile free tier.
- Edge/WAF: Cloudflare free tier.
- Monitoring başlangıç: container health checks + structured logs; production’da OpenTelemetry collector eklenir.
