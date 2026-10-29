// Preparacao de homologacao: nenhum acesso externo ao importar ou sem --execute.
// Usa credencial PROPRIA descartavel; nunca consulta process.env.GITHUB_TOKEN.
import { realpathSync, lstatSync, openSync, closeSync, chmodSync } from "node:fs";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { LocalD1 } from "./test-support.mjs";
import { runMaintenance } from "./github-retention.mjs";
import { betaVersion, expiresForDay } from "./policy.mjs";

export function disposableRepository(repo) {
  return typeof repo === "string" &&
    /^[A-Za-z0-9][A-Za-z0-9-]{0,38}\/gf-beta-rehearsal-[a-z0-9][a-z0-9-]{2,50}$/.test(repo);
}

export async function rehearsalPhase({ db, repo, token, expiry, phase, fetch: transport = globalThis.fetch, now = Date.now }) {
  if (!disposableRepository(repo) || !["create", "inspect", "expire", "recover"].includes(phase) ||
      typeof token !== "string" || token.length < 20 || !/^\d{4}-\d{2}-\d{2}$/.test(expiry || ""))
    return { ok: false, code: "rehearsal_configuration" };
  const [owner, name] = repo.split("/");
  // Preflight privado/ADMIN antes de qualquer escrita remota. Create requer
  // repositorio vazio e ledger novo; recuperar nunca repete createIssue.
  let repository;
  try {
    const response = await transport("https://api.github.com/graphql", {
      method: "POST", redirect: "manual", signal: AbortSignal.timeout(8000),
      headers: { authorization: "Bearer " + token, "content-type": "application/json",
        "user-agent": "gf-beta-disposable-rehearsal", accept: "application/vnd.github+json" },
      body: JSON.stringify({ query: `query RehearsalRepository($owner:String!,$name:String!) {
        viewer { id }
        repository(owner:$owner,name:$name) { id isPrivate viewerPermission owner { id __typename } issues { totalCount } }
      }`, variables: { owner, name } })
    });
    if (!response.ok) { void response.body?.cancel(); return { ok: false, code: "rehearsal_access" }; }
    const { boundedText } = await import("./policy.mjs");
    const body = await boundedText(response.body, 16384);
    const result = body && !body.oversize ? JSON.parse(body.raw) : null;
    if (result?.errors?.length) return { ok: false, code: "rehearsal_access" };
    repository = result?.data?.repository;
    if (!repository || repository.isPrivate !== true || repository.viewerPermission !== "ADMIN" ||
        !Number.isSafeInteger(repository.issues?.totalCount) || typeof result.data.viewer?.id !== "string" ||
        !["User", "Organization"].includes(repository.owner?.__typename) ||
        (repository.owner.__typename === "User" && repository.owner.id !== result.data.viewer.id))
      return { ok: false, code: "rehearsal_access" };
  } catch { return { ok: false, code: "rehearsal_transport" }; }

  const jobs = () => db.sqlite.prepare("SELECT count(*) AS n FROM github_exports").get().n;
  const refs = () => db.sqlite.prepare("SELECT count(*) AS n FROM github_delete_refs").get().n;
  if (phase === "inspect") return { ok: true, code: "inspected", issues: repository.issues.totalCount,
    obligations: jobs(), duplicate_obligations: refs() };
  let instant = now();
  if (phase === "create") {
    if (repository.issues.totalCount || jobs() || refs()) return { ok: false, code: "rehearsal_not_empty" };
    const day = Math.floor(instant / 86400000) - 1;
    db.sqlite.prepare("INSERT OR IGNORE INTO daily_counts VALUES(?,?,?,?,?)")
      .run(day, betaVersion, "MSIX.LEITURA", 17, expiresForDay(day));
  }
  if (phase === "expire") {
    if (!jobs() && !refs()) return { ok: false, code: "rehearsal_missing_obligation" };
    const latest = db.sqlite.prepare(`SELECT max(expires_at) AS expiry FROM
      (SELECT expires_at FROM github_exports UNION ALL SELECT expires_at FROM github_delete_refs)`).get().expiry;
    instant = Math.max(instant, (latest + 1) * 1000); // relogio APENAS do processo descartavel
  }
  const env = { DB: db, GITHUB_PRIVATE_REPO: repo, GITHUB_TOKEN: token,
    PILOT_ENABLED: phase === "create" ? "yes" : "no",
    GITHUB_EXPORT_ENABLED: phase === "create" ? "yes" : "no", GITHUB_TOKEN_EXPIRES_AT: expiry };
  const result = await runMaintenance(env, { now: () => instant, fetch: transport,
    sleep: async ms => { await new Promise(r => setTimeout(r, ms)); instant += ms; }, allowCreate: phase === "create" });
  return { ...result, obligations: jobs(), duplicate_obligations: refs() };
}

async function main(args) {
  if (args.length === 0) {
    console.log(JSON.stringify({ mode: "plan_only", network: false,
      usage: "--execute --approved-disposable-repository OWNER/gf-beta-rehearsal-NAME --ledger LOCAL.beta-rehearsal.sqlite --phase create|inspect|expire|recover",
      credential: "GF_REHEARSAL_GITHUB_TOKEN e GF_REHEARSAL_TOKEN_EXPIRES_AT, proprios do ensaio" }));
    return;
  }
  if (args.length !== 7 || args[0] !== "--execute" || args[1] !== "--approved-disposable-repository" ||
      args[3] !== "--ledger" || args[5] !== "--phase" || !disposableRepository(args[2]) ||
      !args[4].endsWith(".beta-rehearsal.sqlite")) throw new Error("rehearsal_arguments");
  // Nunca sobrescrever um link ou truncar um ledger de recuperacao.
  const filename = resolve(args[4]);
  try { closeSync(openSync(filename, "wx", 0o600)); }
  catch (error) { if (error.code !== "EEXIST") throw new Error("rehearsal_ledger"); }
  if (lstatSync(filename).isSymbolicLink() || realpathSync(filename) !== filename) throw new Error("rehearsal_ledger");
  chmodSync(filename, 0o600);
  const db = new LocalD1(undefined, filename);
  try {
    const result = await rehearsalPhase({ db, repo: args[2], phase: args[6],
      token: process.env.GF_REHEARSAL_GITHUB_TOKEN, expiry: process.env.GF_REHEARSAL_TOKEN_EXPIRES_AT });
    console.log(JSON.stringify(result));
    if (!result.ok || ["review_required", "budget", "backlog", "scanning"].includes(result.code)) process.exitCode = 1;
  } finally { db.close(); }
}
if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url))
  main(process.argv.slice(2)).catch(() => { console.error("GF_REHEARSAL_BLOCKED"); process.exitCode = 1; });
