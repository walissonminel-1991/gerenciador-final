import test, { before, after } from "node:test";
import assert from "node:assert/strict";
import worker, { approvedOrigin, validateReport } from "./worker.mjs";
import { fixture, json } from "./test-support.mjs";
import { betaVersion } from "./policy.mjs";
import { disposableRepository, rehearsalPhase } from "./github-rehearsal.mjs";
import { runMaintenance } from "./github-retention.mjs";
import {inspectSchema,migrationPlan} from "./schema-migration.mjs";
const originalFetch = globalThis.fetch;
before(() => { globalThis.fetch = async () => { throw new Error("TEST_NETWORK_FORBIDDEN"); }; });
after(() => { globalThis.fetch = originalFetch; });
const request = (origin = "https://receiver.invalid") => {
  const body = JSON.stringify({ schema: 1, version: betaVersion, exit: "normal", counts: { "MSIX.LEITURA": 17 } });
  return new Request(origin + "/ingest", { method: "POST", body, headers: {
    "content-type": "application/json", "content-length": String(Buffer.byteLength(body)), "cf-connecting-ip": "198.51.100.5" } });
};
const ingestEnv = f => ({ ...f.env, PILOT_ENABLED: "yes", BETA_RATE_LIMITER: { limit: async () => ({ success: true }) } });

test("origem aprovada recusa URLs alternativas, HTTP, portas e origem ambigua", async t => {
  for (const value of [undefined, "http://receiver.invalid", "https://test.workers.dev", "https://receiver.invalid/",
    "https://user@receiver.invalid", "https://127.0.0.1", "https://receiver.invalid:444", "https://receiver.invalid?x=1"])
    assert.equal(approvedOrigin(value), null);
  assert.equal(approvedOrigin("https://receiver.invalid"), "https://receiver.invalid");
  const f = fixture(t), env = ingestEnv(f);
  for (const origin of ["http://receiver.invalid", "https://alternate.invalid", "https://preview.workers.dev"])
    assert.equal((await worker.fetch(request(origin), env)).status, 403);
  env.INGEST_ORIGIN = "";
  assert.equal((await worker.fetch(request(), env)).status, 503);
  assert.equal(f.db.sqlite.prepare("SELECT count(*) AS n FROM daily_counts").get().n, 0);
});

test("ingestao nao habilita escrita com D1 pre-v3 mesmo que flags estejam erradas", async t => {
  const f = fixture(t); f.db.sqlite.exec("UPDATE github_maintenance SET schema_version=2");
  assert.equal((await worker.fetch(request(), ingestEnv(f))).status, 503);
  assert.equal(f.db.sqlite.prepare("SELECT count(*) AS n FROM ingest_budget").get().n, 0);
});

test("fluxo simulado completo HTTPS -> D1 -> GitHub privado -> expiracao e exclusao", async t => {
  const f = fixture(t); t.mock.method(Date, "now", f.runtime.now);
  assert.equal((await worker.fetch(request(), ingestEnv(f))).status, 202);
  const row = f.db.sqlite.prepare("SELECT * FROM daily_counts").get();
  assert.equal(row.total, 17);
  assert.deepEqual(Object.keys(row).sort(), ["day", "version", "event", "total", "expires_at"].sort());
  f.advance(86400); f.enable();
  assert.equal((await runMaintenance(f.env, f.runtime)).code, "exported");
  assert.equal(f.remote.created.length, 1);
  assert.match(f.remote.created[0].body, /MSIX.LEITURA: 15/);
  f.env.PILOT_ENABLED = f.env.GITHUB_EXPORT_ENABLED = "no";
  f.advance(7 * 86400); t.mock.method(globalThis, "fetch", f.remote.fetch);
  await worker.scheduled(null, f.env);
  assert.equal(f.remote.deleted.length, 1); assert.equal(f.jobs().length, 0);
  assert.equal(f.db.sqlite.prepare("SELECT count(*) AS n FROM daily_counts").get().n, 0);
  assert.equal(f.db.sqlite.prepare("SELECT count(*) AS n FROM ingest_budget").get().n, 0);
});

