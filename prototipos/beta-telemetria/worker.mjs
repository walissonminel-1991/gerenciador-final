// Receptor BETA experimental. Nao implantar/ativar nesta missao.
// Sem armazenamento por PC/IP, sem textos livres nem token no aplicativo.
import { allowed, betaVersion, boundedText, expiresForDay } from "./policy.mjs";
import { runMaintenance } from "./github-retention.mjs";
const headers = { "cache-control": "no-store", "content-type": "text/plain; charset=utf-8" };
const respond = status => new Response(null, { status, headers });
export function validateReport(report) {
  if (!report || Array.isArray(report) || typeof report !== "object") return null;
  if (Object.keys(report).sort().join(",") !== "counts,exit,schema,version") return null;
  if (report.schema !== 1 || report.exit !== "normal" ||
      typeof report.version !== "string" ||
      !/^v\d{1,3}(?:\.\d{1,4}){3}$/.test(report.version) ||
      report.version !== betaVersion ||
      !report.counts || Array.isArray(report.counts) || typeof report.counts !== "object") return null;
  const entries = Object.entries(report.counts);
  if (entries.length < 1 || entries.length > 14) return null;
  for (const [code, value] of entries)
    if (!allowed.has(code) || !Number.isSafeInteger(value) || value < 1 || value > 100) return null;
  return { version: report.version, counts: entries };
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
    let limit;
    try { limit = await env.BETA_RATE_LIMITER.limit({ key: ip }); }
    catch { return respond(503); }
    if (limit?.success !== true) return respond(429);
    const body = await boundedText(request.body, 4096);
    if (!body) return respond(400);
    if (body.oversize) return respond(413);
    if (body.size !== size) return respond(400);
    let parsed;try { parsed = JSON.parse(body.raw); } catch { return respond(400); }
    const report = validateReport(parsed);
    if (!report) return respond(422);
    const now = Math.floor(Date.now()/1000), day = Math.floor(now/86400);
    try {
      const batch = report.counts.map(([code, count]) =>
        env.DB.prepare(`INSERT INTO daily_counts(day,version,event,total,expires_at)
        VALUES(?,?,?,?,?)
        ON CONFLICT(day,version,event) DO UPDATE SET
          total=MIN(10000000,daily_counts.total+excluded.total),
          expires_at=MIN(daily_counts.expires_at,excluded.expires_at)`)
          .bind(day,report.version,code,count,expiresForDay(day)));
      await env.DB.batch(batch);
    } catch { return respond(503); }
    return respond(202);
  },
  async scheduled(_event, env) {
    if (!env.DB) return;
    const now = Math.floor(Date.now()/1000);
    // Nao depender de flags de coleta/exportacao para cumprir exclusao.
    // Uma falha em D1 local nao impede tentar a retencao do GitHub.
    let localFailure = false;
    try {
      await env.DB.prepare("DELETE FROM daily_counts WHERE expires_at <= ?").bind(now).run();
    } catch { localFailure = true; }
    const maintenance = await runMaintenance(env, { allowCreate: !localFailure });
    if (localFailure || !maintenance.ok)
      throw new Error("GF_BETA_MAINTENANCE_" + (localFailure ? "database" : maintenance.code));
  }
};
