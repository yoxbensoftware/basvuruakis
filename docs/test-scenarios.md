# BaşvuruAkış Test Senaryoları

Bu senaryolar, PDF gereksinimleri ile geliştirilecek testlerin izlenebilirlik kaydıdır.

## Unit Testler

1. TCKN algoritması geçerli/geçersiz değerleri doğru ayırır.
2. Telefon, e-posta ve TCKN normalize edilir; mükerrer hash aynı normalize değerde deterministiktir.
3. AES-256-GCM şifreleme düz metni saklamaz ve doğru anahtarla çözer.
4. Farklı HMAC anahtarıyla arama hash’i değişir.
5. Maskeleme servisleri TCKN, telefon, e-posta ve ad/soyad için yetkisiz liste çıktısını güvenli üretir.
6. OTP 6 hanelidir, hashlenir, 3 dakika sonunda geçersizdir.
7. OTP en fazla 5 doğrulama denemesi kabul eder.
8. OTP resend cooldown 60 saniye altında tekrar gönderimi reddeder.
9. Verification token tek kullanımlı ve kısa ömürlüdür.
10. Assignment engine önceliği mahalle, ilçe, il, bölge, varsayılan merkez olarak uygular.
11. Kural çakışmalarında deterministik priority ve tarih sıralaması uygulanır.
12. Permission kontrolü role bağlı değil permission bağlı karar verir.
13. CSV/XLSX export hücre temizleyici formula injection payloadlarını güvenli hale getirir.
14. Anonimleştirme kişisel alanları geri döndürülemez temizler, istatistiksel alanları korur.

## Integration Testler

1. Health/readiness/liveness endpointleri doğru durum döner.
2. CAPTCHA doğrulanmadan OTP request reddedilir.
3. OTP request telefon cooldown, IP rate limit ve cihaz rate limit kurallarını uygular.
4. Fake SMS provider yalnızca development/test ortamında çalışır.
5. Doğrulanmamış telefonla başvuru oluşturulamaz.
6. KVKK ve açık rıza onayı olmadan başvuru oluşturulamaz.
7. Aynı TCKN veya telefonla ikinci başvuru genel hata mesajıyla reddedilir.
8. Idempotency key aynı payload için aynı sonucu döner.
9. Başvuru oluşturulurken hassas alanlar veritabanında şifreli ve hashli saklanır.
10. Başvuru oluşturma ile otomatik atama aynı transaction sınırında tamamlanır.
11. Admin login başarısız denemelerde security log üretir ve limit aşımında geçici kilit uygular.
12. Refresh token rotation eski tokenı geçersiz kılar.
13. Başvuru liste endpointi filtre, sorting ve pagination uygular.
14. Liste endpointi hassas alanları maskeli döner.
15. Detay endpointi ayrı permission olmadan erişilemez.
16. Detay görüntüleme audit log üretir ve audit endpointinde listelenir.
17. Manuel temsilcilik değişikliği assignment history ve audit log üretir.
18. Export endpointi permission olmadan erişilemez.
19. Export log yönetici, filtre, format, kayıt sayısı ve durum bilgisini saklar.
20. Güvenlik olayları security log endpointinde permission kontrollü listelenir.
21. LegalText aynı türde tek aktif sürüm kuralını korur.
22. Data subject anonimleştirme endpointi yeniden kimlik doğrulama/onay olmadan çalışmaz.

## E2E / Smoke Senaryoları

1. Public kullanıcı ana sayfadan başvuru formuna gider.
2. Kullanıcı CAPTCHA, OTP ve KVKK onaylarıyla başvuru gönderir.
3. Başvuru doğru temsilciliğe atanır ve kullanıcı güvenli başarı mesajı görür.
4. Admin MFA ile giriş yapar.
5. Admin dashboard toplam, bugün, son 7/30 gün ve atanamayan sayılarını görür.
6. ApplicationManager başvuru listesini filtreler, detay açar ve manuel atama yapar.
7. ContentManager başvuru detayına erişemez.
8. Auditor audit logları salt okunur görüntüler ve export alamaz.
9. CSV export indirilir ve formula injection payloadı çalıştırılabilir formda değildir.
10. Yetkili kullanıcı veri sahibi talebi oluşturur ve anonimleştirme sonrası kişisel alanlar geri dönmez.

## Güvenlik ve Operasyon Kontrolleri

1. Dependency scan kritik/yüksek açık bırakmaz.
2. Secret taraması repository içinde secret bulmaz.
3. Security header, CSP, CORS, secure cookie ve HSTS production profilde doğrulanır.
4. Backup script local dump üretir, şifreler ve restore smoke testi temiz veritabanında geçer.
5. k6 smoke profili public sayfa, OTP, başvuru, admin liste ve export job akışlarını ölçer.
