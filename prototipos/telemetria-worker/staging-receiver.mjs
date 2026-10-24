// Homologacao com dados ficticios apenas. NAO importar no GF nem adicionar
// secrets ao executavel. O worker de producao permanece desativado (503).
// Para ativar este receptor de TESTE: STAGING_ENABLED=true, STAGING_TOKEN
// gerado aleatoriamente (>=32 bytes), DB privado e RATE_LIMITER obrigatorio.
import { validateReport } from "./worker.mjs";

const MAX_BODY = 4096;
const MAX_TOKEN_BYTES = 256;
const HEADERS = { "cache-control": "no-store", "content-type": "text/plain; charset=utf-8" };
const reply = status => new Response(null, { status, headers: HEADERS });

async function sameSecret(given, expected) {
  if (typeof expected !== "string" || expected.length < 32 ||
      expected.length > MAX_TOKEN_BYTES || given.length > MAX_TOKEN_BYTES)
    return false;
  const encoder = new TextEncoder();
  const hashes = await Promise.all([given, expected].map(
    x => crypto.subtle.digest("SHA-256", encoder.encode(x))));
  const a = new Uint8Array(hashes[0]);
  const b = new Uint8Array(hashes[1]);
  let diff = 0;
  for (let i = 0; i < a.length; i++) diff |= a[i] ^ b[i];
  return diff === 0;
}

async function readBounded(request) {
  if (!request.body) return null;
  const declared = request.headers.get("content-length");
  if (declared !== null && (!/^\d+$/.test(declared) || Number(declared) > MAX_BODY))
    return null;
  const reader = request.body.getReader();
  let total = 0;
  const chunks = [];
  try {
    for (;;) {
      const { value, done } = await reader.read();
      if (done) break;
      total += value.byteLength;
      if (total > MAX_BODY) {
        await reader.cancel();
        return null;
      }
      chunks.push(value);
    }
  } catch {
    return null;
  } finally {
    reader.releaseLock();
  }
  if (total === 0 || (declared !== null && Number(declared) !== total))
    return null;
  const combined = new Uint8Array(total);
  let pos = 0;
  for (const chunk of chunks) {
    combined.set(chunk, pos);
    pos += chunk.length;
  }
  try {
    return new TextDecoder("utf-8", { fatal: true }).decode(combined);
  } catch {
    return null;
  }
}

export async function handleStagingRequest(request, env) {
  // Fail-closed: branch/CI podem construir este modulo sem ativar a rede.
  if (!env || env.STAGING_ENABLED !== "true" ||
      !env.DB || !env.RATE_LIMITER ||
      typeof env.RATE_LIMITER.limit !== "function" ||
      !env.STAGING_TOKEN)
    return reply(503);

  let url;
  try { url = new URL(request.url); }
  catch { return reply(404); }
  if (url.protocol !== "https:" || url.username || url.password ||
      url.pathname !== "/v1/diagnostico" || url.search ||
      request.method !== "POST")
    return reply(404);

  const auth = request.headers.get("authorization") || "";
  // Rejeitar cookies, credenciais na URL, autenticacao client-side e uploads.
  if (!auth.startsWith("Bearer ") || !(await sameSecret(auth.slice(7), env.STAGING_TOKEN)))
    return reply(401);
  if ((request.headers.get("content-type") || "").toLowerCase() !== "application/json" ||
      request.headers.has("content-encoding") ||
      request.headers.has("cookie") ||
      request.headers.has("content-disposition"))
    return reply(415);
  try {
    // Limite por credencial de HOMOLOGACAO; credencial nunca deve ser distribuida
    // a usuarios. Retorno 503 quando binding falha, nao desliga protecao.
    const limit = await env.RATE_LIMITER.limit({ key: "gf-staging" });
    if (limit?.success !== true) return reply(429);
  } catch {
    return reply(503);
  }

  const raw = await readBounded(request);
  if (raw === null) return reply(413);

  let report;
  try {
    const parsed = JSON.parse(raw);
    const canonical = validateReport(parsed);
    // Exige bytes canonicos: JSON duplicado, ordem adulterada, texto extra e
    // whitespace inesperado nao passam silenciosamente.
    if (canonical === null || canonical !== raw) return reply(422);
    report = JSON.parse(canonical);
  } catch {
    return reply(422);
  }

  // Banco guarda somente agregados por dia/versao/categoria, NUNCA payload,
  // IP, account, token, texto, timestamp individual ou Device ID.
  const now = Math.floor(Date.now() / 1000);
  const day = Math.floor(now / 86400);
  const expires = now + 7 * 86400;
  try {
    const statements = Object.entries(report.counts).map(([category, n]) =>
      env.DB.prepare(
        "INSERT INTO daily_counts (day, version, event, total, expires_at) " +
        "VALUES (?, ?, ?, ?, ?) ON CONFLICT(day, version, event) DO UPDATE " +
        "SET total = daily_counts.total + excluded.total, " +
        "expires_at = MAX(daily_counts.expires_at, excluded.expires_at)"
      ).bind(day, report.version, category, n, expires));
    await env.DB.batch(statements);
    return reply(204);
  } catch {
    // Nunca expor SQL, conteudo, excecoes nem valores de dados ao cliente.
    return reply(503);
  }
}

export default {
  fetch: handleStagingRequest,
  // Apaga agregados expirados; somente dados tecnicos, nenhum registro individual.
  async scheduled(_event, env) {
    if (!env?.DB) return;
    const now = Math.floor(Date.now() / 1000);
    await env.DB.prepare("DELETE FROM daily_counts WHERE expires_at <= ?")
      .bind(now).run();
  }
};
