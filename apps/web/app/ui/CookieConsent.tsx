"use client";

import { useEffect, useState } from "react";

export function CookieConsent() {
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setVisible(localStorage.getItem("basvuruakis-cookie-consent") !== "accepted");
    }, 0);
    return () => window.clearTimeout(timer);
  }, []);

  if (!visible) {
    return null;
  }

  return (
    <section className="cookie" aria-label="Çerez bildirimi">
      <p>
        Zorunlu çerezler hizmet için kullanılır. Analitik/pazarlama çerezleri onay verilmeden çalıştırılmaz.
      </p>
      <button
        type="button"
        onClick={() => {
          localStorage.setItem("basvuruakis-cookie-consent", "accepted");
          setVisible(false);
        }}
      >
        Kabul et
      </button>
    </section>
  );
}
