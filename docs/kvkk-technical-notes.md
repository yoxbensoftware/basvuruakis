# KVKK Teknik Notları

## Veri Minimizasyonu

Başvuru formu gereksinim dokümanındaki zorunlu alanlarla sınırlıdır: ad, soyad, TCKN, telefon, e-posta, il/ilçe/mahalle, açık adres ve isteğe bağlı posta kodu.

## Hassas Alan Saklama

- TCKN: AES-256-GCM şifreli değer + HMAC-SHA-256 lookup hash.
- Telefon: AES-256-GCM şifreli değer + HMAC-SHA-256 lookup hash.
- E-posta: AES-256-GCM şifreli değer + HMAC-SHA-256 lookup hash.
- Açık adres: AES-256-GCM şifreli değer.
- Ad/soyad: MVP’de düz alan olarak tutulur; liste ekranlarında maskelenir ve erişim admin permission ile sınırlıdır.

Encryption key ve lookup key ayrı environment secret değerlerinden türetilir.

## Onay Kanıtı

Başvuru sırasında aktif KVKK aydınlatma ve açık rıza metinlerinin tür/sürüm bilgisi, kabul zamanı, IP ve User-Agent ile `ApplicationConsents` içinde saklanır.

## Anonimleştirme

Yetkili admin, veri sahibi talebi kapsamında başvuruyu gerekçeli ve açık onaylı olarak anonimleştirebilir. İşlem:

- Ad/soyadı anonim demo değerlerine çeker.
- TCKN, telefon, e-posta ve açık adres şifreli alanlarını boş değerle değiştirir.
- TCKN/telefon/e-posta lookup hashlerini eski değerle ilişkisiz anonim placeholder değerlerine çevirir.
- Başvuruya bağlı consent IP/User-Agent alanlarını ve telefonla ilişkili OTP kayıtlarını anonimleştirir.
- Durum geçmişi ve audit log üretir.

## Hukuki Sınır

Bu repository teknik uygulama sağlar. Hukuki metin içerikleri, saklama süresi, işleme şartı ve veri sahibi süreçleri veri sorumlusu ve hukuk danışmanı tarafından onaylanmalıdır.
