"use client";

import { FormEvent, useState } from "react";
import { apiFetch } from "./api";

type LoginResponse = {
  accessToken: string;
  refreshToken: string;
  permissions: string[];
};

type DashboardResponse = {
  total: number;
  today: number;
  last7Days: number;
  last30Days: number;
  verified: number;
  unverified: number;
  unassigned: number;
  byProvince: { label: string; count: number }[];
};

type PagedApplications = {
  items: ApplicationListItem[];
  total: number;
  page: number;
  pageSize: number;
};

type ApplicationListItem = {
  id: string;
  referenceNumber: string;
  fullNameMasked: string;
  nationalIdMasked: string;
  phoneMasked: string;
  status: string;
  createdAt: string;
};

export function AdminPanel() {
  const [email, setEmail] = useState("admin@basvuruakis.local");
  const [password, setPassword] = useState("ChangeMe!12345");
  const [token, setToken] = useState<string | null>(null);
  const [dashboard, setDashboard] = useState<DashboardResponse | null>(null);
  const [applications, setApplications] = useState<PagedApplications | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function login(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const result = await apiFetch<LoginResponse>("/api/admin/auth/login", {
        method: "POST",
        body: JSON.stringify({ email, password, totpCode: null })
      });
      setToken(result.accessToken);
      await loadAdminData(result.accessToken);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Giriş başarısız.");
    } finally {
      setLoading(false);
    }
  }

  async function loadAdminData(accessToken = token) {
    if (!accessToken) {
      return;
    }
    const authHeaders = { Authorization: `Bearer ${accessToken}` };
    const [dashboardResult, applicationsResult] = await Promise.all([
      apiFetch<DashboardResponse>("/api/admin/dashboard", { headers: authHeaders }),
      apiFetch<PagedApplications>("/api/admin/applications?page=1&pageSize=20&sort=createdAt&desc=true", { headers: authHeaders })
    ]);
    setDashboard(dashboardResult);
    setApplications(applicationsResult);
  }

  return (
    <div className="form-card">
      {error && <div className="status error" role="alert">{error}</div>}
      {!token && (
        <form onSubmit={login} className="form-grid">
          <div className="field">
            <label htmlFor="admin-email">E-posta</label>
            <input id="admin-email" value={email} onChange={(event) => setEmail(event.target.value)} />
          </div>
          <div className="field">
            <label htmlFor="admin-password">Parola</label>
            <input id="admin-password" type="password" value={password} onChange={(event) => setPassword(event.target.value)} />
          </div>
          <div className="field full">
            <button type="submit" disabled={loading}>Giriş yap</button>
          </div>
        </form>
      )}

      {token && (
        <>
          <div className="actions">
            <button type="button" onClick={() => loadAdminData()} disabled={loading}>Yenile</button>
            <button type="button" className="secondary" onClick={() => setToken(null)}>Çıkış</button>
          </div>

          {dashboard && (
            <section aria-label="Dashboard">
              <h2>Dashboard</h2>
              <div className="grid">
                <Metric label="Toplam" value={dashboard.total} />
                <Metric label="Bugün" value={dashboard.today} />
                <Metric label="Son 7 gün" value={dashboard.last7Days} />
                <Metric label="Son 30 gün" value={dashboard.last30Days} />
                <Metric label="Doğrulanmış" value={dashboard.verified} />
                <Metric label="Atanamayan" value={dashboard.unassigned} />
              </div>
            </section>
          )}

          <section>
            <h2>Başvurular</h2>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Referans</th>
                    <th>Ad Soyad</th>
                    <th>TCKN</th>
                    <th>Telefon</th>
                    <th>Durum</th>
                    <th>Tarih</th>
                  </tr>
                </thead>
                <tbody>
                  {applications?.items.map((item) => (
                    <tr key={item.id}>
                      <td>{item.referenceNumber}</td>
                      <td>{item.fullNameMasked}</td>
                      <td>{item.nationalIdMasked}</td>
                      <td>{item.phoneMasked}</td>
                      <td>{item.status}</td>
                      <td>{new Date(item.createdAt).toLocaleString("tr-TR")}</td>
                    </tr>
                  ))}
                  {applications?.items.length === 0 && (
                    <tr>
                      <td colSpan={6}>Henüz başvuru yok.</td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </section>
        </>
      )}
    </div>
  );
}

function Metric({ label, value }: { label: string; value: number }) {
  return (
    <div className="metric">
      <strong>{value}</strong>
      <span>{label}</span>
    </div>
  );
}
