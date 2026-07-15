import http from "k6/http";
import { check, sleep } from "k6";

export const options = {
  vus: 5,
  duration: "30s",
  thresholds: {
    http_req_failed: ["rate<0.01"],
    http_req_duration: ["p(95)<2000"]
  }
};

const webBaseUrl = __ENV.WEB_BASE_URL || __ENV.BASE_URL || "http://localhost:8080";
const apiBaseUrl = __ENV.API_BASE_URL || __ENV.BASE_URL || "http://localhost:8080";
const adminEmail = __ENV.ADMIN_EMAIL || "admin@basvuruakis.local";
const adminPassword = __ENV.ADMIN_PASSWORD || "ChangeMe!12345";
const jsonHeaders = { "Content-Type": "application/json" };

export default function () {
  const home = http.get(`${webBaseUrl}/`);
  check(home, { "home 200": (response) => response.status === 200 });

  const applicationPage = http.get(`${webBaseUrl}/basvuru`);
  check(applicationPage, { "application page 200": (response) => response.status === 200 });

  const health = http.get(`${apiBaseUrl}/health/live`);
  check(health, { "health live 200": (response) => response.status === 200 });

  const ready = http.get(`${apiBaseUrl}/health/ready`);
  check(ready, { "health ready 200": (response) => response.status === 200 });

  if (__ITER === 0) {
    const legalTexts = http.get(`${apiBaseUrl}/api/legal-texts/active`);
    check(legalTexts, { "legal texts 200": (response) => response.status === 200 });

    const otp = http.post(
      `${apiBaseUrl}/api/otp/request`,
      JSON.stringify({
        phone: `0532${String(1000000 + __VU).slice(1)}`,
        captchaToken: "demo-ok",
        deviceId: `k6-smoke-${__VU}`
      }),
      { headers: jsonHeaders }
    );
    check(otp, { "otp request accepted": (response) => response.status === 200 });

    const login = http.post(
      `${apiBaseUrl}/api/admin/auth/login`,
      JSON.stringify({ email: adminEmail, password: adminPassword, totpCode: null }),
      { headers: jsonHeaders }
    );
    check(login, { "admin login accepted": (response) => response.status === 200 });
  }

  sleep(1);
}