test("ensaio preparado rejeita repositorios reais antes de usar credencial ou rede", async t => {
  const f = fixture(t);
  for (const repo of ["walissonminel-1991/gerenciador-final", "walissonminel-1991/gerenciador-final-beta-agregados", "fixture/private"])
    assert.equal(disposableRepository(repo), false);
  assert.equal(disposableRepository("fixture-owner/gf-beta-rehearsal-20261026"), true);
  const result = await rehearsalPhase({ db: f.db, repo: "walissonminel-1991/gerenciador-final-beta-agregados",
    token: "synthetic-not-a-production-secret", expiry: "2026-11-25", phase: "expire" });
  assert.equal(result.code, "rehearsal_configuration");
});

test("mesmo motor de ensaio cria, le e exclui Issue sintetica com relogio local", async t => {
  const f = fixture(t), repo = "fixture-owner/gf-beta-rehearsal-20261026";
  const transport = (url, options) => {
    if (JSON.parse(options.body).query.startsWith("query RehearsalRepository"))
      return json({ data: { viewer: {id:"ACTOR_FIXTURE"}, repository: { ...f.remote.repo(), issues: { totalCount: f.remote.issues.length } } } });
    return f.remote.fetch(url, options);
  };
  const options = { db: f.db, repo, token: f.env.GITHUB_TOKEN, expiry: "2026-11-25", fetch: transport, now: f.runtime.now };
  assert.equal((await rehearsalPhase({ ...options, phase: "create" })).code, "exported");
  assert.equal((await rehearsalPhase({ ...options, phase: "inspect" })).issues, 1);
  assert.equal((await rehearsalPhase({ ...options, phase: "create" })).code, "rehearsal_not_empty");
  assert.equal((await rehearsalPhase({ ...options, phase: "expire" })).obligations, 0);
  assert.equal(f.remote.created.length, 1); assert.equal(f.remote.deleted.length, 1);
});

test("ensaio com destino publico ou sem ADMIN nao cria nem exclui", async t => {
  const f = fixture(t);
  for (const change of [{ isPrivate: false }, { viewerPermission: "WRITE" }]) {
    const result = await rehearsalPhase({ db: f.db, repo: "fixture-owner/gf-beta-rehearsal-20261026",
      token: f.env.GITHUB_TOKEN, expiry: "2026-11-25", phase: "create", now: f.runtime.now,
      fetch: async () => json({ data: { viewer: {id:"ACTOR_FIXTURE"}, repository: { ...f.remote.repo(), ...change, issues: { totalCount: 0 } } } }) });
    assert.equal(result.code, "rehearsal_access");
  }
  assert.equal(f.remote.calls.length, 0); assert.equal(f.jobs().length, 0);
});

test("encerramento incompleto usa somente o schema e categorias ja existentes", () => {
  assert.ok(validateReport({schema:1,version:betaVersion,exit:"incompleto",counts:{"APP.FALHA_NAO_TRATADA":1}}));
  assert.equal(validateReport({schema:1,version:betaVersion,exit:"incompleto",counts:{"APP.NOVO_CAMPO":1}}),null);
});

test("repositorio pessoal exige ator proprietario antes de criar divida de retencao", async t => {
  const f=fixture(t);f.enable();f.count();f.remote.ownerId="OTHER_ACCOUNT";
  assert.equal((await runMaintenance(f.env,f.runtime)).code,"authorization");
  assert.equal(f.remote.created.length,0);assert.equal(f.remote.deleted.length,0);
  assert.equal(f.jobs().length,0);
});

test("planejador nao rebaixa banco de versao futura desconhecida", async t => {
  const f=fixture(t);f.db.sqlite.exec("UPDATE github_maintenance SET schema_version=4");
  const snapshot=await inspectSchema(f.db);
  assert.throws(()=>migrationPlan(snapshot), /schema_unknown_version/);
  assert.equal(f.state().schema_version,4);
});
