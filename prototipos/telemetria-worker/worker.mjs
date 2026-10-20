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
    // Leitura em streaming limitada: nunca carregar mais que 4 KB na memoria.
    let raw;
    try {
      if (!request.body) return new Response(null, { status: 400 });
      const reader = request.body.getReader();
      const chunks = [];
      let total = 0;
      for (;;) {
        const { value, done } = await reader.read();
        if (done) break;
        total += value.byteLength;
        if (total > maxBody) {
          await reader.cancel();
          return new Response(null, { status: 413 });
        }
        chunks.push(value);
      }
      const combined = new Uint8Array(total);
      let offset = 0;
      for (const chunk of chunks) {
        combined.set(chunk, offset);
        offset += chunk.byteLength;
      }
      raw = new TextDecoder("utf-8", { fatal: true }).decode(combined);
    } catch { return new Response(null, { status: 400 }); }
    let parsed;
    try { parsed = JSON.parse(raw); }
    catch { return new Response(null, { status: 400 }); }
    const normalized = validateReport(parsed);
    if (normalized === null) return new Response(null, { status: 422 });
    // Agrega por dia/versao/categoria; nao armazena uma linha por usuario.
    // Não guardar IP, user-agent, cookies, IDs ou metadados do computador.
    const report = JSON.parse(normalized);
    const now = Math.floor(Date.now() / 1000);
    const day = Math.floor(now / 86400);
    const statements = Object.entries(report.counts).map(([event, count]) =>
      env.DB.prepare(
        "INSERT INTO daily_counts (day, version, event, total, expires_at) " +
        "VALUES (?, ?, ?, ?, ?) ON CONFLICT(day, version, event) DO UPDATE " +
        "SET total = daily_counts.total + excluded.total, " +
        "expires_at = MAX(daily_counts.expires_at, excluded.expires_at)"
      ).bind(day, report.version, event, count, now + ttlSeconds));
    await env.DB.batch(statements);
    return new Response(null, { status: 204, headers: { "cache-control": "no-store" } });
  },
  async scheduled(_event, env) {
    const now = Math.floor(Date.now() / 1000);
    await env.DB.prepare("DELETE FROM daily_counts WHERE expires_at <= ?")
      .bind(now).run();
  }
};
