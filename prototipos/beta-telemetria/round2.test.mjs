import test,{before,after} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {fixture,LocalD1,json} from "./test-support.mjs";
import worker from "./worker.mjs";
import {runMaintenance} from "./github-retention.mjs";
import {readHealth,healthFromSnapshot,logHealth} from "./observability.mjs";
import observer from "./health-observer.mjs";
import {inspectSchema,migrationPlan} from "./schema-migration.mjs";
import {parseStrictJson} from "./strict-json.mjs";
import {betaVersion} from "./policy.mjs";
const originalFetch=globalThis.fetch;
before(()=>{globalThis.fetch=async()=>{throw new Error("TEST_NETWORK_FORBIDDEN");};});
after(()=>{globalThis.fetch=originalFetch;});
const run=f=>runMaintenance(f.env,f.runtime);
const good={schema:1,version:betaVersion,exit:"normal",counts:{"MSIX.LEITURA":2}};
const req=(body=JSON.stringify(good),headers={})=>new Request("https://receiver.invalid/ingest",{
  method:"POST",headers:{"content-type":"application/json","content-length":String(Buffer.byteLength(body)),
    "cf-connecting-ip":"198.51.100.5",...headers},body});
const ingestEnv=f=>({...f.env,PILOT_ENABLED:"yes",BETA_RATE_LIMITER:{limit:async()=>({success:true})}});

for(const edit of ["title","body","both"]){
  test("POST confirmado no GitHub, resposta perdida e adulteracao: "+edit,async t=>{
    const f=fixture(t);f.count();f.enable();f.remote.loseCreateResponse=true;
    assert.equal((await run(f)).code,"network");
    const issue=f.remote.created[0],original={title:issue.title,body:issue.body};
    if(edit!=="body")issue.title="Unrecognizable";
    if(edit!=="title")issue.body="Edited externally without marker";
    f.remote.loseCreateResponse=false;f.advance(120);
    const result=await run(f);
    if(edit==="title"){
      // O marcador e o autor ainda provam a operacao, apesar do titulo.
      assert.equal(result.code,"idle");assert.equal(f.jobs()[0].issue_number,issue.number);
    }else{
      assert.equal(result.code,"review_required");assert.equal(f.jobs()[0].status,"review_required");
      for(let n=0;n<3;n++){f.advance(3*86400);await run(f);assert.equal(f.jobs().length,1);}
      assert.equal(f.remote.created.length,1);assert.equal(f.remote.deleted.length,0);
      // Recuperacao posterior so depois de voltar a existir prova positiva.
      Object.assign(issue,original);await run(f);
      assert.equal(f.jobs().length,0);assert.equal(f.remote.deleted.length,1);
    }
  });
}

test("intencao v2 sem nonce jamais e descartada por falta de texto",async t=>{
  const f=fixture(t);f.job(f.today-30,null);f.enable();
  for(let n=0;n<4;n++){await run(f);f.advance(86400);}
  assert.equal(f.jobs().length,1);assert.equal(f.jobs()[0].status,"review_required");
  assert.equal(f.remote.created.length,0);assert.equal(f.remote.deleted.length,0);
});

test("registro prepared de backup tambem nao autoriza aposentar obrigacao",async t=>{
  const f=fixture(t);f.job(f.today-30,null);f.db.sqlite.exec("UPDATE github_exports SET status='prepared'");
  await run(f);assert.equal(f.jobs().length,1);assert.equal(f.remote.created.length,0);
});

test("marcador copiado por outro ator nao autoriza exclusao",async t=>{
  const f=fixture(t),a=f.remote.add(f.today-1,1);f.job(f.today-1,a);
  const other=f.remote.add(f.today-1,2);other.body=a.body;other.author={id:"ANOTHER_ACTOR"};
  assert.equal((await run(f)).code,"review_required");
  assert.equal(f.remote.deleted.length,0);assert.equal(f.jobs()[0].issue_number,1);
});

test("um nonce diferente nao e uma duplicata comprovada",async t=>{
  const f=fixture(t),a=f.remote.add(f.today-1,1);f.job(f.today-1,a);f.remote.add(f.today-1,2);
  assert.equal((await run(f)).code,"review_required");assert.equal(f.remote.deleted.length,0);
});

