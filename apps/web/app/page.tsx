export default function HomePage() {
  return (
    <>
      <section className="hero">
        <div>
          <p className="status">KVKK uyumlu başvuru, OTP doğrulama ve otomatik yönlendirme</p>
          <h1>Başvuruları güvenli topla, doğru temsilciliğe yönlendir.</h1>
          <p className="lead">
            BaşvuruAkış; kurumsal içerik yayını, telefon doğrulama, mükerrer kontrol, alan şifreleme,
            permission kontrollü admin paneli ve audit logları tek platformda sunar.
          </p>
          <div className="actions">
            <a className="button" href="/basvuru">Başvuru yap</a>
            <a className="button secondary" href="/admin">Yönetim paneli</a>
          </div>
        </div>
        <aside className="card" aria-label="Platform metrikleri">
          <div className="grid">
            <div className="metric"><strong>OTP</strong><span>3 dk geçerli</span></div>
            <div className="metric"><strong>RBAC</strong><span>Permission bazlı</span></div>
            <div className="metric"><strong>KVKK</strong><span>Versiyonlu onay</span></div>
          </div>
          <p className="lead">
            Demo ortamı development fake SMS/CAPTCHA provider kullanır. Production’da gerçek secret ve provider
            konfigürasyonu olmadan bypass açılmaz.
          </p>
        </aside>
      </section>

      <section className="section">
        <h2>Sunum kapsamı</h2>
        <div className="grid">
          <article className="card">
            <h3>Güvenli başvuru</h3>
            <p>CAPTCHA, OTP, idempotency key, TCKN algoritma kontrolü ve mükerrer başvuru engeli.</p>
          </article>
          <article className="card">
            <h3>Otomatik atama</h3>
            <p>Mahalle, ilçe, il, bölge ve varsayılan merkez önceliğiyle deterministik temsilcilik ataması.</p>
          </article>
          <article className="card">
            <h3>Admin operasyonu</h3>
            <p>JWT oturum, refresh rotation, permission kontrolü, maskeli liste, detay audit’i ve export.</p>
          </article>
        </div>
      </section>

      <section id="kvkk" className="section">
        <h2>KVKK teknik notu</h2>
        <p className="lead">
          TCKN, telefon, e-posta ve adres API tarafında AES-256-GCM ile şifrelenir. Arama ve mükerrer kontrol için
          ayrı HMAC-SHA-256 hash alanları tutulur. Hukuki metinlerin nihai içeriği veri sorumlusu ve hukuk danışmanı
          tarafından onaylanmalıdır.
        </p>
      </section>

      <section id="iletisim" className="section">
        <h2>İletişim</h2>
        <p className="lead">Kurumsal iletişim içeriği yönetim panelindeki içerik modülü üzerinden güncellenir.</p>
      </section>
    </>
  );
}
