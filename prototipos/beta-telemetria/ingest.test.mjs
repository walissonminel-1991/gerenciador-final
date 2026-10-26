import test, { before, after } from "node:test";
import assert from "node:assert/strict";
import worker, { validateReport } from "./worker.mjs";
import { allowed, betaVersion, boundedText, expiresForDay } from "./policy.mjs";
import { fixture } from "./test-support.mjs";

const good={schema:1,version:betaVersion,exit:"normal",counts:{"MSIX.LEITURA":2}};
const realFetch=globalThis.fetch;
before(()=>{globalThis.fetch=async()=>{throw new Error("TEST_NETWORK_FORBIDDEN");};});
after(()=>{globalThis.fetch=realFetch;});
function request(body=JSON.stringify(good),headers={},path="/ingest",method="POST") {
  const bytes=typeof body==="string"?Buffer.byteLength(body):body.byteLength;
  return new Request("https://receiver.invalid"+path,{method,
    headers:{"content-type":"application/json","cf-connecting-ip":"198.51.100.5","content-length":String(bytes),...headers},
    ...(method==="POST"?{body}:{})});
}
function ingestion(f){return {...f.env,PILOT_ENABLED:"yes",BETA_RATE_LIMITER:{limit:async()=>({success:true})}};}

test("somente versao BETA revisada pode criar chaves D1",()=>{
  for(const version of ["v5.13.11.65","v5.13.11.67","v999.9999.9999.9999",betaVersion+"\n",betaVersion+" "])
    assert.equal(validateReport({...good,version}),null);
});

test("payload nao admite textos, metadados, arrays ou contagens ambiguas",()=>{
  const invalid=[null,[],true,{}, {...good,ip:"198.51.100.5"}, {...good,consent:true},
    {...good,counts:[]}, {...good,counts:{"MSIX.LEITURA":true}}, {...good,counts:{"MSIX.LEITURA":1.1}},
    {...good,counts:{"MSIX.LEITURA":0}}, {...good,counts:{"MSIX.LEITURA":-1}},
    {...good,counts:{"MSIX.LEITURA":"2"}}, {...good,counts:{"MSIX.LEITURA":{path:"fixture"}}}];
  for(const value of invalid)assert.equal(validateReport(value),null);
  assert.equal(validateReport({...good,counts:Object.fromEntries([...allowed].map(x=>[x,100]))}).counts.length,14);
});

for(const [name,headers,status] of [["tipo",{"content-type":"text/plain"},415],
  ["tamanho ausente",{"content-length":""},413], ["tamanho excessivo",{"content-length":"5000"},413],
  ["sem IP",{"cf-connecting-ip":""},403]]) {
  test("rejeita antes de gravar: "+name,async t=>{
    const f=fixture(t);
    assert.equal((await worker.fetch(request(undefined,headers),ingestion(f))).status,status);
    assert.equal(f.db.sqlite.prepare("SELECT count(*) AS n FROM daily_counts").get().n,0);
  });
}

test("rota e metodo nao autorizados nao chamam limitador ou D1",async()=>{
  const env={};
  assert.equal((await worker.fetch(request(undefined,{},"/admin"),env)).status,404);
  assert.equal((await worker.fetch(request(undefined,{},"/ingest","GET"),env)).status,405);
});

test("limitador indisponivel retorna 503 sem armazenar IP ou payload",async t=>{
  const f=fixture(t),env=ingestion(f);
  env.BETA_RATE_LIMITER.limit=async()=>{throw new Error("fixture_failure");};
  assert.equal((await worker.fetch(request(),env)).status,503);
  assert.equal(f.db.sqlite.prepare("SELECT count(*) AS n FROM daily_counts").get().n,0);
});

test("UTF-8 e JSON invalidos falham sem escrita",async t=>{
  const f=fixture(t),env=ingestion(f);
  for(const body of ["{",new Uint8Array([0xff,0xff])])assert.equal((await worker.fetch(request(body),env)).status,400);
  assert.equal(f.db.sqlite.prepare("SELECT count(*) AS n FROM daily_counts").get().n,0);
});

