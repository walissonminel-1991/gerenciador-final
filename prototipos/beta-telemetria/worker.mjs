// Receptor BETA experimental. Nao implantar/ativar nesta missao.
// Sem armazenamento por PC/IP, sem textos livres nem token no aplicativo.
import { allowed, betaVersion, boundedText, expiresForDay } from "./policy.mjs";
import { runMaintenance } from "./github-retention.mjs";
import { parseStrictJson } from "./strict-json.mjs";
import { readHealth,logHealth } from "./observability.mjs";
const headers = { "cache-control": "no-store", "content-type": "text/plain; charset=utf-8" };
const respond = status => new Response(null, { status, headers });
export function approvedOrigin(value) {
  try {
    const url = new URL(value);
    return url.protocol === "https:" && url.origin === value &&
      !url.username && !url.password && !url.port && url.hostname.includes(".") &&
      /^[a-z0-9.-]+$/.test(url.hostname) && !url.hostname.endsWith(".") &&
      !/^[0-9.]+$/.test(url.hostname) && !url.hostname.endsWith(".workers.dev") &&
      url.hostname !== "workers.dev" ? url.origin : null;
  } catch { return null; }
}
export function validateReport(report) {
  if (!report || Array.isArray(report) || typeof report !== "object") return null;
  if (Object.keys(report).sort().join(",") !== "counts,exit,schema,version") return null;
  if (report.schema !== 1 || !["normal", "incompleto"].includes(report.exit) ||
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
    const url=new URL(request.url);
    if (url.pathname !== "/ingest" || url.search) return respond(404);
    if (request.method !== "POST") return respond(405);
    // Exigir bindings reais de seguranca no Cloudflare; ausentes = desativado.
    if (!env.DB || !env.BETA_RATE_LIMITER || !env.PILOT_ENABLED || env.PILOT_ENABLED !== "yes") return respond(503);
    // Defesa adicional contra uma rota alternativa reativada por engano.
    // O valor revisado e servidor, nunca Host/Origin fornecido no payload.
    const origin = approvedOrigin(env.INGEST_ORIGIN);
    if (!origin) return respond(503);
    if (url.origin !== origin) return respond(403);
    if (!/^application\/json(?:;|$)/i.test(request.headers.get("content-type") || "")) return respond(415);
    const declared=request.headers.get("content-length")||"";
    const size = /^[1-9]\d{0,3}$/.test(declared)?Number(declared):NaN;
    if (!Number.isInteger(size) || size < 1 || size > 4096) return respond(413);
    const ip = request.headers.get("cf-connecting-ip");
    if (!ip) return respond(403); // usado somente em rate limit efemero, NAO persiste em D1
    let limit;
    try {
      limit = await env.BETA_RATE_LIMITER.limit({ key: ip });
      // Mesmo binding existente, chave global por PoP: nao e um limite global
      // estrito. O teto transacional D1 abaixo protege as ESCRITAS de contagens.
      if(limit?.success===true)limit=await env.BETA_RATE_LIMITER.limit({key:"gf-beta-global-v1"});
    }
    catch { return respond(503); }
    if (limit?.success !== true) return respond(429);
    const body = await boundedText(request.body, 4096);
    if (!body) return respond(400);
    if (body.oversize) return respond(413);
    if (body.size !== size) return respond(400);
    const parsed=parseStrictJson(body.raw);
    if(parsed===null)return respond(400);
    const report = validateReport(parsed);
    if (!report) return respond(422);
    const now = Math.floor(Date.now()/1000), day = Math.floor(now/86400);
    try {
      if ((await env.DB.prepare("SELECT schema_version FROM github_maintenance WHERE id=1").first())?.schema_version !== 3)
        return respond(503);
      const ticket=crypto.randomUUID(),expiry=expiresForDay(day);
      const events=report.counts.reduce((n,[,value])=>n+value,0);
      const batch=[env.DB.prepare(`INSERT INTO ingest_budget(day,reports,event_total,expires_at,ticket)
        VALUES(?,1,?,?,?) ON CONFLICT(day) DO UPDATE SET
        reports=reports+1,event_total=event_total+excluded.event_total,ticket=excluded.ticket
        WHERE reports<1000`).bind(day,events,expiry,ticket),...report.counts.map(([code, count]) =>
        env.DB.prepare(`INSERT INTO daily_counts(day,version,event,total,expires_at)
        SELECT ?,?,?,?,? WHERE EXISTS(SELECT 1 FROM ingest_budget WHERE day=? AND ticket=?)
        ON CONFLICT(day,version,event) DO UPDATE SET
          total=MIN(10000000,daily_counts.total+excluded.total),
          expires_at=MIN(daily_counts.expires_at,excluded.expires_at)`)
          .bind(day,report.version,code,count,expiry,day,ticket)),
        // O ticket e somente controle transacional: nao fica persistido apos commit.
        env.DB.prepare("UPDATE ingest_budget SET ticket=NULL WHERE day=? AND ticket=?").bind(day,ticket)];
      const result=await env.DB.batch(batch);
      if(result[0]?.meta?.changes!==1)return respond(429);
    } catch { return respond(503); }
    return respond(202);
  },
  async scheduled(_event, env) {
    const now = Math.floor(Date.now()/1000);
    // Nao depender de flags de coleta/exportacao para cumprir exclusao.
    // Uma falha em D1 local nao impede tentar a retencao do GitHub.
    let localFailure = false;
    try {
      await env.DB.prepare("DELETE FROM daily_counts WHERE expires_at <= ?").bind(now).run();
      await env.DB.prepare("DELETE FROM ingest_budget WHERE expires_at <= ?").bind(now).run();
    } catch { localFailure = true; }
    const maintenance = await runMaintenance(env, { allowCreate: !localFailure });
    try { logHealth(await readHealth(env.DB,now,env.GITHUB_TOKEN_EXPIRES_AT),env.OBSERVABILITY_LOG_ENABLED); }
    catch { localFailure=true; }
    if (localFailure || !maintenance.ok)
      throw new Error("GF_BETA_MAINTENANCE_" + (localFailure ? "database" : maintenance.code));
  }
};