test("restore anterior a intencao exige triagem para Issue remota sem ledger",async t=>{
  const f=fixture(t);f.count();f.enable();await run(f);
  f.db.sqlite.exec("DELETE FROM github_exports");
  assert.equal((await run(f)).code,"review_required");
  assert.equal(f.remote.created.length,1);assert.equal(f.remote.deleted.length,0);
});

test("capacidade do ledger bloqueia nova criacao, sem truncar historico",async t=>{
  const f=fixture(t);for(let i=0;i<32;i++)f.job(f.today+1+i,{number:i+100,id:"LEGACY_"+i});
  f.count();f.enable();assert.equal((await run(f)).code,"ledger_capacity");
  assert.equal(f.jobs().length,32);assert.equal(f.remote.created.length,0);
});

test("nonce/autor nao sao identificadores de PC nem aparecem nos alertas",async t=>{
  const f=fixture(t);f.count();f.enable();await run(f);
  const record=f.jobs()[0],snapshot=await readHealth(f.db,Math.floor(f.runtime.now()/1000),f.env.GITHUB_TOKEN_EXPIRES_AT);
  assert.match(record.operation_id,/^[0-9a-f-]{36}$/);
  assert.doesNotMatch(JSON.stringify(snapshot),new RegExp(record.operation_id+"|"+record.creator_id+"|REPO_FIXTURE|ISSUE_NEW"));
});

test("perda do lease antes de mutacao impede DELETE e preserva referencia",async t=>{
  const f=fixture(t);f.job(f.today-8,f.remote.add(f.today-8));
  f.remote.intercept=call=>{if(call.query.startsWith("query Lookup"))f.db.sqlite.exec("UPDATE github_maintenance SET lease_token='other'");};
  assert.equal((await run(f)).code,"lease_lost");
  assert.equal(f.remote.deleted.length,0);assert.equal(f.jobs().length,1);
});

test("interrupcao apos marcar uncertain nao pode levar a um segundo POST",async t=>{
  const f=fixture(t);f.count();f.enable();
  f.remote.intercept=call=>{if(call.query.startsWith("mutation Create"))throw new Error("synthetic_interruption");};
  assert.equal((await run(f)).code,"network");f.remote.intercept=null;f.advance(601);
  assert.equal((await run(f)).code,"review_required");
  assert.equal(f.remote.calls.filter(c=>c.query.startsWith("mutation Create")).length,1);
});

test("cursor GraphQL com erro generico tambem e invalidado sem perder obrigacoes",async t=>{
  const f=fixture(t);f.job(f.today-3,null);
  f.db.sqlite.prepare("UPDATE github_maintenance SET scan_cursor='bad',scan_started=?").run(Math.floor(f.runtime.now()/1000));
  f.remote.intercept=c=>c.query.startsWith("query Scan")?json({errors:[{type:"OTHER_GRAPHQL_ERROR"}]}):null;
  assert.equal((await run(f)).code,"graphql");assert.equal(f.state().scan_cursor,null);assert.equal(f.jobs().length,1);
});

test("cursor antigo reinicia a busca sem inferir inexistencia",async t=>{
  const f=fixture(t);f.job(f.today-8,null);
  f.db.sqlite.exec("UPDATE github_maintenance SET scan_cursor='999',scan_started=1");
  await run(f);assert.equal(f.remote.calls.find(c=>c.query.startsWith("query Scan")).variables.after,null);
  assert.equal(f.jobs().length,1);
});

for(const [status,code] of [[401,"authentication"],[403,"authorization"],[404,"http"],[500,"http"]]){
  test("falhas consecutivas preservam referencia e classificam HTTP "+status,async t=>{
    const f=fixture(t);f.job(f.today-8,f.remote.add(f.today-8));f.remote.intercept=()=>json({},status);
    for(let n=0;n<4;n++){assert.equal((await run(f)).code,code);f.advance(60*2**n);}
    assert.equal(f.jobs().length,1);assert.equal(f.state().failures,4);assert.equal(f.state().last_error,code);
  });
}

