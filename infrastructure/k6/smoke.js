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

const baseUrl = __ENV.BASE_URL || "http://localhost:8080";

export default function () {
  const home = http.get(`${baseUrl}/`);
  check(home, { "home 200": (response) => response.status === 200 });

  const health = http.get(`${baseUrl}/health/live`);
  check(health, { "health live 200": (response) => response.status === 200 });

  sleep(1);
}
