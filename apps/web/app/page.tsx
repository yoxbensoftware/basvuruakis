export default function HomePage() {
  return (
    <>
      <section className="hero product-hero">
        <div className="hero-copy">
          <p className="eyebrow">KVKK uyumlu başvuru operasyonu</p>
          <h1>BaşvuruAkış</h1>
          <p className="lead">
            Telefon doğrulama, mükerrer kontrol, otomatik temsilcilik atama, permission bazlı yönetim ve denetim kayıtları
            tek akışta çalışır.
          </p>
          <div className="hero-actions">
            <a className="button" href="/basvuru">Başvuru aç</a>
            <a className="button secondary" href="/admin">Admin paneli</a>
          </div>
        </div>
        <aside className="ops-board" aria-label="Canlı sistem özeti">
          <div className="ops-board-header">
            <span className="badge success">Render staging canlı</span>
            <span className="muted">15 Temmuz 2026</span>
          </div>
          <div className="ops-list">
            <div><strong>API</strong><span>/health/ready 200</span></div>
            <div><strong>Web</strong><span>/, /basvuru, /admin 200</span></div>
            <div><strong>Full smoke</strong><span>OTP, başvuru, liste, CSV export</span></div>
            <div><strong>Kalite kapısı</strong><span>35/35 API test + web build</span></div>
          </div>
        </aside>
      </section>

      <section className="section section-tight">
        <div className="section-title">
          <p className="eyebrow">Platform kapsamı</p>
          <h2>Günlük operasyona hazır modüller</h2>
        </div>
        <div className="feature-grid">
          <article className="feature-card">
            <h3>Güvenli başvuru</h3>
            <p>CAPTCHA, OTP, idempotency key, TCKN algoritma kontrolü ve mükerrer başvuru engeli.</p>
          </article>
          <article className="feature-card">
            <h3>Otomatik atama</h3>
            <p>Mahalle, ilçe, il, bölge ve varsayılan merkez önceliğiyle deterministik temsilcilik ataması.</p>
          </article>
          <article className="feature-card">
            <h3>Admin operasyonu</h3>
            <p>JWT oturum, refresh rotation, permission kontrolü, maskeli liste, detay audit’i ve export.</p>
          </article>
        </div>
      </section>

      <section id="kvkk" className="section info-band">
        <div>
          <p className="eyebrow">Veri güvenliği</p>
          <h2>KVKK teknik sınırı</h2>
        </div>
        <p>
          TCKN, telefon, e-posta ve adres API tarafında AES-256-GCM ile şifrelenir. Arama ve mükerrer kontrol için ayrı
          HMAC-SHA-256 hash alanları tutulur. Nihai hukuki metinler veri sorumlusu ve hukuk danışmanı tarafından onaylanmalıdır.
        </p>
      </section>

      <section id="iletisim" className="section section-tight">
        <div className="section-title">
          <p className="eyebrow">Production geçişi</p>
          <h2>Dış operasyon bağımlılıkları</h2>
        </div>
        <div className="check-grid">
          <span>Gerçek SMS provider</span>
          <span>Cloudflare Turnstile</span>
          <span>WAF / TLS / domain</span>
          <span>Secret manager</span>
          <span>Backup / PITR</span>
          <span>Hukuk onayı</span>
        </div>
      </section>
    </>
  );
}
