"use client";

import { FormEvent, useState } from "react";
import { apiBaseUrl, apiFetch } from "./api";

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
  provinceId: number;
  districtId: number;
  neighborhoodId: number;
  status: string;
  createdAt: string;
};

type ApplicationDetail = {
  id: string;
  referenceNumber: string;
  firstName: string;
  lastName: string;
  nationalId: string;
  phone: string;
  email: string;
  address: string;
  provinceId: number;
  districtId: number;
  neighborhoodId: number;
  postalCode?: string | null;
  status: string;
  createdAt: string;
};

type ApplicationAnonymizedResponse = {
  id: string;
  status: string;
  anonymizedAt: string;
};

export function AdminPanel() {
  const [email, setEmail] = useState("admin@basvuruakis.local");
  const [password, setPassword] = useState("ChangeMe!12345");
  const [token, setToken] = useState<string | null>(null);
  const [dashboard, setDashboard] = useState<DashboardResponse | null>(null);
  const [applications, setApplications] = useState<PagedApplications | null>(null);
  const [detail, setDetail] = useState<ApplicationDetail | null>(null);
  const [assignmentOfficeId, setAssignmentOfficeId] = useState(2);
  const [assignmentReason, setAssignmentReason] = useState("Demo manuel yönlendirme");
  const [anonymizeReason, setAnonymizeReason] = useState("KVKK veri sahibi talebi");
  const [error, setError] = useState<string | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  function authHeaders(accessToken = token): Record<string, string> {
    return accessToken ? { Authorization: `Bearer ${accessToken}` } : {};
  }

  async function login(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setStatus(null);
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
    const [dashboardResult, applicationsResult] = await Promise.all([
      apiFetch<DashboardResponse>("/api/admin/dashboard", { headers: authHeaders(accessToken) }),
      apiFetch<PagedApplications>("/api/admin/applications?page=1&pageSize=20&sort=createdAt&desc=true", { headers: authHeaders(accessToken) })
    ]);
    setDashboard(dashboardResult);
    setApplications(applicationsResult);
  }

  async function refreshAdminData() {
    setError(null);
    setStatus(null);
    setLoading(true);
    try {
      await loadAdminData();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Yönetim verileri yenilenemedi.");
    } finally {
      setLoading(false);
    }
  }

  function logout() {
    setToken(null);
    setDetail(null);
    setDashboard(null);
    setApplications(null);
    setStatus(null);
    setError(null);
  }

  async function loadDetail(applicationId: string) {
    setError(null);
    setStatus(null);
    setLoading(true);
    try {
      const result = await apiFetch<ApplicationDetail>(`/api/admin/applications/${applicationId}`, {
        headers: authHeaders()
      });
      setDetail(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Başvuru detayı alınamadı.");
    } finally {
      setLoading(false);
    }
  }

  async function assignSelectedApplication(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token || !detail) {
      return;
    }

    setError(null);
    setStatus(null);
    setLoading(true);
    try {
      const response = await fetch(`${apiBaseUrl}/api/admin/applications/${detail.id}/assignment`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...authHeaders()
        },
        body: JSON.stringify({
          representativeOfficeId: assignmentOfficeId,
          reason: assignmentReason
        })
      });

      if (!response.ok) {
        throw new Error(await readErrorMessage(response));
      }

      setStatus("Başvuru manuel olarak yönlendirildi.");
      await loadAdminData(token);
      await loadDetail(detail.id);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Manuel yönlendirme başarısız.");
    } finally {
      setLoading(false);
    }
  }

  async function anonymizeSelectedApplication(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token || !detail) {
      return;
    }

    setError(null);
    setStatus(null);
    setLoading(true);
    try {
      const result = await apiFetch<ApplicationAnonymizedResponse>(`/api/admin/applications/${detail.id}/anonymize`, {
        method: "POST",
        headers: authHeaders(),
        body: JSON.stringify({ reason: anonymizeReason })
      });
      setStatus(`Başvuru anonimleştirildi. Durum: ${result.status}`);
      await loadAdminData(token);
      await loadDetail(detail.id);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Anonimleştirme başarısız.");
    } finally {
      setLoading(false);
    }
  }

  async function downloadCsvExport() {
    if (!token) {
      return;
    }

    setError(null);
    setStatus(null);
    setLoading(true);
    try {
      const response = await fetch(`${apiBaseUrl}/api/admin/exports`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...authHeaders()
        },
        body: JSON.stringify({
          format: 0,
          filters: {
            page: null,
            pageSize: null,
            sort: null,
            desc: null,
            status: null,
            provinceId: null,
            districtId: null,
            neighborhoodId: null,
            from: null,
            to: null
          }
        })
      });

      if (!response.ok) {
        throw new Error(await readErrorMessage(response));
      }

      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = resolveFileName(response.headers.get("content-disposition"));
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
      setStatus("CSV export oluşturuldu.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Export indirilemedi.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="form-card">
      {error && <div className="status error" role="alert">{error}</div>}
      {status && <div className="status success" role="status">{status}</div>}
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
            <button type="button" onClick={refreshAdminData} disabled={loading}>Yenile</button>
            <button type="button" className="secondary" onClick={downloadCsvExport} disabled={loading}>CSV export indir</button>
            <button type="button" className="secondary" onClick={logout}>Çıkış</button>
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
                    <th>Aksiyon</th>
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
                      <td>
                        <button type="button" className="secondary" onClick={() => loadDetail(item.id)} disabled={loading}>
                          Detay
                        </button>
                      </td>
                    </tr>
                  ))}
                  {applications?.items.length === 0 && (
                    <tr>
                      <td colSpan={7}>Henüz başvuru yok.</td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </section>

          {detail && (
            <section className="detail-card" aria-label="Başvuru detayı">
              <h2>Başvuru Detayı</h2>
              <dl className="detail-grid">
                <DetailItem label="Referans" value={detail.referenceNumber} />
                <DetailItem label="Ad Soyad" value={`${detail.firstName} ${detail.lastName}`} />
                <DetailItem label="TCKN" value={detail.nationalId} />
                <DetailItem label="Telefon" value={detail.phone} />
                <DetailItem label="E-posta" value={detail.email} />
                <DetailItem label="Durum" value={detail.status} />
                <DetailItem label="Lokasyon" value={`${detail.provinceId}/${detail.districtId}/${detail.neighborhoodId}`} />
                <DetailItem label="Posta Kodu" value={detail.postalCode ?? "-"} />
                <DetailItem label="Adres" value={detail.address} />
              </dl>

              <form className="form-grid compact" onSubmit={assignSelectedApplication}>
                <div className="field">
                  <label htmlFor="assignment-office">Temsilcilik</label>
                  <select
                    id="assignment-office"
                    value={assignmentOfficeId}
                    onChange={(event) => setAssignmentOfficeId(Number(event.target.value))}
                  >
                    <option value={1}>Genel Merkez</option>
                    <option value={2}>Kadıköy Temsilciliği</option>
                  </select>
                </div>
                <div className="field">
                  <label htmlFor="assignment-reason">Gerekçe</label>
                  <input
                    id="assignment-reason"
                    value={assignmentReason}
                    onChange={(event) => setAssignmentReason(event.target.value)}
                    required
                  />
                </div>
                <div className="field full">
                  <button type="submit" disabled={loading || assignmentReason.trim().length < 3}>
                    Manuel yönlendir
                  </button>
                </div>
              </form>

              <form className="form-grid compact" onSubmit={anonymizeSelectedApplication}>
                <div className="field full">
                  <label htmlFor="anonymize-reason">Anonimleştirme gerekçesi</label>
                  <input
                    id="anonymize-reason"
                    value={anonymizeReason}
                    onChange={(event) => setAnonymizeReason(event.target.value)}
                    required
                  />
                </div>
                <div className="field full">
                  <button type="submit" className="secondary" disabled={loading || detail.status === "Anonymized" || anonymizeReason.trim().length < 5}>
                    KVKK anonimleştir
                  </button>
                </div>
              </form>
            </section>
          )}
        </>
      )}
    </div>
  );
}

async function readErrorMessage(response: Response) {
  try {
    const body = await response.json() as { message?: string; title?: string };
    return body.message ?? body.title ?? `İstek başarısız: ${response.status}`;
  } catch {
    return `İstek başarısız: ${response.status}`;
  }
}

function resolveFileName(contentDisposition: string | null) {
  const match = contentDisposition?.match(/filename\*?=(?:UTF-8''|")?([^";]+)/i);
  return match?.[1] ?? "basvuru-export.csv";
}

function Metric({ label, value }: { label: string; value: number }) {
  return (
    <div className="metric">
      <strong>{value}</strong>
      <span>{label}</span>
    </div>
  );
}

function DetailItem({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}
