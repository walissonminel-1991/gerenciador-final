// MODELO NAO IMPLANTADO. Sem endpoint publico nem chaves do GitHub.
// Recebe somente agregados ja consentidos, com D1 privado e expiracao curta.
const allowed = new Set([
  "APP.FALHA_INTERFACE", "APP.FALHA_NAO_TRATADA",
  "REGISTRO_PROGRAMAS.FALHA", "MSIX.LEITURA",
  "MULTIFONTES.CONSULTA_FALHA", "MULTIFONTES.CONSULTA_CANCELADA",
  "DESINSTALACAO.FALHA", "INSTALACAO_OFICIAL.FALHA",
  "TERMINAL_GITHUB.FALHA", "JANELA.BANDEJA_RESTAURAR.INICIO",
  "JANELA.BANDEJA_RESTAURAR.FIM", "JANELA.BANDEJA_OCULTAR.INICIO",
  "JANELA.BANDEJA_OCULTAR.FIM", "DIAGNOSTICO_COMPLETO.FIM"
]);
const maxBody = 4096;
const ttlSeconds = 7 * 86400;

export function validateReport(value) {
  if (!value || typeof value !== "object" || Array.isArray(value)) return null;
  if (Object.keys(value).sort().join(",") !== "counts,exit,schema,version") return null;
  if (value.schema !== 1 || value.exit !== "normal" ||
      typeof value.version !== "string" ||
      !/^v\d{1,3}(\.\d{1,4}){3}$/.test(value.version) ||
      !value.counts || typeof value.counts !== "object" || Array.isArray(value.counts))
    return null;
  const keys = Object.keys(value.counts);
  if (keys.length === 0 || keys.length > 14) return null;
  for (const key of keys) {
    const n = value.counts[key];
    if (!allowed.has(key) || !Number.isInteger(n) || n < 1 || n > 100) return null;
  }
  return JSON.stringify({ schema: 1, version: value.version, exit: "normal",
    counts: Object.fromEntries(keys.sort().map(key => [key, value.counts[key]])) });
}

export default {
  async fetch(request, env) {
    if (request.method !== "POST" ||
        new URL(request.url).pathname !== "/v1/diagnostico")
      return new Response(null, { status: 404 });
    if ((request.headers.get("content-type") || "").split(";")[0].trim() !==
        "application/json") return new Response(null, { status: 415 });
    const declared = Number(request.headers.get("content-length"));
    if (Number.isFinite(declared) && declared > maxBody)
      return new Response(null, { status: 413 });
    // Read maximum bytes without buffering arbitrarily large payloads.
    let raw;
    try {
      raw = await request.text();
      if (new TextEncoder().encode(raw).length > maxBody)
        return new Response(null, { status: 413 });
    } catch { return new Response(null, { status: 400 }); }
    let parsed;
    try { parsed = JSON.parse(raw); }
    catch { return new Response(null, { status: 400 }); }
    const normalized = validateReport(parsed);
    if (normalized === null) return new Response(null, { status: 422 });
    // Não armazenar IP, user-agent, cookies, URLs, IDs, timestamps enviados pelo cliente.
    // Retenção precisa ser limitada também na configuração de logs do provedor.
    const now = Math.floor(Date.now() / 1000);
    await env.DB.prepare("INSERT INTO reports (data, created_at, expires_at) VALUES (?, ?, ?)")
      .bind(normalized, now, now + ttlSeconds).run();
    return new Response(null, { status: 204, headers: { "cache-control": "no-store" } });
  },
  async scheduled(_event, env) {
    const now = Math.floor(Date.now() / 1000);
    await env.DB.prepare("DELETE FROM reports WHERE expires_at <= ?")
      .bind(now).run();
  }
};
