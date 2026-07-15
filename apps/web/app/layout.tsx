import type { Metadata } from "next";
import Link from "next/link";
import "./globals.css";
import { CookieConsent } from "./ui/CookieConsent";

export const metadata: Metadata = {
  title: "BaşvuruAkış",
  description: "KVKK uyumlu başvuru, OTP doğrulama ve otomatik temsilcilik atama platformu.",
  metadataBase: new URL("http://localhost:3000"),
  openGraph: {
    title: "BaşvuruAkış",
    description: "Güvenli başvuru toplama ve otomatik yönlendirme.",
    type: "website"
  }
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="tr">
      <body>
        <a className="skip-link" href="#main">İçeriğe geç</a>
        <header className="site-header">
          <nav className="nav" aria-label="Ana menü">
            <Link className="brand" href="/">BaşvuruAkış</Link>
            <div className="nav-links">
              <Link href="/basvuru">Başvuru</Link>
              <Link href="/admin">Yönetim</Link>
              <Link href="/#kvkk">KVKK</Link>
              <Link href="/#iletisim">İletişim</Link>
            </div>
          </nav>
        </header>
        <main id="main">{children}</main>
        <footer className="footer">
          <span>© 2026 BaşvuruAkış</span>
          <span>KVKK teknik şablonları hukuk onayı gerektirir.</span>
        </footer>
        <CookieConsent />
      </body>
    </html>
  );
}
