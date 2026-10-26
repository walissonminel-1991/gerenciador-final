import test from "node:test";
import assert from "node:assert/strict";
import worker, { validateReport } from "./worker.mjs";
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
test("receptor limitado acumula somente agregados com 202", async () => {
  const seen=[];
  const bind = function(...args){return {run:async()=>{seen.push(args)}};};
  const env={PILOT_ENABLED:"yes",BETA_RATE_LIMITER:{limit:async()=>({success:true})},
    DB:{prepare:()=>({bind}),batch:async commands=>Promise.all(commands.map(c=>c.run()))}};
  const request = new Request("https://private.example/ingest", {method:"POST",
    headers:{"content-type":"application/json","cf-connecting-ip":"198.51.100.5","content-length":String(Buffer.byteLength(JSON.stringify(good)))},
    body:JSON.stringify(good)});
  assert.equal((await worker.fetch(request,env)).status,202);
  assert.equal(seen.length,1);
  assert.equal(seen[0][2],"MSIX.LEITURA");
  assert.equal(seen[0][3],2);
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

test("limpeza inclui metadados de exportacao mesmo com piloto desligado", async () => {
  const sql=[];
  const env={PILOT_ENABLED:"no",DB:{prepare:statement=>{
    sql.push(statement); return {bind:()=>({run:async()=>{}})};
  }}};
  await worker.scheduled(null,env);
  env.PILOT_ENABLED = "yes";
  env.GITHUB_EXPORT_ENABLED = "no";
  await worker.scheduled(null,env);
  assert.equal(sql.length,4);
  assert.match(sql[0],/DELETE FROM daily_counts/);
  assert.match(sql[1],/DELETE FROM github_exports/);
  assert.match(sql[3],/DELETE FROM github_exports/);
});
