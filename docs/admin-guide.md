# Admin Kullanım Kılavuzu

## Giriş

Development demo hesabı:

- E-posta: `admin@basvuruakis.local`
- Parola: `ChangeMe!12345`

Production’da ilk admin yalnızca `AdminBootstrap__Email`, `AdminBootstrap__Password` ve Base32 `AdminBootstrap__MfaSecret` environment değerleriyle oluşturulur. Veritabanında admin yoksa bu değerlerden biri eksik olduğunda API başlamaz; admin hesaplarından herhangi birinde MFA kapalıysa production başlangıcı durdurulur.

## Yetkiler

Uygulama permission bazlı çalışır:

- `applications.read`: maskeli başvuru listesi.
- `applications.detail.read`: hassas detay görüntüleme ve audit.
- `applications.assign`: manuel temsilcilik atama.
- `applications.anonymize`: KVKK veri sahibi talebi kapsamında kişisel verileri anonimleştirme.
- `applications.export`: CSV/XLSX dışa aktarma.
- `dashboard.read`: dashboard metrikleri.
- `audit.read`: denetim kayıtları.
- `content.manage`, `legal-text.manage`, `system.manage`: yönetim modülleri.

## Başvuru Operasyonu

1. Başvuru listesinde sonuçlar maskeli görüntülenir.
2. Detay ekranı ayrı permission gerektirir ve her görüntüleme audit log üretir.
3. Manuel atama yapılırsa eski otomatik atama korunur, yeni atama history olarak eklenir.
4. Export işlemi ayrı permission gerektirir ve export log üretir.
5. Anonimleştirme işlemi gerekçe ister, başvuru durumunu `Anonymized` yapar ve audit log üretir.

Demo admin panelinde bu akışlar dashboard, liste, detay, manuel yönlendirme, KVKK anonimleştirme, son audit/security log tabloları ve CSV export butonlarıyla uçtan uca denenebilir.

## Güvenlik Beklentileri

- Production admin hesaplarında MFA zorunludur; giriş formunda TOTP/MFA kodu girilmelidir.
- Paylaşımlı admin hesabı kullanılmamalıdır.
- Export dosyaları kısa ömürlü storage linkleriyle sunulmalıdır. Mevcut MVP küçük veri setinde dosyayı senkron üretir.
