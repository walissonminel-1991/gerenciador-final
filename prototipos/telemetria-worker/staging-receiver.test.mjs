import test from "node:test";
import assert from "node:assert/strict";
import staging, { handleStagingRequest } from "./staging-receiver.mjs";

const TEST_TOKEN = "TEST_ONLY_49b618a4309f48e7a05c7c2c84872d32";
const report = JSON.stringify({
  schema: 1, version: "v5.13.11.8", exit: "normal",
  counts: { "APP.FALHA_INTERFACE": 1, "MSIX.LEITURA": 2 }
});

function mockEnv() {
  const state = { calls: [], batches: [], limiter: [], allowed: true };
  const env = {
    STAGING_ENABLED: "true",
    STAGING_TOKEN: TEST_TOKEN,
    RATE_LIMITER: { async limit({ key }) {
      state.limiter.push(key);
      return { success: state.allowed };
    } },
    DB: {
      prepare(sql) {
        assert.match(sql, /^INSERT INTO daily_counts /);
        return { bind(...args) { state.calls.push(args); return { args }; } };
      },
      async batch(items) { state.batches.push(items); }
    }
  };
  return { env, state };
}
function request(body = report, headers = {}, url = "https://staging.example.invalid/v1/diagnostico") {
  return new Request(url, {
    method: "POST", headers: {
      authorization: "Bearer " + TEST_TOKEN,
      "content-type": "application/json", ...headers
    }, body
  });
}

test("desligado por padrao sem chave, sem limiter, sem DB ou com chave fraca", async () => {
  const { env, state } = mockEnv();
  for (const override of [
    { STAGING_ENABLED: "false" }, { STAGING_ENABLED: undefined },
    { STAGING_TOKEN: undefined }, { RATE_LIMITER: undefined }, { DB: undefined }
  ]) {
    assert.equal((await handleStagingRequest(request(), { ...env, ...override })).status, 503);
  }
  assert.equal(state.calls.length, 0);
  const weak = { ...env, STAGING_TOKEN: "senha" };
  assert.equal((await handleStagingRequest(request(), weak)).status, 401);
});

test("credencial de staging obrigatoria, nao exposta e fora do aplicativo", async () => {
  const { env, state } = mockEnv();
  for (const bad of ["", "Bearer incorreta", "Basic teste", "Bearer " + "X".repeat(300)]) {
    assert.equal((await handleStagingRequest(request(report, { authorization: bad }), env)).status, 401);
  }
  assert.equal(state.calls.length, 0);
  assert.equal(state.limiter.length, 0);
});

test("rejeita arquivos, metodos, content-types, encoding e query", async () => {
  const { env, state } = mockEnv();
  assert.equal((await handleStagingRequest(new Request("https://staging.example.invalid/v1/diagnostico"), env)).status, 404);
  assert.equal((await handleStagingRequest(request(report, {}, "https://staging.example.invalid/v1/diagnostico?file=test.txt"), env)).status, 404);
  assert.equal((await handleStagingRequest(request(report, {}, "http://staging.example.invalid/v1/diagnostico"), env)).status, 404);
  for (const headers of [
    { "content-type": "multipart/form-data" }, { "content-type": "text/plain" },
    { "content-encoding": "gzip" }, { "content-disposition": "attachment; filename=diagnostico.txt" },
    { cookie: "sessao=dado-que-nao-deve-ser-recebido" }
  ]) {
    assert.equal((await handleStagingRequest(request(report, headers), env)).status, 415);
  }
  assert.equal(state.calls.length, 0);
});

test("validacao rejeita campos arbitrarios, duplicados, comandos, payloads grandes e espacos", async () => {
  const { env, state } = mockEnv();
  const cases = [
    "  " + report,
    report.slice(0, -1) + ', "extra": "rm -rf /"}',
    report.replace('"schema":1,', '"schema":1,"schema":1,'),
    report.replace('"MSIX.LEITURA":2', '"C:\\\\Users\\\\Privado":2'),
    report.replace('"MSIX.LEITURA":2', '"MSIX.LEITURA":101'),
    report.replace('"v5.13.11.8"', '"v5.13.11.8\\n"'),
    JSON.stringify({ schema: 1, version: "v5.13.11.8", exit: "normal",
                     counts: { "MSIX.LEITURA": 1 }, file: "malware.exe" }),
    "x".repeat(4097),
    "{malformed}"
  ];
  for (const payload of cases) {
    const result = await handleStagingRequest(request(payload), env);
    assert.ok(result.status === 413 || result.status === 422,
      "Carga invalida nao pode passar; status=" + result.status);
  }
  assert.equal(state.calls.length, 0);
});

test("limite, banco indisponivel e falha do limiter bloqueiam fechados", async () => {
  const { env, state } = mockEnv();
  state.allowed = false;
  assert.equal((await handleStagingRequest(request(), env)).status, 429);
  state.allowed = true;
  assert.equal((await handleStagingRequest(request(), { ...env,
    RATE_LIMITER: { async limit() { throw new Error("Rate-limit indisponivel"); } }
  })).status, 503);
  assert.equal((await handleStagingRequest(request(), { ...env,
    DB: { prepare() { throw new Error("DB indisponivel"); } }
  })).status, 503);
  assert.equal(state.batches.length, 0);
});

test("aceita agregado canonico e guarda somente buckets sem dados individuais", async () => {
  const { env, state } = mockEnv();
  const outcome = await handleStagingRequest(request(), env);
  assert.equal(outcome.status, 204);
  assert.equal(outcome.headers.get("cache-control"), "no-store");
  assert.equal(state.batches.length, 1);
  assert.equal(state.calls.length, 2);
  assert.deepEqual(state.limiter, ["gf-staging"]);
  const categories = state.calls.map(args => args[2]);
  assert.deepEqual(categories, ["APP.FALHA_INTERFACE", "MSIX.LEITURA"]);
  for (const args of state.calls) {
    assert.equal(args.length, 5);
    assert.equal(args[1], "v5.13.11.8");
    assert.ok(Number.isInteger(args[0]) && Number.isInteger(args[3]));
    assert.ok(args[4] > Math.floor(Date.now() / 1000));
    assert.ok(!args.includes(TEST_TOKEN));
  }
});

test("limpeza agendada limita retencao do banco de homologacao", async () => {
  let cutoff = 0, calls = 0;
  await staging.scheduled({}, {
    DB: { prepare(sql) {
      assert.equal(sql, "DELETE FROM daily_counts WHERE expires_at <= ?");
      return { bind(value) {
        cutoff = value;
        return { async run() { calls++; } };
      } };
    } }
  });
  assert.equal(calls, 1);
  assert.ok(Math.abs(cutoff - Math.floor(Date.now() / 1000)) < 3);
});