test("credencial perto da expiracao bloqueia criar mas continua excluindo",async t=>{
  const f=fixture(t);f.enable();f.count();f.env.GITHUB_TOKEN_EXPIRES_AT="2026-10-30";
  f.job(f.today-8,f.remote.add(f.today-8));
  assert.equal((await run(f)).code,"credential_expiry");
  assert.equal(f.remote.deleted.length,1);assert.equal(f.remote.created.length,0);
});

test("metadata expirada nao descarta obrigacao nem dispensa tentativa de DELETE",async t=>{
  const f=fixture(t);f.env.GITHUB_TOKEN_EXPIRES_AT="2026-10-20";f.job(f.today-8,f.remote.add(f.today-8));
  assert.equal((await run(f)).code,"cleanup_only");assert.equal(f.remote.deleted.length,1);
});

test("backlog nao atualiza ultima manutencao completa com sucesso",async t=>{
  const f=fixture(t);for(let i=0;i<9;i++)f.job(f.today-8-i,f.remote.add(f.today-8-i,i+1));
  f.db.sqlite.exec("UPDATE github_maintenance SET last_success=100");
  assert.equal((await run(f)).code,"backlog");assert.equal(f.state().last_success,100);
  const health=await readHealth(f.db,Math.floor(f.runtime.now()/1000),f.env.GITHUB_TOKEN_EXPIRES_AT);
  assert.equal(health.overdue,5);assert.ok(health.alerts.includes("retention_overdue"));
});

test("observador independente detecta ausencia de cron e nao expoe colunas extras",async t=>{
  const f=fixture(t),now=Math.floor(f.runtime.now()/1000);
  f.db.sqlite.exec("UPDATE github_maintenance SET last_started=1,last_error='secret-input',lease_token='SECRET' ");
  const snapshot=await readHealth(f.db,now,f.env.GITHUB_TOKEN_EXPIRES_AT);
  assert.ok(snapshot.alerts.includes("cron_missing"));assert.equal(snapshot.last_error,"database");
  assert.doesNotMatch(JSON.stringify(snapshot),/SECRET|secret-input|repo_name|lease_token|ticket/);
  const seen=[];logHealth(snapshot,"no",x=>seen.push(x));assert.equal(seen.length,0);
  logHealth(snapshot,"yes",x=>seen.push(x));assert.equal(seen.length,1);
});

test("observador esta desligado por padrao, inclusive sem DB",async()=>{
  await observer.scheduled(null,{});
});

for(const [date,alert] of [["2026-11-05","credential_rotation_due"],["2026-10-25","credential_expired"],[null,"credential_expiry_unknown"]]){
  test("alerta de rotacao sem ler o token: "+alert,()=>{
    const state={last_started:1,last_success:1,last_error:"none",last_code:"idle"};
    assert.ok(healthFromSnapshot(state,{},Date.parse("2026-10-26T00:00:00Z")/1000,date).alerts.includes(alert));
  });
}

test("JSON com chaves repetidas simples ou escapadas e rejeitado",async t=>{
  const f=fixture(t);
  for(const body of [JSON.stringify(good).replace('"schema":1','"schema":1,"schema":1'),
    JSON.stringify(good).replace('"MSIX.LEITURA":2','"MSIX.LEITURA":2,"MSIX.\\u004cEITURA":3')]){
    assert.equal(parseStrictJson(body),null);assert.equal((await worker.fetch(req(body),ingestEnv(f))).status,400);
  }
  assert.equal(f.db.sqlite.prepare("SELECT count(*) AS n FROM daily_counts").get().n,0);
});

test("JSON profundo, NaN, trailing e caracteres de controle falham fechados",()=>{
  for(const value of ['{"x":NaN}','{} true','[[[[[[]]]]]]','{"x":"\u0001"}', '"unterminated'])assert.equal(parseStrictJson(value),null);
  assert.equal(parseStrictJson('{"a":1,"b":[true,false,null,"escaped\\n"]}').a,1);
});

test("Content-Length numericamente equivalente mas nao canonico e rejeitado",async t=>{
  const f=fixture(t);for(const value of ["+100","1e2","0100"])
    assert.equal((await worker.fetch(req(undefined,{"content-length":value}),ingestEnv(f))).status,413);
});

