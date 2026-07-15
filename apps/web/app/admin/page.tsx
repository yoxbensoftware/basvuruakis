import { AdminPanel } from "../ui/AdminPanel";

export default function AdminPage() {
  return (
    <section className="section">
      <h1>Yönetim Paneli</h1>
      <p className="lead">Demo giriş: admin@basvuruakis.local / ChangeMe!12345</p>
      <AdminPanel />
    </section>
  );
}
