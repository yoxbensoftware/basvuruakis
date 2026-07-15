# Troubleshooting

## API Production Başlamıyor

- `Security__EncryptionKey`, `Security__LookupKey`, `Jwt__SigningKey` eksik olabilir.
- `ConnectionStrings__Postgres` yanlış olabilir.
- PostgreSQL health check başarısız olabilir.

## OTP Çalışmıyor

- Development ortamında `captchaToken` boş veya `fail` ise reddedilir.
- Production’da `Captcha__TurnstileSecret` ve SMS provider credential gerekir.
- Aynı telefon için 60 saniye resend cooldown vardır.

## Başvuru Reddediliyor

- TCKN algoritmik doğrulama başarısız olabilir.
- KVKK/açık rıza checkbox’ları gönderilmemiş olabilir.
- Verification token daha önce kullanılmış veya süresi dolmuş olabilir.
- Aynı TCKN veya telefonla başvuru daha önce alınmış olabilir; kullanıcıya kayıt varlığı açıklanmaz.

## Admin Liste 403/401

- Token süresi dolmuş olabilir.
- Refresh token rotation sonrası eski refresh token geçersizdir.
- Kullanıcının ilgili permission’ı olmayabilir.
