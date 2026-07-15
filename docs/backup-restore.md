# Backup ve Restore

## Hedef

- Günlük tam PostgreSQL yedeği.
- Mümkünse WAL/PITR.
- Yedeklerin ana hosting ve domainden bağımsız S3 uyumlu storage üzerinde saklanması.
- 7 günlük, 4 haftalık, 6 aylık saklama politikası.

## Local Backup

```powershell
.\infrastructure\backup\backup-postgres.ps1 -Container basvuruakis-postgres-1 -Database basvuruakis -User basvuruakis
```

## Local Restore

```powershell
.\infrastructure\backup\restore-postgres.ps1 -BackupFile .\backups\basvuruakis-YYYYMMDDHHMMSS.dump
```

## Production Gereksinimleri

- Backup dosyaları aktarımda ve saklamada şifrelenmelidir.
- Restore testi aylık olarak temiz ortamda çalıştırılmalıdır.
- Backup başarısızlığı alarm üretmelidir.
- RPO hedefi en fazla 15 dakika, RTO hedefi en fazla 4 saattir. Bu hedefler altyapı ve WAL/PITR konfigürasyonu ile doğrulanmalıdır.