test("limite global por PoP e consultado e nenhuma falha chega ao D1",async t=>{
  const f=fixture(t),keys=[],env=ingestEnv(f);
  env.BETA_RATE_LIMITER.limit=async({key})=>{keys.push(key);return{success:keys.length===1};};
  assert.equal((await worker.fetch(req(),env)).status,429);assert.equal(keys[1],"gf-beta-global-v1");
  assert.equal(f.db.sqlite.prepare("SELECT count(*) AS n FROM ingest_budget").get().n,0);
});

test("payload valido repetido respeita teto global de 1000 commits de relatorios",async t=>{
  const f=fixture(t),env=ingestEnv(f),day=Math.floor(Date.now()/86400000);
  f.db.sqlite.prepare("INSERT INTO ingest_budget VALUES(?,999,1998,?,NULL)").run(day,(day+7)*86400);
  assert.equal((await worker.fetch(req(),env)).status,202);
  for(let n=0;n<4;n++)assert.equal((await worker.fetch(req(),env)).status,429);
  const budget=f.db.sqlite.prepare("SELECT * FROM ingest_budget").get();
  assert.equal(budget.reports,1000);assert.equal(budget.event_total,2000);assert.equal(budget.ticket,null);
  assert.equal(f.db.sqlite.prepare("SELECT total FROM daily_counts").get().total,2);
});

test("orcamento e contagens fazem rollback juntos em falha D1",async t=>{
  const f=fixture(t);f.db.fault=sql=>{if(sql.startsWith("INSERT INTO daily_counts"))throw new Error("fixture");};
  assert.equal((await worker.fetch(req(),ingestEnv(f))).status,503);
  assert.equal(f.db.sqlite.prepare("SELECT count(*) AS n FROM ingest_budget").get().n,0);
});

test("eventos e relatorios sao medidos separadamente; nao ha coluna de participantes",async t=>{
  const f=fixture(t),env=ingestEnv(f);
  await worker.fetch(req(),env);await worker.fetch(req(),env);
  const metric=f.db.sqlite.prepare("SELECT * FROM ingest_budget").get();
  assert.equal(metric.reports,2);assert.equal(metric.event_total,4);
  assert.doesNotMatch(JSON.stringify(metric),/participant|user|IP|198\.51/);
});

const legacy=`CREATE TABLE daily_counts(day INTEGER NOT NULL,version TEXT NOT NULL,event TEXT NOT NULL,
 total INTEGER NOT NULL DEFAULT 0,expires_at INTEGER NOT NULL,PRIMARY KEY(day,version,event));
 CREATE INDEX beta_expiry ON daily_counts(expires_at);
 CREATE TABLE github_exports(day INTEGER PRIMARY KEY,issue_number INTEGER NOT NULL,exported_at INTEGER NOT NULL);
 INSERT INTO github_exports VALUES(20000,0,100);INSERT INTO github_exports VALUES(20001,42,100);`;
async function migrate(db){const plan=migrationPlan(await inspectSchema(db));await db.batch(plan.map(s=>db.prepare(s)));return plan;}

for(const start of ["v1","v2","partial"]){
  test("migracao preserva referencias, permite repetir e recupera "+start,async t=>{
    const db=new LocalD1(legacy);t.after(()=>db.close());
    if(start==="v2")db.sqlite.exec(readFileSync(new URL("./migrations/0002_github_retention.sql",import.meta.url),"utf8"));
    if(start==="partial")db.sqlite.exec("ALTER TABLE github_exports ADD COLUMN issue_node_id TEXT;ALTER TABLE github_exports ADD COLUMN expires_at INTEGER NOT NULL DEFAULT 0;");
    await migrate(db);await migrate(db);
    const rows=db.sqlite.prepare("SELECT * FROM github_exports ORDER BY day").all();
    assert.equal(rows.length,2);assert.equal(rows[1].issue_number,42);assert.equal(rows[0].status,"review_required");
    assert.equal(rows[1].expires_at,(20001+7)*86400);
    assert.equal(db.sqlite.prepare("SELECT schema_version FROM github_maintenance").get().schema_version,3);
  });
}

