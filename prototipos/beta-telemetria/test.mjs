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
    headers:{"content-type":"application/json","cf-connecting-ip":"198.51.100.5"},
    body:JSON.stringify(good)});
  assert.equal((await worker.fetch(request,{PILOT_ENABLED:"no"})).status,503);
});
test("receptor limitado acumula somente agregados com 202", async () => {
  const seen=[];
  const bind = function(...args){return {run:async()=>{seen.push(args)}};};
  const env={PILOT_ENABLED:"yes",BETA_RATE_LIMITER:{limit:async()=>({success:true})},
    DB:{prepare:()=>({bind}),batch:async commands=>Promise.all(commands.map(c=>c.run()))}};
  const request = new Request("https://private.example/ingest", {method:"POST",
    headers:{"content-type":"application/json","cf-connecting-ip":"198.51.100.5"},
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
    headers:{"content-type":"application/json","cf-connecting-ip":"198.51.100.5"},
    body:JSON.stringify(good)});
  assert.equal((await worker.fetch(request,base)).status,429);
});
