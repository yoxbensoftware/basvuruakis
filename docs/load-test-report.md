# Load Test Report

## Mevcut Durum

Bu aşamada k6 smoke senaryosu `infrastructure/k6/smoke.js` olarak eklendi. Tam 1.000 eşzamanlı kullanıcı ve dakikada 2.000 başvuru profili, gerçek staging altyapısı ve SMS/CAPTCHA provider limitleri netleştikten sonra koşturulmalıdır.

## Çalıştırma

```powershell
k6 run -e BASE_URL=http://localhost:8080 .\infrastructure\k6\smoke.js
```

## Kabul Hedefleri

- Normal yükte başvuru API p95 < 2 saniye.
- Hata oranı < %1.
- Queue ve DB bağlantı metrikleri stabil.
- SMS maliyet limiti ve provider rate limitleri aşılmıyor.

## Bilinen Kısıt

Mevcut local smoke testi kapasite kanıtı değildir; yalnızca deployment’ın temel endpointleri cevapladığını doğrular.
