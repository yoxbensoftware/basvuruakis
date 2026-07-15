import { AdminPanel } from "../ui/AdminPanel";

export default function AdminPage() {
  return (
    <section className="section page-shell admin-page">
      <div className="page-heading">
        <div>
          <p className="eyebrow">Admin operasyonu</p>
          <h1>Yönetim paneli</h1>
        </div>
        <div className="page-summary">
          Dashboard, liste, detay, atama, log ve export akışları permission bazlı çalışır.
        </div>
      </div>
      <AdminPanel />
    </section>
  );
}
