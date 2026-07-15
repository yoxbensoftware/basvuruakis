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

type PagedAuditLogs = {
  items: AuditLogItem[];
  total: number;
  page: number;
  pageSize: number;
};

type PagedSecurityLogs = {
  items: SecurityLogItem[];
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

type AuditLogItem = {
  id: string;
  actorUserId?: string | null;
  action: string;
  entityType: string;
  entityId?: string | null;
  metadataJson: string;
  createdAt: string;
};

type SecurityLogItem = {
  id: string;
  eventType: string;
  actorUserId?: string | null;
  ipAddress: string;
  userAgent: string;
  metadataJson: string;
  createdAt: string;
};

export function AdminPanel() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [totpCode, setTotpCode] = useState("");
  const [token, setToken] = useState<string | null>(null);
  const [permissions, setPermissions] = useState<string[]>([]);
  const [dashboard, setDashboard] = useState<DashboardResponse | null>(null);
  const [applications, setApplications] = useState<PagedApplications | null>(null);
  const [auditLogs, setAuditLogs] = useState<PagedAuditLogs | null>(null);
  const [securityLogs, setSecurityLogs] = useState<PagedSecurityLogs | null>(null);
  const [detail, setDetail] = useState<ApplicationDetail | null>(null);
  const [assignmentOfficeId, setAssignmentOfficeId] = useState(2);
  const [assignmentReason, setAssignmentReason] = useState("Operasyonel manuel yönlendirme");
  const [anonymizeReason, setAnonymizeReason] = useState("KVKK veri sahibi talebi");
  const [anonymizeConfirmed, setAnonymizeConfirmed] = useState(false);
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
        body: JSON.stringify({ email, password, totpCode: totpCode.trim() || null })
      });
      setToken(result.accessToken);
      setPermissions(result.permissions);
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
    const [dashboardResult, applicationsResult, auditResult, securityResult] = await Promise.all([
      apiFetch<DashboardResponse>("/api/admin/dashboard", { headers: authHeaders(accessToken) }),
      apiFetch<PagedApplications>("/api/admin/applications?page=1&pageSize=20&sort=createdAt&desc=true", { headers: authHeaders(accessToken) }),
      apiFetch<PagedAuditLogs>("/api/admin/audit-logs?page=1&pageSize=10", { headers: authHeaders(accessToken) }),
      apiFetch<PagedSecurityLogs>("/api/admin/security-logs?page=1&pageSize=10", { headers: authHeaders(accessToken) })
    ]);
    setDashboard(dashboardResult);
    setApplications(applicationsResult);
    setAuditLogs(auditResult);
    setSecurityLogs(securityResult);
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
    setAuditLogs(null);
    setSecurityLogs(null);
    setPermissions([]);
    setAnonymizeConfirmed(false);
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
      setAnonymizeConfirmed(false);
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
        body: JSON.stringify({ reason: anonymizeReason, confirmed: anonymizeConfirmed })
      });
      setStatus(`Başvuru anonimleştirildi. Durum: ${result.status}`);
      setAnonymizeConfirmed(false);
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
    <div className="admin-panel">
      {(error || status) && (
        <div className="message-stack">
          {error && <div className="status error" role="alert">{error}</div>}
          {status && <div className="status success" role="status">{status}</div>}
        </div>
      )}

      {!token && (
        <form onSubmit={login} className="login-panel">
          <div className="login-copy">
            <p className="eyebrow">Yönetim oturumu</p>
            <h2>Başvuru operasyonu</h2>
            <p className="muted">Liste, detay, manuel atama, KVKK anonimleştirme ve export tek panelde çalışır.</p>
          </div>
          <div className="login-fields">
            <div className="field">
              <label htmlFor="admin-email">E-posta</label>
              <input id="admin-email" value={email} onChange={(event) => setEmail(event.target.value)} autoComplete="username" />
            </div>
            <div className="field">
              <label htmlFor="admin-password">Parola</label>
              <input id="admin-password" type="password" value={password} onChange={(event) => setPassword(event.target.value)} autoComplete="current-password" />
            </div>
            <div className="field">
              <label htmlFor="admin-totp">MFA kodu</label>
              <input
                id="admin-totp"
                value={totpCode}
                onChange={(event) => setTotpCode(event.target.value)}
                inputMode="numeric"
                autoComplete="one-time-code"
              />
            </div>
            <button type="submit" disabled={loading}>Giriş yap</button>
          </div>
        </form>
      )}

      {token && (
        <>
          <div className="admin-toolbar">
            <div>
              <p className="eyebrow">Operasyon konsolu</p>
              <h2>Başvuru yönetimi</h2>
            </div>
            <div className="toolbar-actions">
              <span className="badge">{permissions.length} yetki</span>
              <button type="button" onClick={refreshAdminData} disabled={loading}>Yenile</button>
              <button type="button" className="secondary" onClick={downloadCsvExport} disabled={loading}>CSV indir</button>
              <button type="button" className="ghost" onClick={logout}>Çıkış</button>
            </div>
          </div>

          {dashboard && (
            <section className="dashboard-band" aria-label="Dashboard">
              <div className="metric-grid">
                <Metric label="Toplam" value={dashboard.total} />
                <Metric label="Bugün" value={dashboard.today} />
                <Metric label="Son 7 gün" value={dashboard.last7Days} />
                <Metric label="Son 30 gün" value={dashboard.last30Days} />
                <Metric label="Doğrulanmış" value={dashboard.verified} />
                <Metric label="Atanamayan" value={dashboard.unassigned} />
              </div>
              <div className="distribution-panel">
                <span className="field-label">İl dağılımı</span>
                {dashboard.byProvince.length === 0 ? (
                  <p className="muted">Henüz dağılım yok.</p>
                ) : (
                  dashboard.byProvince.map((item) => (
                    <div className="distribution-row" key={item.label}>
                      <span>{item.label}</span>
                      <strong>{item.count}</strong>
                    </div>
                  ))
                )}
              </div>
            </section>
          )}

          <section className="data-panel">
            <div className="panel-heading">
              <div>
                <h2>Başvurular</h2>
                <p>{applications?.total ?? 0} kayıt içinden son {applications?.items.length ?? 0} başvuru.</p>
              </div>
              <span className="badge">Maskeli liste</span>
            </div>
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
                      <td><StatusPill status={item.status} /></td>
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

          <div className="log-grid">
            <section className="data-panel compact-panel">
              <div className="panel-heading">
                <div>
                  <h2>Denetim</h2>
                  <p>Son denetim kayıtları.</p>
                </div>
              </div>
              <div className="event-list">
                {auditLogs?.items.map((item) => (
                  <div className="event-row" key={item.id}>
                    <strong>{item.action}</strong>
                    <span>{item.entityType}{item.entityId ? ` / ${item.entityId.slice(0, 8)}` : ""}</span>
                    <time>{new Date(item.createdAt).toLocaleString("tr-TR")}</time>
                  </div>
                ))}
                {auditLogs?.items.length === 0 && <p className="muted">Henüz audit kaydı yok.</p>}
              </div>
            </section>

            <section className="data-panel compact-panel">
              <div className="panel-heading">
                <div>
                  <h2>Güvenlik</h2>
                  <p>Son güvenlik kayıtları.</p>
                </div>
              </div>
              <div className="event-list">
                {securityLogs?.items.map((item) => (
                  <div className="event-row" key={item.id}>
                    <strong>{item.eventType}</strong>
                    <span>{item.ipAddress}</span>
                    <time>{new Date(item.createdAt).toLocaleString("tr-TR")}</time>
                  </div>
                ))}
                {securityLogs?.items.length === 0 && <p className="muted">Henüz security kaydı yok.</p>}
              </div>
            </section>
          </div>

          {detail && (
            <section className="detail-card" aria-label="Başvuru detayı">
              <div className="panel-heading">
                <div>
                  <h2>Başvuru detayı</h2>
                  <p>{detail.referenceNumber}</p>
                </div>
                <StatusPill status={detail.status} />
              </div>
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

              <div className="action-forms">
                <form className="form-grid compact" onSubmit={assignSelectedApplication}>
                  <div className="section-heading full">
                    <h3>Manuel yönlendirme</h3>
                    <p>Başvuruyu başka temsilciliğe gerekçeli olarak aktarır.</p>
                  </div>
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

                <form className="form-grid compact danger-zone" onSubmit={anonymizeSelectedApplication}>
                  <div className="section-heading full">
                    <h3>KVKK anonimleştirme</h3>
                    <p>Geri alınamaz işlem için gerekçe ve açık onay gerekir.</p>
                  </div>
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
                    <label className="checkline">
                      <input
                        type="checkbox"
                        checked={anonymizeConfirmed}
                        onChange={(event) => setAnonymizeConfirmed(event.target.checked)}
                        disabled={loading || detail.status === "Anonymized"}
                      />
                      <span>Geri alınamaz anonimleştirme işlemini onaylıyorum.</span>
                    </label>
                  </div>
                  <div className="field full">
                    <button type="submit" className="secondary danger-action" disabled={loading || detail.status === "Anonymized" || anonymizeReason.trim().length < 5 || !anonymizeConfirmed}>
                      KVKK anonimleştir
                    </button>
                  </div>
                </form>
              </div>
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

function StatusPill({ status }: { status: string }) {
  return (
    <span className={`status-pill status-${status.toLowerCase()}`}>
      {status}
    </span>
  );
}
