# Backup ve Restore

## Hedef

- Günlük tam PostgreSQL yedeği.
- Mümkünse WAL/PITR.
- Yedeklerin ana hosting ve domainden bağımsız S3 uyumlu storage üzerinde saklanması.
- 7 günlük, 4 haftalık, 6 aylık saklama politikası.

## Local Backup

```powershell
.\infrastructure\backup\backup-postgres.ps1 -Container basvuruakis-postgres-1 -Database basvuruakis -User basvuruakis -OutputDirectory .\backups -RetentionDays 7
```

Script PostgreSQL custom-format dump üretir, aynı dizinde SHA-256 checksum (`.sha256`) ve metadata (`.json`) dosyası oluşturur. `RetentionDays` değerinden eski `basvuruakis-*.dump*` dosyaları aynı backup dizini içinde temizlenir.

## Local Restore

```powershell
.\infrastructure\backup\restore-postgres.ps1 -BackupFile .\backups\basvuruakis-YYYYMMDDHHMMSS.dump -Force
```

Restore işlemi yıkıcıdır; `-Force` olmadan çalışmaz. Yanında `.sha256` dosyası varsa restore öncesi checksum doğrulanır. Hedef veritabanı, kullanıcı ve container değerleri production ortamında açıkça verilmelidir.

## Restore Smoke Test

Her production release öncesi veya en az aylık olarak temiz PostgreSQL ortamında şu akış doğrulanmalıdır:

1. Geçici veritabanına bilinen bir kayıt yaz.
2. `backup-postgres.ps1` ile yedek al.
3. Kaydı veya tabloyu sil.
4. `restore-postgres.ps1 -Force` ile geri yükle.
5. Bilinen kaydın geri geldiğini ve checksum dosyasının eşleştiğini doğrula.

## Production Gereksinimleri

- Backup dosyaları aktarımda ve saklamada şifrelenmelidir; S3/object storage tarafında server-side encryption etkin olmalıdır.
- Backup çıktı dizini geçici olmalı, job sonunda dump + checksum + metadata bağımsız S3 uyumlu hedefe kopyalanmalıdır.
- Restore testi aylık olarak temiz ortamda çalıştırılmalıdır.
- Backup başarısızlığı alarm üretmelidir.
- RPO hedefi en fazla 15 dakika, RTO hedefi en fazla 4 saattir. Bu hedefler altyapı ve WAL/PITR konfigürasyonu ile doğrulanmalıdır.
