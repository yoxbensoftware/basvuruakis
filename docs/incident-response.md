# Incident Response

## Veri İhlali Şüphesi

1. Etkilenen sistemi izole et.
2. Audit/security logları ve correlation bilgilerini koru.
3. Secret rotasyonu gerekip gerekmediğini değerlendir.
4. Etkilenen veri kategorilerini ve zaman aralığını belirle.
5. Veri sorumlusu ve hukuk danışmanı ile bildirim yükümlülüğünü değerlendir.
6. Kök neden düzeltmesini ve tekrar önleme aksiyonlarını kayıt altına al.

## SMS/OTP Abuse

1. Telefon/IP/device bazlı metrikleri incele.
2. Cloudflare rate limit ve challenge seviyesini artır.
3. SMS provider maliyet limitlerini kontrol et.
4. Gerekirse ilgili prefix/IP blokları geçici sınırla.

## Sunucu Kaybı

1. Son başarılı backup ve WAL/PITR durumunu doğrula.
2. Temiz ortamda PostgreSQL restore çalıştır.
3. Secret store değerlerini yeni ortama bağla.
4. DNS/WAF origin yönlendirmesini güncelle.
5. Smoke test sonrası trafiği aç.
