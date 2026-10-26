import test, { before, after } from "node:test";
import assert from "node:assert/strict";
import worker, { validateReport } from "./worker.mjs";
import { LocalD1 } from "./test-support.mjs";
const realFetch=globalThis.fetch;
before(()=>{globalThis.fetch=async()=>{throw new Error("TEST_NETWORK_FORBIDDEN");};});
after(()=>{globalThis.fetch=realFetch;});
const good = { schema:1, version:"v5.13.11.66", exit:"normal", counts:{"MSIX.LEITURA":2} };
test("schema estrito impede identificadores e texto livre", () => {
  assert.ok(validateReport(good));
  for (const bad of [
    {...good, hostname:"pc"}, {...good, counts:{"PATH.USER":1}},
    {...good, counts:{"MSIX.LEITURA":101}}, {...good, version:"../../etc"},
    {...good, counts:{}}, {...good, exit:"incompleto"}
  ]) assert.equal(validateReport(bad), null);
});
test("servidor nao implantado ou sem limite recusa envio", async () => {
  const request = new Request("https://private.example/ingest", {method:"POST",
    headers:{"content-type":"application/json","cf-connecting-ip":"198.51.100.5","content-length":String(Buffer.byteLength(JSON.stringify(good)))},
    body:JSON.stringify(good)});
  assert.equal((await worker.fetch(request,{PILOT_ENABLED:"no"})).status,503);
});
test("receptor limitado acumula somente agregados com 202", async t => {
  const db=new LocalD1();t.after(()=>db.close());
  const env={PILOT_ENABLED:"yes",BETA_RATE_LIMITER:{limit:async()=>({success:true})},DB:db};
  const request = new Request("https://private.example/ingest", {method:"POST",
    headers:{"content-type":"application/json","cf-connecting-ip":"198.51.100.5","content-length":String(Buffer.byteLength(JSON.stringify(good)))},
    body:JSON.stringify(good)});
  assert.equal((await worker.fetch(request,env)).status,202);
  const row=db.sqlite.prepare("SELECT * FROM daily_counts").get();
  assert.equal(row.event,"MSIX.LEITURA");assert.equal(row.total,2);
  const admission=db.sqlite.prepare("SELECT * FROM ingest_budget").get();
  assert.equal(admission.reports,1);assert.equal(admission.ticket,null);
});
test("sem IP/sem aceitação ou limite retorna erro", async()=> {
  const base={PILOT_ENABLED:"yes",DB:{batch:async()=>{},prepare:()=>({bind:()=>({run:async()=>{}})})},
    BETA_RATE_LIMITER:{limit:async()=>({success:false})}};
  const request=new Request("https://private.example/ingest",{method:"POST",
    headers:{"content-type":"application/json","cf-connecting-ip":"198.51.100.5","content-length":String(Buffer.byteLength(JSON.stringify(good)))},
    body:JSON.stringify(good)});
  assert.equal((await worker.fetch(request,base)).status,429);
});

test("Content-Length falso e corpo maior que 4KB falham antes de gravar", async () => {
  let writes = 0;
  const env = { PILOT_ENABLED:"yes",
    BETA_RATE_LIMITER:{limit:async()=>({success:true})},
    DB:{prepare:()=>({bind:()=>({run:async()=>{writes++}})}),
      batch:async()=>{writes++}} };
  const mk = (body,declaredSize) => new Request("https://private.example/ingest", {
    method:"POST", headers:{"content-type":"application/json",
      "cf-connecting-ip":"198.51.100.5","content-length":String(declaredSize)},
    body
  });
  const json = JSON.stringify(good);
  assert.equal((await worker.fetch(mk(json,1),env)).status,400);
  assert.equal((await worker.fetch(mk("x".repeat(5000),1),env)).status,413);
  assert.equal(writes,0);
});

test("limpeza D1 desligada nao esquece obrigacao de excluir Issue", async t => {
  const db = new LocalD1();
  t.after(() => db.close());
  const day = Math.floor(Date.now()/86400000)-8;
  db.sqlite.prepare("INSERT INTO daily_counts VALUES(?,?,?,?,?)").run(day,good.version,"MSIX.LEITURA",2,1);
  db.sqlite.prepare("INSERT INTO github_exports(day,issue_number,exported_at,expires_at) VALUES(?,?,?,?)").run(day,42,1,1);
  const env={PILOT_ENABLED:"no",GITHUB_EXPORT_ENABLED:"no",DB:db};
  await assert.rejects(worker.scheduled(null,env), /GF_BETA_MAINTENANCE_configuration/);
  assert.equal(db.sqlite.prepare("SELECT count(*) AS n FROM daily_counts").get().n,0);
  assert.equal(db.sqlite.prepare("SELECT issue_number FROM github_exports").get().issue_number,42);
});
