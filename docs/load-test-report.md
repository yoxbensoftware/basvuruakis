# Load Test Report

## Mevcut Durum

Bu aşamada k6 smoke senaryosu `infrastructure/k6/smoke.js` olarak eklendi. Tam 1.000 eşzamanlı kullanıcı ve dakikada 2.000 başvuru profili, gerçek staging altyapısı ve SMS/CAPTCHA provider limitleri netleştikten sonra koşturulmalıdır.

## Çalıştırma

```powershell
k6 run -e BASE_URL=http://localhost:8080 .\infrastructure\k6\smoke.js
```

Render gibi web ve API ayrı hostlarda çalışıyorsa:

```powershell
k6 run -e WEB_BASE_URL=https://basvuruakis-web.onrender.com -e API_BASE_URL=https://basvuruakis-api.onrender.com .\infrastructure\k6\smoke.js
```

Smoke senaryosu:

- Web ana sayfa ve başvuru sayfasını açar.
- API live/ready health endpointlerini kontrol eder.
- İlk iterasyonda aktif KVKK metinlerini kontrol eder.
- İlk VU'nun ilk iterasyonunda OTP request/verify, başvuru oluşturma, admin login, admin başvuru listesi ve CSV export akışını doğrular.
- Demo ortamında OTP doğrulaması API'nin döndürdüğü `developmentCode` ile yapılır; production benzeri ortamda `OTP_CODE` ve gerekirse `ADMIN_TOTP_CODE` environment değerleri verilebilir.

## Kabul Hedefleri

- Normal yükte başvuru API p95 < 2 saniye.
- Hata oranı < %1.
- Queue ve DB bağlantı metrikleri stabil.
- SMS maliyet limiti ve provider rate limitleri aşılmıyor.

## Bilinen Kısıt

Mevcut local smoke testi kapasite kanıtı değildir; yalnızca deployment’ın temel endpointleri cevapladığını doğrular.
