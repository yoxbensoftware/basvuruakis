# Troubleshooting

## API Production Başlamıyor

- `Security__EncryptionKey`, `Security__LookupKey`, `Jwt__SigningKey` eksik olabilir.
- İlk admin yoksa `AdminBootstrap__Email`, `AdminBootstrap__Password`, `AdminBootstrap__MfaSecret` eksik veya geçersiz olabilir.
- Mevcut admin kullanıcılarından biri MFA kapalı veya geçersiz Base32 MFA secret ile kayıtlı olabilir.
- `ConnectionStrings__Postgres` yanlış olabilir.
- PostgreSQL health check başarısız olabilir.

## OTP Çalışmıyor

- Development ortamında `captchaToken` boş veya `fail` ise reddedilir.
- Production’da `Captcha__TurnstileSecret`, web build için `NEXT_PUBLIC_CAPTCHA_PROVIDER=turnstile`, `NEXT_PUBLIC_TURNSTILE_SITE_KEY`, `Sms__Provider=http-json`, `Sms__Endpoint`, `Sms__ApiKey` gerekir.
- SMS provider 2xx dönmezse OTP isteği başarısız olur; provider dashboard kota, sender ve teslimat logları kontrol edilmelidir.
- Aynı telefon için 60 saniye resend cooldown vardır.
- Aynı IP ve tarayıcı cihaz kimliği için saatlik OTP rate limit vardır.

## Başvuru Reddediliyor

- TCKN algoritmik doğrulama başarısız olabilir.
- KVKK/açık rıza checkbox’ları gönderilmemiş olabilir.
- Verification token daha önce kullanılmış veya süresi dolmuş olabilir.
- Aynı TCKN veya telefonla başvuru daha önce alınmış olabilir; kullanıcıya kayıt varlığı açıklanmaz.

## Admin Liste 403/401

- Token süresi dolmuş olabilir.
- Refresh token rotation sonrası eski refresh token geçersizdir.
- Kullanıcının ilgili permission’ı olmayabilir.
