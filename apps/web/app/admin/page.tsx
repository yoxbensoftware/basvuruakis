import { AdminPanel } from "../ui/AdminPanel";

export default function AdminPage() {
  return (
    <section className="section">
      <h1>Yönetim Paneli</h1>
      <p className="lead">Yetkili kullanıcılar için güvenli başvuru yönetimi.</p>
      <AdminPanel />
    </section>
  );
}
