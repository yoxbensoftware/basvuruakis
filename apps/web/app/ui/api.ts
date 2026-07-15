const explicitApiUrl = nonEmpty(process.env.NEXT_PUBLIC_API_URL);
const renderApiHost = nonEmpty(process.env.NEXT_PUBLIC_API_HOST);

export const apiBaseUrl = explicitApiUrl ?? (renderApiHost ? `https://${renderApiHost}` : fallbackApiBaseUrl());

function nonEmpty(value: string | undefined): string | undefined {
  value = value?.trim();
  return value ? value : undefined;
}

function fallbackApiBaseUrl(): string {
  if (typeof window !== "undefined" && window.location.hostname === "basvuruakis-web.onrender.com") {
    return "https://basvuruakis-api.onrender.com";
  }

  return "http://localhost:5000";
}

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(init?.headers ?? {})
    }
  });

  if (!response.ok) {
    let message = `İstek başarısız: ${response.status}`;
    try {
      const error = await response.json() as { message?: string; title?: string };
      message = error.message ?? error.title ?? message;
    } catch {
      // Keep default safe message.
    }
    throw new Error(message);
  }

  return await response.json() as T;
}