test("SQL de ingestao usa transacao real e rollback se uma categoria falhar",async t=>{
  const f=fixture(t),env=ingestion(f);
  f.db.fault=(_sql,args)=>{if(args[2]==="APP.FALHA_INTERFACE")throw new Error("synthetic_d1");};
  const body=JSON.stringify({...good,counts:{"MSIX.LEITURA":2,"APP.FALHA_INTERFACE":1}});
  assert.equal((await worker.fetch(request(body),env)).status,503);
  assert.equal(f.db.sqlite.prepare("SELECT count(*) AS n FROM daily_counts").get().n,0);
});

test("falha na preparacao D1 retorna resposta generica 503",async t=>{
  const f=fixture(t),env=ingestion(f);
  env.DB={prepare(){throw new Error("fixture_failure");}};
  assert.equal((await worker.fetch(request(),env)).status,503);
});

test("acumula e satura total sem renovar prazo nem guardar IP",async t=>{
  const f=fixture(t),env=ingestion(f);t.mock.method(Date,"now",f.runtime.now);
  assert.equal((await worker.fetch(request(),env)).status,202);
  f.db.sqlite.prepare("UPDATE daily_counts SET total=9999999").run();
  f.advance(60);
  assert.equal((await worker.fetch(request(),env)).status,202);
  const row=f.db.sqlite.prepare("SELECT * FROM daily_counts").get();
  assert.equal(row.total,10000000);assert.equal(row.expires_at,expiresForDay(f.today));
  assert.deepEqual(Object.keys(row).sort(),["day","version","event","total","expires_at"].sort());
  assert.doesNotMatch(JSON.stringify(row),/198\.51\.100/);
});

test("stream lento e cancelado por prazo total sem esperar bytes ilimitados",async()=>{
  let cancelled=false;
  const stream=new ReadableStream({cancel(){cancelled=true;}});
  assert.equal(await boundedText(stream,4096,10),null);
  assert.equal(cancelled,true);
});

test("falha na limpeza local ainda exclui Issue, mas impede nova exportacao",async t=>{
  const f=fixture(t);f.count();f.enable();f.job(f.today-8,f.remote.add(f.today-8));
  t.mock.method(Date,"now",f.runtime.now);t.mock.method(globalThis,"fetch",f.remote.fetch);
  f.db.fault=sql=>{if(sql.startsWith("DELETE FROM daily_counts"))throw new Error("synthetic_d1");};
  await assert.rejects(worker.scheduled(null,f.env),/^Error: GF_BETA_MAINTENANCE_database$/);
  assert.equal(f.remote.deleted.length,1);assert.equal(f.remote.created.length,0);
});

test("cron com falha remota gera somente codigo de erro sanitizado",async t=>{
  const f=fixture(t);f.job(f.today-8,f.remote.add(f.today-8));
  t.mock.method(Date,"now",f.runtime.now);t.mock.method(globalThis,"fetch",async()=>{throw new Error("SENSITIVE_FIXTURE_ONLY");});
  await assert.rejects(worker.scheduled(null,f.env),/^Error: GF_BETA_MAINTENANCE_network$/);
  assert.equal(f.jobs().length,1);
});

test("cron respeita teto combinado de 50 operacoes SQL/API inclusive controle",async t=>{
  const f=fixture(t);for(let i=1;i<=45;i++)f.remote.add(f.today-8,i);
  f.remote.intercept=call=>{if(call.query.startsWith("mutation"))f.advance(1);};
  t.mock.method(Date,"now",f.runtime.now);t.mock.method(globalThis,"fetch",f.remote.fetch);
  let sqlCalls=0;f.db.fault=()=>{sqlCalls++;};
  await assert.rejects(worker.scheduled(null,f.env),/GF_BETA_MAINTENANCE_budget/);
  assert.ok(sqlCalls+f.remote.calls.length<=50);
  assert.ok(f.remote.issues.length>0);assert.equal(f.remote.created.length,0);
});
