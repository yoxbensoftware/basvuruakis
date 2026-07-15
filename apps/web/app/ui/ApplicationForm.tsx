"use client";

import { FormEvent, useEffect, useState } from "react";
import { apiFetch } from "./api";

type OtpRequestResponse = {
  requestId: string;
  expiresAt: string;
  resendAvailableAt: string;
  developmentCode?: string | null;
};

type OtpVerifyResponse = {
  verificationToken: string;
  expiresAt: string;
};

type ApplicationCreatedResponse = {
  id: string;
  referenceNumber: string;
  status: string;
};

type LegalTextResponse = {
  id: string;
  type: "PrivacyNotice" | "ExplicitConsent" | "CookiePolicy" | string;
  version: string;
  title: string;
  body: string;
  publishedAt: string;
};

const initialForm = {
  firstName: "",
  lastName: "",
  nationalId: "",
  phone: "",
  email: "",
  provinceId: 34,
  districtId: 3401,
  neighborhoodId: 340101,
  address: "",
  postalCode: "",
  privacyNoticeAccepted: false,
  explicitConsentAccepted: false
};

export function ApplicationForm() {
  const [form, setForm] = useState(initialForm);
  const [otpCode, setOtpCode] = useState("");
  const [devCode, setDevCode] = useState<string | null>(null);
  const [verificationToken, setVerificationToken] = useState<string | null>(null);
  const [legalTexts, setLegalTexts] = useState<LegalTextResponse[]>([]);
  const [legalTextError, setLegalTextError] = useState<string | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    let cancelled = false;
    apiFetch<LegalTextResponse[]>("/api/legal-texts/active")
      .then((texts) => {
        if (!cancelled) {
          setLegalTexts(texts);
          setLegalTextError(null);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setLegalTextError("KVKK metinleri yüklenemedi. Lütfen sayfayı yenileyin.");
        }
      });

    return () => {
      cancelled = true;
    };
  }, []);

  async function requestOtp() {
    setError(null);
    setStatus(null);
    setLoading(true);
    try {
      const result = await apiFetch<OtpRequestResponse>("/api/otp/request", {
        method: "POST",
        body: JSON.stringify({
          phone: form.phone,
          captchaToken: "demo-ok",
          deviceId: "web-demo"
        })
      });
      setDevCode(result.developmentCode ?? null);
      setStatus("OTP kodu gönderildi. Demo ortamında kod ekranda gösterilir.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "OTP isteği başarısız.");
    } finally {
      setLoading(false);
    }
  }

  async function verifyOtp() {
    setError(null);
    setStatus(null);
    setLoading(true);
    try {
      const result = await apiFetch<OtpVerifyResponse>("/api/otp/verify", {
        method: "POST",
        body: JSON.stringify({
          phone: form.phone,
          code: otpCode,
          deviceId: "web-demo"
        })
      });
      setVerificationToken(result.verificationToken);
      setStatus("Telefon doğrulandı. Başvuruyu gönderebilirsiniz.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "OTP doğrulama başarısız.");
    } finally {
      setLoading(false);
    }
  }

  async function submitApplication(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setStatus(null);
    if (!verificationToken) {
      setError("Önce telefon doğrulamasını tamamlayın.");
      return;
    }
    setLoading(true);
    try {
      const result = await apiFetch<ApplicationCreatedResponse>("/api/applications", {
        method: "POST",
        body: JSON.stringify({
          ...form,
          verificationToken,
          idempotencyKey: crypto.randomUUID()
        })
      });
      setStatus(`Başvuru alındı. Referans numarası: ${result.referenceNumber}`);
      setForm(initialForm);
      setOtpCode("");
      setDevCode(null);
      setVerificationToken(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Başvuru gönderilemedi.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <form className="form-card" onSubmit={submitApplication}>
      {error && <div className="status error" role="alert">{error}</div>}
      {legalTextError && <div className="status error" role="alert">{legalTextError}</div>}
      {status && <div className="status success" role="status">{status}</div>}
      {devCode && <div className="status">Demo OTP kodu: <strong>{devCode}</strong></div>}

      <div className="form-grid">
        <TextField label="Ad" value={form.firstName} onChange={(value) => setForm({ ...form, firstName: value })} />
        <TextField label="Soyad" value={form.lastName} onChange={(value) => setForm({ ...form, lastName: value })} />
        <TextField label="TCKN" value={form.nationalId} onChange={(value) => setForm({ ...form, nationalId: value })} inputMode="numeric" />
        <TextField label="Telefon" value={form.phone} onChange={(value) => setForm({ ...form, phone: value })} inputMode="tel" />
        <TextField label="E-posta" value={form.email} onChange={(value) => setForm({ ...form, email: value })} inputMode="email" />
        <TextField label="Posta kodu" value={form.postalCode} onChange={(value) => setForm({ ...form, postalCode: value })} inputMode="numeric" required={false} />
        <div className="field">
          <label htmlFor="otp">OTP kodu</label>
          <input id="otp" value={otpCode} onChange={(event) => setOtpCode(event.target.value)} inputMode="numeric" />
          <div className="actions">
            <button type="button" className="secondary" onClick={requestOtp} disabled={loading || !form.phone}>OTP iste</button>
            <button type="button" className="secondary" onClick={verifyOtp} disabled={loading || !otpCode}>OTP doğrula</button>
          </div>
        </div>
        <div className="field">
          <label htmlFor="location">Lokasyon</label>
          <select id="location" value="340101" disabled>
            <option value="340101">İstanbul / Kadıköy / Caferağa</option>
          </select>
        </div>
        <div className="field full">
          <label htmlFor="address">Açık adres</label>
          <textarea id="address" value={form.address} onChange={(event) => setForm({ ...form, address: event.target.value })} rows={4} required />
        </div>
        <div className="field full legal-texts">
          <label>KVKK ve Onay Metinleri</label>
          {legalTexts.length === 0 && !legalTextError && <p className="muted">Metinler yükleniyor...</p>}
          {legalTexts
            .filter((text) => text.type === "PrivacyNotice" || text.type === "ExplicitConsent")
            .map((text) => (
              <details key={text.id}>
                <summary>{text.title} - v{text.version}</summary>
                <p>{text.body}</p>
              </details>
            ))}
        </div>
        <label className="field full">
          <span>
            <input
              type="checkbox"
              checked={form.privacyNoticeAccepted}
              onChange={(event) => setForm({ ...form, privacyNoticeAccepted: event.target.checked })}
            /> KVKK aydınlatma metnini okudum.
          </span>
        </label>
        <label className="field full">
          <span>
            <input
              type="checkbox"
              checked={form.explicitConsentAccepted}
              onChange={(event) => setForm({ ...form, explicitConsentAccepted: event.target.checked })}
            /> Açık rıza metnini onaylıyorum.
          </span>
        </label>
      </div>

      <div className="actions">
        <button type="submit" disabled={loading}>Başvuruyu gönder</button>
      </div>
    </form>
  );
}

function TextField({
  label,
  value,
  onChange,
  inputMode,
  required = true
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  inputMode?: "text" | "numeric" | "tel" | "email";
  required?: boolean;
}) {
  const id = label.toLowerCase().replaceAll(" ", "-");
  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      <input id={id} value={value} onChange={(event) => onChange(event.target.value)} inputMode={inputMode} required={required} />
    </div>
  );
}
