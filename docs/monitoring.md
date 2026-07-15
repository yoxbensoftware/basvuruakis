# Monitoring

## Health

- `/health/live`: process ayakta mı.
- `/health/ready`: veritabanı erişilebilir mi.

## İzlenecek Metrikler

- API latency p50/p95/p99.
- HTTP 4xx/5xx oranı.
- OTP request/verify oranı ve hata sayısı.
- Başvuru oluşturma başarı/hata oranı.
- Duplicate application reject sayısı.
- Admin başarısız login ve lockout sayısı.
- Export sayısı ve dosya boyutu.
- PostgreSQL bağlantı, disk, CPU, RAM.
- Redis bellek ve bağlantı.
- Backup başarı/başarısızlık durumu.

## Alarm Başlangıç Eşikleri

- 5 dakika boyunca 5xx oranı %1 üzeri.
- Başvuru endpoint p95 2 saniye üzeri.
- Başarısız login ani artışı.
- Backup job başarısızlığı.
- PostgreSQL disk kullanımının %80 üzeri.