test("falha em cada etapa da migracao atomica reverte DDL e referencias",async t=>{
  const initial=new LocalD1(legacy);t.after(()=>initial.close());const plan=migrationPlan(await inspectSchema(initial));
  for(let at=0;at<plan.length;at++){
    const db=new LocalD1(legacy);let calls=0;
    db.fault=(_sql,_args,mode)=>{if(mode==="run"&&calls++===at)throw new Error("synthetic_migration_fault");};
    await assert.rejects(db.batch(plan.map(s=>db.prepare(s))));db.fault=null;
    assert.deepEqual(await inspectSchema(db),await inspectSchema(initial));
    assert.equal(db.sqlite.prepare("SELECT count(*) AS n FROM github_exports").get().n,2);db.close();
  }
});

test("schema ou indice incompatível bloqueia plano sem perder referencias",async t=>{
  const db=new LocalD1(legacy);t.after(()=>db.close());
  db.sqlite.exec("CREATE INDEX beta_github_expiry ON github_exports(exported_at)");
  assert.throws(()=>migrationPlan({tables:{},indexes:[],invalid:1}),/schema_invalid_rows/);
  const snapshot=await inspectSchema(db);
  assert.throws(()=>migrationPlan(snapshot),/schema_index_conflict/);
  assert.equal(db.sqlite.prepare("SELECT count(*) AS n FROM github_exports").get().n,2);
});

test("duplicata com erro HTTP no delete conserva ID mesmo apos editar seu texto",async t=>{
  const f=fixture(t),canonical=f.remote.add(f.today-1,1);f.job(f.today-1,canonical);
  const duplicate=f.remote.add(f.today-1,2);duplicate.body=canonical.body;
  f.remote.intercept=c=>c.query.startsWith("mutation Delete")?json({},500):null;
  assert.equal((await run(f)).code,"http");
  assert.equal(f.db.sqlite.prepare("SELECT issue_number FROM github_delete_refs").get().issue_number,2);
  duplicate.body="edited after failed delete";duplicate.title="edited";f.remote.intercept=null;f.advance(120);
  await run(f);
  assert.equal(f.remote.deleted[0].number,2);assert.equal(f.jobs()[0].issue_number,1);
  assert.equal(f.db.sqlite.prepare("SELECT count(*) AS n FROM github_delete_refs").get().n,0);
});

test("duplicatas expiradas conservam prova depois de excluir a Issue canonica",async t=>{
  const f=fixture(t),canonical=f.remote.add(f.today-8,1);f.job(f.today-8,canonical);
  f.remote.add(f.today-8,2).body=canonical.body;
  await run(f);
  assert.equal(f.remote.deleted.length,2);assert.equal(f.jobs().length,0);
});

test("resposta perdida ao excluir duplicata nao apaga sua referencia",async t=>{
  const f=fixture(t),canonical=f.remote.add(f.today-1,1);f.job(f.today-1,canonical);
  f.remote.add(f.today-1,2).body=canonical.body;f.remote.loseDeleteResponse=true;
  assert.equal((await run(f)).code,"network");
  assert.equal(f.db.sqlite.prepare("SELECT count(*) AS n FROM github_delete_refs").get().n,1);
  f.remote.loseDeleteResponse=false;f.advance(120);await run(f);
  assert.equal(f.remote.deleted.length,1);assert.equal(f.jobs().length,1);
  assert.equal(f.db.sqlite.prepare("SELECT count(*) AS n FROM github_delete_refs").get().n,0);
});

test("lease sem margem para concluir a chamada bloqueia mutacao",async t=>{
  const f=fixture(t);f.job(f.today-8,f.remote.add(f.today-8));
  f.remote.intercept=c=>{if(c.query.startsWith("query Lookup"))f.db.sqlite.prepare("UPDATE github_maintenance SET lease_until=?").run(Math.floor(f.runtime.now()/1000)+1);};
  assert.equal((await run(f)).code,"lease_lost");assert.equal(f.remote.deleted.length,0);
});

test("indice unico em nonce duplicado aborta migracao sem truncar referencias",async t=>{
  const db=new LocalD1(legacy);t.after(()=>db.close());
  db.sqlite.exec("ALTER TABLE github_exports ADD COLUMN operation_id TEXT;UPDATE github_exports SET operation_id='duplicated';");
  await assert.rejects(migrate(db));
  assert.equal(db.sqlite.prepare("SELECT count(*) AS n FROM github_exports").get().n,2);
  assert.equal((await inspectSchema(db)).tables.github_maintenance.length,0);
});
