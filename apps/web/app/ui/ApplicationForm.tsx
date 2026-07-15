"use client";

import { FormEvent, useEffect, useRef, useState } from "react";
import { apiFetch } from "./api";

type TurnstileOptions = {
  sitekey: string;
  callback: (token: string) => void;
  "expired-callback": () => void;
  "error-callback": () => void;
};

declare global {
  interface Window {
    turnstile?: {
      render: (container: HTMLElement, options: TurnstileOptions) => string;
      reset: (widgetId: string) => void;
      remove: (widgetId: string) => void;
    };
  }
}

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

const captchaProvider = (process.env.NEXT_PUBLIC_CAPTCHA_PROVIDER ?? "development").trim().toLowerCase();
const turnstileSiteKey = (process.env.NEXT_PUBLIC_TURNSTILE_SITE_KEY ?? "").trim();
const turnstileEnabled = captchaProvider === "turnstile" || turnstileSiteKey.length > 0;
const turnstileScriptId = "cloudflare-turnstile-script";
const deviceIdStorageKey = "basvuruakis-device-id";

export function ApplicationForm() {
  const [form, setForm] = useState(initialForm);
  const [otpCode, setOtpCode] = useState("");
  const [devCode, setDevCode] = useState<string | null>(null);
  const [verificationToken, setVerificationToken] = useState<string | null>(null);
  const [captchaToken, setCaptchaToken] = useState(turnstileEnabled ? "" : "demo-ok");
  const [legalTexts, setLegalTexts] = useState<LegalTextResponse[]>([]);
  const [legalTextError, setLegalTextError] = useState<string | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const turnstileContainerRef = useRef<HTMLDivElement | null>(null);
  const turnstileWidgetIdRef = useRef<string | null>(null);
  const deviceIdRef = useRef<string | null>(null);

  useEffect(() => {
    if (!turnstileEnabled) {
      return;
    }

    if (!turnstileSiteKey) {
      return;
    }

    let cancelled = false;
    const renderTurnstile = () => {
      if (cancelled || !turnstileContainerRef.current || !window.turnstile || turnstileWidgetIdRef.current) {
        return;
      }

      turnstileWidgetIdRef.current = window.turnstile.render(turnstileContainerRef.current, {
        sitekey: turnstileSiteKey,
        callback: (token) => setCaptchaToken(token),
        "expired-callback": () => setCaptchaToken(""),
        "error-callback": () => setCaptchaToken("")
      });
    };

    if (window.turnstile) {
      renderTurnstile();
    } else {
      const existingScript = document.getElementById(turnstileScriptId);
      if (existingScript) {
        existingScript.addEventListener("load", renderTurnstile, { once: true });
      } else {
        const script = document.createElement("script");
        script.id = turnstileScriptId;
        script.src = "https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit";
        script.async = true;
        script.defer = true;
        script.addEventListener("load", renderTurnstile, { once: true });
        document.head.appendChild(script);
      }
    }

    return () => {
      cancelled = true;
      const widgetId = turnstileWidgetIdRef.current;
      if (widgetId && window.turnstile) {
        window.turnstile.remove(widgetId);
        turnstileWidgetIdRef.current = null;
      }
    };
  }, []);

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
    if (turnstileEnabled && !turnstileSiteKey) {
      setError("CAPTCHA yapılandırması eksik.");
      return;
    }
    if (turnstileEnabled && !captchaToken) {
      setError("CAPTCHA doğrulamasını tamamlayın.");
      return;
    }

    setLoading(true);
    try {
      const result = await apiFetch<OtpRequestResponse>("/api/otp/request", {
        method: "POST",
        body: JSON.stringify({
          phone: form.phone,
          captchaToken,
          deviceId: getDeviceId()
        })
      });
      setDevCode(result.developmentCode ?? null);
      setStatus(result.developmentCode ? "OTP kodu gönderildi. Demo ortamında kod ekranda gösterilir." : "OTP kodu gönderildi.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "OTP isteği başarısız.");
    } finally {
      resetCaptcha();
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
          deviceId: getDeviceId()
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

  function resetCaptcha() {
    if (!turnstileEnabled) {
      return;
    }

    const widgetId = turnstileWidgetIdRef.current;
    if (widgetId && window.turnstile) {
      window.turnstile.reset(widgetId);
    }
    setCaptchaToken("");
  }

  function getDeviceId() {
    deviceIdRef.current ??= resolveDeviceId();
    return deviceIdRef.current;
  }

  const activeLegalTexts = legalTexts.filter((text) => text.type === "PrivacyNotice" || text.type === "ExplicitConsent");
  const canSubmit = !loading && Boolean(verificationToken) && form.privacyNoticeAccepted && form.explicitConsentAccepted;

  return (
    <form className="form-card intake-form" onSubmit={submitApplication}>
      <div className="form-card-header">
        <div>
          <p className="eyebrow">Güvenli başvuru akışı</p>
          <h2>Başvuru bilgileri</h2>
        </div>
        <span className={verificationToken ? "badge success" : "badge"}>{verificationToken ? "Telefon doğrulandı" : "Doğrulama bekliyor"}</span>
      </div>

      {(error || legalTextError || status || devCode) && (
        <div className="message-stack">
          {error && <div className="status error" role="alert">{error}</div>}
          {legalTextError && <div className="status error" role="alert">{legalTextError}</div>}
          {status && <div className="status success" role="status">{status}</div>}
          {devCode && <div className="status neutral">Demo OTP kodu: <strong>{devCode}</strong></div>}
        </div>
      )}

      <div className="step-grid">
        <div className="step done"><strong>1</strong><span>Kimlik</span></div>
        <div className={verificationToken ? "step done" : "step active"}><strong>2</strong><span>Telefon</span></div>
        <div className="step"><strong>3</strong><span>Adres</span></div>
        <div className={canSubmit ? "step done" : "step"}><strong>4</strong><span>Onay</span></div>
      </div>

      <section className="form-section" aria-labelledby="identity-section">
        <div className="section-heading">
          <h3 id="identity-section">Kimlik ve iletişim</h3>
          <p>Başvuru sahibinin doğrulanabilir temel bilgileri.</p>
        </div>
        <div className="form-grid">
          <TextField label="Ad" value={form.firstName} onChange={(value) => setForm({ ...form, firstName: value })} />
          <TextField label="Soyad" value={form.lastName} onChange={(value) => setForm({ ...form, lastName: value })} />
          <TextField label="TCKN" value={form.nationalId} onChange={(value) => setForm({ ...form, nationalId: value })} inputMode="numeric" />
          <TextField label="Telefon" value={form.phone} onChange={(value) => setForm({ ...form, phone: value })} inputMode="tel" />
          <TextField label="E-posta" value={form.email} onChange={(value) => setForm({ ...form, email: value })} inputMode="email" />
          <TextField label="Posta kodu" value={form.postalCode} onChange={(value) => setForm({ ...form, postalCode: value })} inputMode="numeric" required={false} />
        </div>
      </section>

      <section className="form-section verification-panel" aria-labelledby="verification-section">
        <div className="section-heading">
          <h3 id="verification-section">Telefon doğrulama</h3>
          <p>OTP doğrulaması tamamlanmadan başvuru gönderilmez.</p>
        </div>
        <div className="form-grid">
          {turnstileEnabled && (
            <div className="field full captcha-field">
              <label>CAPTCHA doğrulaması</label>
              {turnstileSiteKey ? (
                <div ref={turnstileContainerRef} />
              ) : (
                <p className="muted">CAPTCHA yapılandırması eksik.</p>
              )}
            </div>
          )}
          <div className="field otp-field">
            <label htmlFor="otp">OTP kodu</label>
            <input id="otp" value={otpCode} onChange={(event) => setOtpCode(event.target.value)} inputMode="numeric" autoComplete="one-time-code" />
          </div>
          <div className="field otp-actions">
            <span className="field-label">İşlem</span>
            <div className="button-row">
              <button type="button" className="secondary" onClick={requestOtp} disabled={loading || !form.phone || (turnstileEnabled && (!turnstileSiteKey || !captchaToken))}>OTP iste</button>
              <button type="button" className="secondary" onClick={verifyOtp} disabled={loading || !otpCode}>OTP doğrula</button>
            </div>
          </div>
        </div>
      </section>

      <section className="form-section" aria-labelledby="address-section">
        <div className="section-heading">
          <h3 id="address-section">Adres ve lokasyon</h3>
          <p>Demo veri setinde Kadıköy / Caferağa lokasyonu kullanılır.</p>
        </div>
        <div className="form-grid">
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
        </div>
      </section>

      <section className="form-section" aria-labelledby="consent-section">
        <div className="section-heading">
          <h3 id="consent-section">KVKK onayları</h3>
          <p>Aktif metin sürümleri başvuru kaydına bağlanır.</p>
        </div>
        <div className="legal-texts">
          <label>KVKK ve onay metinleri</label>
          {legalTexts.length === 0 && !legalTextError && <p className="muted">Metinler yükleniyor...</p>}
          {activeLegalTexts.map((text) => (
            <details key={text.id}>
              <summary>{text.title} - v{text.version}</summary>
              <p>{text.body}</p>
            </details>
          ))}
        </div>
        <div className="consent-list">
          <label className="checkline">
            <input
              type="checkbox"
              checked={form.privacyNoticeAccepted}
              onChange={(event) => setForm({ ...form, privacyNoticeAccepted: event.target.checked })}
            />
            <span>KVKK aydınlatma metnini okudum.</span>
          </label>
          <label className="checkline">
            <input
              type="checkbox"
              checked={form.explicitConsentAccepted}
              onChange={(event) => setForm({ ...form, explicitConsentAccepted: event.target.checked })}
            />
            <span>Açık rıza metnini onaylıyorum.</span>
          </label>
        </div>
      </section>

      <div className="submit-bar">
        <div>
          <strong>{verificationToken ? "Gönderime hazır" : "Telefon doğrulaması bekleniyor"}</strong>
          <span>Başvuru gönderildiğinde referans numarası üretilecek.</span>
        </div>
        <button type="submit" disabled={!canSubmit}>Başvuruyu gönder</button>
      </div>
    </form>
  );
}

function resolveDeviceId(): string {
  try {
    const existing = window.localStorage.getItem(deviceIdStorageKey);
    if (existing) {
      return existing;
    }

    const next = createDeviceId();
    window.localStorage.setItem(deviceIdStorageKey, next);
    return next;
  } catch {
    return createDeviceId();
  }
}

function createDeviceId(): string {
  return typeof crypto !== "undefined" && "randomUUID" in crypto
    ? crypto.randomUUID()
    : `web-${Date.now()}-${Math.random().toString(36).slice(2)}`;
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
