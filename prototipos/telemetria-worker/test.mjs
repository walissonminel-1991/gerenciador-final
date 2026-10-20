import test from "node:test";
import assert from "node:assert/strict";
import handler, { validateReport } from "./worker.mjs";

const example = { schema: 1, version: "v5.13.11.1", exit: "normal",
  counts: { "MSIX.LEITURA": 3 } };
test("aceita apenas envelope fixo agregado", () => {
  assert.ok(validateReport(example));
  assert.equal(validateReport({ ...example, hostname: "privado" }), null);
  assert.equal(validateReport({ ...example, counts: { "APP.FALHA_INTERFACE": 101 } }), null);
  assert.equal(validateReport({ ...example, counts: { "PATH.C:\\Users": 2 } }), null);
  assert.equal(validateReport({ ...example, exit: "crash" }), null);
  assert.equal(validateReport({ ...example, version: "nome-privado" }), null);
});
test("rejeita metadados ou payload de logs enviados pelo cliente", async () => {
  const fakeDb = { prepare: () => { throw new Error("Nao deve escrever"); } };
  const request = new Request("https://example.invalid/v1/diagnostico", {
    method: "POST", headers: { "content-type": "application/json" },
    body: JSON.stringify({ ...example, path: "C:\\Users\\private" })
  });
  assert.equal((await handler.fetch(request, { DB: fakeDb })).status, 422);
});
test("grava exclusivamente JSON validado e com prazo de expiração", async () => {
  let inserted;
  const db = { prepare: sql => ({ bind(...args) {
    assert.match(sql, /^INSERT INTO reports/);
    inserted = args;
    return { async run() {} };
  }}) };
  const request = new Request("https://example.invalid/v1/diagnostico", {
    method: "POST", headers: { "content-type": "application/json" },
    body: JSON.stringify(example)
  });
  assert.equal((await handler.fetch(request, { DB: db })).status, 204);
  assert.deepEqual(JSON.parse(inserted[0]), example);
  assert.equal(inserted[2] - inserted[1], 7 * 86400);
});
test("executa limpeza programada", async () => {
  let deleted = false;
  const db = { prepare: sql => ({
    bind: cutoff => {
      assert.match(sql, /^DELETE FROM reports/);
      assert.equal(typeof cutoff, "number");
      return { async run() { deleted = true; } };
    }
  }) };
  await handler.scheduled({}, { DB: db });
  assert.equal(deleted, true);
});
