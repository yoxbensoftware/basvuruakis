# API Dokümantasyonu

OpenAPI endpoint’i development ortamında `/openapi/v1.json` üzerinden yayınlanır.

## Public

- `GET /health/live`: process sağlık kontrolü.
- `GET /health/ready`: veritabanı erişilebilirlik kontrolü.
- `GET /api/content/pages/{slug}`: yayınlanmış içerik sayfası.
- `GET /api/legal-texts/active`: aktif KVKK/açık rıza/çerez metinleri.
- `POST /api/otp/request`: CAPTCHA sonrası OTP üretir.
- `POST /api/otp/verify`: OTP doğrular ve tek kullanımlık verification token döner.
- `POST /api/applications`: KVKK onayı, OTP token, TCKN kontrolü, şifreleme, mükerrer kontrol ve otomatik atama ile başvuru oluşturur.

## Admin

Tüm admin endpointleri JWT Bearer token kullanır.

- `POST /api/admin/auth/login`: admin login.
- `POST /api/admin/auth/refresh`: refresh token rotation.
- `POST /api/admin/auth/logout`: refresh token iptali.
- `GET /api/admin/applications`: permission kontrollü maskeli liste. `status`, `firstName`, `lastName`, `nationalId`, `phone`, `email`, `provinceId`, `districtId`, `neighborhoodId`, `representativeOfficeId`, `isAssigned`, `isPhoneVerified`, `from`, `to`, `page`, `pageSize`, `sort`, `desc` query parametrelerini destekler. TCKN/telefon/e-posta aramaları normalize HMAC lookup hash üzerinden yapılır.
- `GET /api/admin/applications/{id}`: detay görüntüleme; audit log üretir.
- `POST /api/admin/applications/{id}/assignment`: manuel temsilcilik ataması.
- `POST /api/admin/applications/{id}/anonymize`: KVKK veri sahibi talebi kapsamında kişisel alanları anonimleştirir.
- `GET /api/admin/dashboard`: temel metrikler.
- `GET /api/admin/audit-logs`: denetim kayıtlarını permission kontrollü listeler.
- `GET /api/admin/security-logs`: güvenlik olay kayıtlarını permission kontrollü listeler.
- `POST /api/admin/exports`: CSV/XLSX export; liste endpointiyle aynı filtre sözleşmesini kullanır, export log ve injection sanitization uygular.

## Demo Seed

Development/test ortamında demo kullanıcı ve lokasyon seed edilir:

- Admin: `admin@basvuruakis.local`
- Parola: `ChangeMe!12345`
- Lokasyon: İstanbul / Kadıköy / Caferağa

Bu seed production ortamında çalışmaz.
