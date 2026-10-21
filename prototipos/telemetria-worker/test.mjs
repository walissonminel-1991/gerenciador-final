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
  for (const ending of ["\n", "\r", "\r\n", "\u2028", "\u2029"])
    assert.equal(validateReport({ ...example, version: example.version + ending }), null);
});
test("receptor permanece desligado mesmo com JSON valido ou malicioso", async () => {
  const fakeDb = { prepare: () => { throw new Error("Nao deve gravar dados de usuarios"); },
                   batch: () => { throw new Error("Nao deve agregar dados"); } };
  for (const value of [example, { ...example, file: "malware.exe" }]) {
    const request = new Request("https://example.invalid/v1/diagnostico", {
      method: "POST", headers: { "content-type": "application/json" },
      body: JSON.stringify(value)
    });
    assert.equal((await handler.fetch(request, { DB: fakeDb })).status, 503);
  }
});
test("executa limpeza programada", async () => {
  let deleted = false;
  const db = { prepare: sql => ({
    bind: cutoff => {
      assert.match(sql, /^DELETE FROM daily_counts/);
      assert.equal(typeof cutoff, "number");
      return { async run() { deleted = true; } };
    }
  }) };
  await handler.scheduled({}, { DB: db });
  assert.equal(deleted, true);
});
