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
const captchaToken = __ENV.CAPTCHA_TOKEN || "demo-ok";
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
  }

  if (__ITER === 0 && __VU === 1) {
    runApplicationAdminSmoke();
  }

  sleep(1);
}

function runApplicationAdminSmoke() {
  const seed = Number(`${Date.now()}${__VU}`.slice(-9));
  const phone = `0532${String(seed).padStart(7, "0").slice(-7)}`;
  const deviceId = `k6-smoke-${Date.now()}-${__VU}`;
  const email = `k6-${seed}@example.test`;

  const otp = http.post(
    `${apiBaseUrl}/api/otp/request`,
    JSON.stringify({
      phone,
      captchaToken,
      deviceId
    }),
    { headers: jsonHeaders }
  );
  check(otp, { "otp request accepted": (response) => response.status === 200 });

  const otpCode = __ENV.OTP_CODE || otp.json("developmentCode");
  if (!check(otpCode, { "otp code available": (value) => Boolean(value) })) {
    return;
  }

  const otpVerify = http.post(
    `${apiBaseUrl}/api/otp/verify`,
    JSON.stringify({
      phone,
      code: otpCode,
      deviceId
    }),
    { headers: jsonHeaders }
  );
  check(otpVerify, { "otp verify accepted": (response) => response.status === 200 });

  const verificationToken = otpVerify.json("verificationToken");
  if (!check(verificationToken, { "verification token available": (value) => Boolean(value) })) {
    return;
  }

  const application = http.post(
    `${apiBaseUrl}/api/applications`,
    JSON.stringify({
      firstName: "K6",
      lastName: "Smoke",
      nationalId: nationalIdFromSeed(seed),
      phone,
      email,
      provinceId: 34,
      districtId: 3401,
      neighborhoodId: 340101,
      address: "K6 smoke test adresi No:1",
      postalCode: "34710",
      privacyNoticeAccepted: true,
      explicitConsentAccepted: true,
      verificationToken,
      idempotencyKey: `k6-${seed}-${__VU}`
    }),
    { headers: jsonHeaders }
  );
  check(application, {
    "application accepted": (response) => response.status === 201 || response.status === 200
  });

  const login = http.post(
    `${apiBaseUrl}/api/admin/auth/login`,
    JSON.stringify({ email: adminEmail, password: adminPassword, totpCode: __ENV.ADMIN_TOTP_CODE || null }),
    { headers: jsonHeaders }
  );
  check(login, { "admin login accepted": (response) => response.status === 200 });

  const accessToken = login.json("accessToken");
  if (!check(accessToken, { "admin access token available": (value) => Boolean(value) })) {
    return;
  }

  const authHeaders = { ...jsonHeaders, Authorization: `Bearer ${accessToken}` };
  const applications = http.get(`${apiBaseUrl}/api/admin/applications?page=1&pageSize=10&email=${encodeURIComponent(email)}`, {
    headers: { Authorization: `Bearer ${accessToken}` }
  });
  check(applications, {
    "admin applications list accepted": (response) => response.status === 200,
    "created application listed": (response) => response.json("total") >= 1
  });

  const exported = http.post(
    `${apiBaseUrl}/api/admin/exports`,
    JSON.stringify({
      format: 0,
      filters: {
        ...emptyApplicationFilters(),
        email
      }
    }),
    { headers: authHeaders }
  );
  check(exported, {
    "admin export accepted": (response) => response.status === 200,
    "csv export contains smoke row": (response) => response.body.includes(email)
  });

  sleep(1);
}

function nationalIdFromSeed(seed) {
  const digits = String(seed).padStart(9, "0").slice(-9).split("").map((digit) => Number(digit));
  if (digits[0] === 0) {
    digits[0] = 1;
  }

  const odd = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
  const even = digits[1] + digits[3] + digits[5] + digits[7];
  const tenth = (((odd * 7 - even) % 10) + 10) % 10;
  const eleventh = (digits.reduce((sum, digit) => sum + digit, 0) + tenth) % 10;
  return `${digits.join("")}${tenth}${eleventh}`;
}

function emptyApplicationFilters() {
  return {
    page: null,
    pageSize: null,
    sort: null,
    desc: null,
    status: null,
    firstName: null,
    lastName: null,
    nationalId: null,
    phone: null,
    email: null,
    provinceId: null,
    districtId: null,
    neighborhoodId: null,
    representativeOfficeId: null,
    isAssigned: null,
    isPhoneVerified: null,
    from: null,
    to: null
  };
}
