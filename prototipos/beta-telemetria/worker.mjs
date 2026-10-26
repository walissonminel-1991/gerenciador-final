// Receptor de BETA experimental — NAO implantar antes de WAF, rate limit, LGPD e revisao.
// Sem armazenamento por PC/IP, sem GitHub token no aplicativo, sem textos livres.
const allowed = new Set([
  "APP.FALHA_INTERFACE","APP.FALHA_NAO_TRATADA",
  "REGISTRO_PROGRAMAS.FALHA","MSIX.LEITURA",
  "MULTIFONTES.CONSULTA_FALHA","MULTIFONTES.CONSULTA_CANCELADA",
  "DESINSTALACAO.FALHA","INSTALACAO_OFICIAL.FALHA",
  "TERMINAL_GITHUB.FALHA","JANELA.BANDEJA_RESTAURAR.INICIO",
  "JANELA.BANDEJA_RESTAURAR.FIM","JANELA.BANDEJA_OCULTAR.INICIO",
  "JANELA.BANDEJA_OCULTAR.FIM","DIAGNOSTICO_COMPLETO.FIM"
]);
const ttl = 7 * 86400;
const headers = { "cache-control": "no-store", "content-type": "text/plain; charset=utf-8" };
const respond = status => new Response(null, { status, headers });
// Nunca usar request.text() em entrada de rede: Content-Length pode ser forjado.
// Limitar os bytes efetivamente recebidos antes de decodificar JSON.
async function readBoundedBody(request, maxBytes) {
  const reader = request.body?.getReader();
  if (!reader) return null;
  const chunks = [];
  let size = 0;
  try {
    for (;;) {
      const { done, value } = await reader.read();
      if (done) break;
      size += value.byteLength;
      if (size > maxBytes) {
        await reader.cancel().catch(() => {});
        return { oversize: true };
      }
      chunks.push(value);
    }
    const bytes = new Uint8Array(size);
    let offset = 0;
    for (const part of chunks) { bytes.set(part, offset); offset += part.byteLength; }
    return { raw: new TextDecoder("utf-8", { fatal: true }).decode(bytes), size };
  } catch { return null; }
  finally { reader.releaseLock(); }
}
export function validateReport(report) {
  if (!report || Array.isArray(report) || typeof report !== "object") return null;
  if (Object.keys(report).sort().join(",") !== "counts,exit,schema,version") return null;
  if (report.schema !== 1 || report.exit !== "normal" ||
      typeof report.version !== "string" ||
      !/^v\d{1,3}(?:\.\d{1,4}){3}$/.test(report.version) ||
      report.version !== report.version.trim() ||
      !report.counts || Array.isArray(report.counts) || typeof report.counts !== "object") return null;
  const entries = Object.entries(report.counts);
  if (entries.length < 1 || entries.length > 14) return null;
  for (const [code, value] of entries)
    if (!allowed.has(code) || !Number.isSafeInteger(value) || value < 1 || value > 100) return null;
  return { version: report.version, counts: entries };
}
function githubConfig(env) {
  if (!/^[\w.-]+\/[\w.-]+$/.test(env.GITHUB_PRIVATE_REPO || "") ||
      typeof env.GITHUB_TOKEN !== "string" || env.GITHUB_TOKEN.length < 20) return null;
  return { repo: env.GITHUB_PRIVATE_REPO, token: env.GITHUB_TOKEN };
}
async function exportPrivateDay(env, day) {
  const gh = githubConfig(env);
  if (!gh) return; // jamais enviar a repositorio publico ou sem segredo no servidor
  const rows = await env.DB.prepare("SELECT version,event,total FROM daily_counts WHERE day=? ORDER BY version,event")
    .bind(day).all();
  if (!rows.results?.length) return;
  const existing = await env.DB.prepare("SELECT day FROM github_exports WHERE day=?").bind(day).first();
  if (existing) return;
  const auth = { "authorization": "Bearer " + gh.token, "accept": "application/vnd.github+json",
    "user-agent": "gf-beta-private-aggregation", "x-github-api-version": "2022-11-28" };
  const repository = await fetch("https://api.github.com/repos/" + gh.repo,
    { headers: auth, redirect: "manual" });
  if (!repository.ok || (await repository.json()).private !== true) return;
  const date = new Date(day * 86400 * 1000).toISOString().slice(0, 10);
  const title = "GF BETA — estatísticas agregadas " + date;
  // Busca idempotente antes da criacao para reduzir duplicidade se D1 falhou.
  const listing = await fetch("https://api.github.com/repos/" + gh.repo +
    "/issues?state=all&per_page=100", { headers: auth, redirect: "manual" });
  if (!listing.ok) return;
  const items = await listing.json();
  let issue = Array.isArray(items) ? items.find(x => x.title === title) : null;
  if (!issue) {
    const lines = ["# Dados agregados GF BETA — " + date, "",
      "Somente contagens por versao/categoria. Nenhum diagnostico individual, IP ou identificador de PC.", ""];
    for (const row of rows.results)
      if (/^v\d{1,3}(?:\.\d{1,4}){3}$/.test(row.version) &&
          allowed.has(row.event) && Number.isSafeInteger(row.total))
        lines.push("- " + row.version + " · " + row.event + ": " + row.total);
    const created = await fetch("https://api.github.com/repos/" + gh.repo + "/issues",
      { method: "POST", headers: { ...auth, "content-type": "application/json" },
        body: JSON.stringify({ title, body: lines.join("\n") }), redirect: "manual" });
    if (created.status !== 201) return;
    issue = await created.json();
  }
  if (!Number.isInteger(issue.number)) return;
  await env.DB.prepare("INSERT OR IGNORE INTO github_exports(day,issue_number,exported_at) VALUES(?,?,?)")
    .bind(day, issue.number, Math.floor(Date.now()/1000)).run();
}
export default {
  async fetch(request, env) {
    if (new URL(request.url).pathname !== "/ingest") return respond(404);
    if (request.method !== "POST") return respond(405);
    // Exigir bindings reais de seguranca no Cloudflare; ausentes = desativado.
    if (!env.DB || !env.BETA_RATE_LIMITER || !env.PILOT_ENABLED || env.PILOT_ENABLED !== "yes") return respond(503);
    if (!/^application\/json(?:;|$)/i.test(request.headers.get("content-type") || "")) return respond(415);
    const size = Number(request.headers.get("content-length"));
    if (!Number.isInteger(size) || size < 1 || size > 4096) return respond(413);
    const ip = request.headers.get("cf-connecting-ip");
    if (!ip) return respond(403); // usado somente em rate limit efemero, NAO persiste em D1
    const limit = await env.BETA_RATE_LIMITER.limit({ key: ip });
    if (!limit.success) return respond(429);
    const body = await readBoundedBody(request, 4096);
    if (!body) return respond(400);
    if (body.oversize) return respond(413);
    if (body.size !== size) return respond(400);
    let parsed;try { parsed = JSON.parse(body.raw); } catch { return respond(400); }
    const report = validateReport(parsed);
    if (!report) return respond(422);
    const now = Math.floor(Date.now()/1000), day = Math.floor(now/86400);
    const batch = report.counts.map(([code, count]) =>
      env.DB.prepare(`INSERT INTO daily_counts(day,version,event,total,expires_at)
        VALUES(?,?,?,?,?)
        ON CONFLICT(day,version,event) DO UPDATE SET
          total=MIN(10000000,daily_counts.total+excluded.total),
          expires_at=excluded.expires_at`)
        .bind(day,report.version,code,count,now+ttl));
    try { await env.DB.batch(batch); } catch { return respond(503); }
    return respond(202);
  },
  async scheduled(_event, env) {
    if (!env.DB) return;
    const now = Math.floor(Date.now()/1000);
    await env.DB.prepare("DELETE FROM daily_counts WHERE expires_at <= ?").bind(now).run();
    // Metadados de exportacao D1 tambem devem ter expiracao finita.
    await env.DB.prepare("DELETE FROM github_exports WHERE day < ?")
      .bind(Math.floor(now/86400)-7).run();
    if (!env.PILOT_ENABLED || env.PILOT_ENABLED !== "yes") return;
    await exportPrivateDay(env, Math.floor(now/86400)-1);
  }
};