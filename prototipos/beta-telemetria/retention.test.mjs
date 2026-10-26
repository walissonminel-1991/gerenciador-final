import test, { before, after } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { runMaintenance } from "./github-retention.mjs";
import { expiresForDay, managedDay, issueContent, betaVersion } from "./policy.mjs";
import { fixture, LocalD1, json } from "./test-support.mjs";

// Qualquer chamada esquecida fora do simulador falha; nao ha acesso a rede real.
const realFetch = globalThis.fetch;
before(() => { globalThis.fetch = async () => { throw new Error("TEST_NETWORK_FORBIDDEN"); }; });
after(() => { globalThis.fetch = realFetch; });
const run = f => runMaintenance(f.env, f.runtime);

test("migracao v1 preserva obrigacoes antigas e calcula prazo desde coleta", t => {
  const db = new LocalD1(`CREATE TABLE daily_counts(day INTEGER,version TEXT,event TEXT,total INTEGER,expires_at INTEGER);
    CREATE TABLE github_exports(day INTEGER PRIMARY KEY,issue_number INTEGER NOT NULL,exported_at INTEGER NOT NULL);
    INSERT INTO github_exports VALUES(20000,42,9999999999);`);
  t.after(() => db.close());
  db.sqlite.exec(readFileSync(new URL("./migrations/0002_github_retention.sql", import.meta.url), "utf8"));
  const row = db.sqlite.prepare("SELECT * FROM github_exports").get();
  assert.equal(row.issue_number,42);
  assert.equal(row.expires_at,expiresForDay(20000));
  assert.equal(row.issue_node_id,null);
  assert.equal(db.sqlite.prepare("SELECT last_code FROM github_maintenance").get().last_code,"never");
});

test("exclusao real da Issue expirada mesmo com ambas flags no", async t => {
  const f = fixture(t), issue = f.remote.add(f.today-7);
  f.job(f.today-7,issue);
  assert.equal((await run(f)).code,"cleanup_only");
  assert.equal(f.remote.deleted.length,1);
  assert.equal(f.jobs().length,0);
  assert.equal(f.remote.created.length,0);
});

test("prazo nao e estendido por exportacao tardia e expira na fronteira", async t => {
  const f = fixture(t);
  const day = f.today-6, issue = f.remote.add(day);
  f.job(day,issue);
  await run(f);
  assert.equal(f.remote.deleted.length,0);
  assert.equal(f.jobs()[0].expires_at,expiresForDay(day));
  f.advance(12*3600); // exatamente meia-noite do setimo dia
  await run(f);
  assert.equal(f.remote.deleted.length,1);
});

test("Issue legada sem prova de propriedade exige revisao administrativa", async t => {
  const f = fixture(t);
  f.remote.add(f.today-9,1,true).state="CLOSED";
  await run(f);
  assert.equal(f.remote.deleted.length,0);
  assert.equal(f.state().unverified_issues,1);
  assert.equal(f.jobs().length,0);
});

test("titulo semelhante, texto adulterado, PR e data invalida nao autorizam exclusao", async t => {
  const f = fixture(t), old=f.today-8;
  f.remote.add(old,1).body += "\ntexto livre";
  f.remote.add(old,2).pull_request = {};
  f.remote.add(old,3).title = "GF BETA — estatísticas agregadas 2026-02-31";
  f.remote.add(old,4).body = "conteudo de outra pessoa";
  await run(f);
  assert.equal(f.remote.deleted.length,0);
  assert.equal(f.remote.issues.length,4);
});

test("recupera acima da primeira pagina sem duplicar exportacao", async t => {
  const f=fixture(t);
  for(let i=1;i<=105;i++) f.remote.issues.push({number:i,id:"OTHER_"+i,title:"Other",body:""});
  const issue=f.remote.add(f.today-1,106);
  f.intent(f.today-1,issue);
  f.count(); f.enable();
  assert.equal((await run(f)).code,"scanning");
  assert.equal((await run(f)).code,"idle");
  assert.equal(f.remote.calls.filter(c=>c.query.startsWith("query Scan")).length,5);
  assert.equal(f.jobs()[0].issue_number,issue.number);
  assert.equal(f.remote.created.length,0);
});

test("varredura interrompida persiste cursor e so cria apos concluir", async t => {
  const f=fixture(t);
  for(let i=1;i<=305;i++) f.remote.issues.push({number:i,id:"OTHER_"+i,title:"Other",body:""});
  f.intent(f.today-8,f.remote.add(f.today-8,306));
  f.count();f.enable();
  assert.equal((await run(f)).code,"scanning");
  assert.equal(f.state().scan_cursor,"75");
  for(let n=0;n<3;n++){assert.equal((await run(f)).code,"scanning");assert.equal(f.remote.created.length,0);}
  assert.equal(f.remote.created.length,0);
  assert.equal((await run(f)).code,"exported");
  assert.equal(f.state().scan_cursor,null);
  assert.equal(f.remote.deleted.length,1);
  assert.equal(f.remote.created.length,1);
});

test("cria no maximo uma Issue por dia; suprime baixos totais e arredonda", async t => {
  const f=fixture(t);
  f.count(f.today-1,17);f.count(f.today-1,4,"APP.FALHA_INTERFACE");f.enable();
  assert.equal((await run(f)).code,"exported");
  assert.equal((await run(f)).code,"idle");
  assert.equal(f.remote.created.length,1);
  assert.match(f.remote.created[0].body,/MSIX.LEITURA: 15/);
  assert.doesNotMatch(f.remote.created[0].body,/APP.FALHA_INTERFACE/);
  assert.equal(f.jobs()[0].expires_at,expiresForDay(f.today-1));
});

test("dia apenas com contagens pequenas nao publica", async t => {
  const f=fixture(t);f.count(f.today-1,4);f.enable();
  assert.equal((await run(f)).code,"suppressed");
  assert.equal(f.remote.created.length,0);
});

test("nao exporta dia corrente nem dados expirados; recupera dia perdido dentro do prazo", async t => {
  const f=fixture(t);f.count(f.today);f.count(f.today-8);f.count(f.today-3);f.enable();
  assert.equal((await run(f)).code,"exported");
  assert.equal(managedDay(f.remote.created[0]),f.today-3);
});

for (const flags of [["no","no"],["yes","no"],["no","yes"]]) {
  test("flags separadas impedem criar: "+flags.join("/"), async t => {
    const f=fixture(t);f.count();[f.env.PILOT_ENABLED,f.env.GITHUB_EXPORT_ENABLED]=flags;
    await run(f);
    assert.equal(f.remote.created.length,0);
  });
}

for(const [name,change] of [["publico",f=>{f.remote.private=false;}],
  ["permissao insuficiente",f=>{f.remote.permission="WRITE";}],
  ["destino trocado",f=>{f.db.sqlite.prepare("UPDATE github_maintenance SET repo_id='OLD_REPO'").run();}]]) {
  test("falha fechada: "+name, async t=>{
    const f=fixture(t);f.job(f.today-8,f.remote.add(f.today-8));f.count();f.enable();change(f);
    const result=await run(f);
    assert.equal(result.ok,false);
    assert.equal(f.remote.created.length,0);
    assert.equal(f.remote.deleted.length,0);
    assert.equal(f.jobs().length,1);
  });
}

test("identidade da Issue alterada preserva obrigacao e bloqueia criacao", async t=>{
  const f=fixture(t), issue=f.remote.add(f.today-8);
  f.job(f.today-8,issue,{issue_node_id:"DIFFERENT_ISSUE"});f.count();f.enable();
  assert.equal((await run(f)).code,"identity_conflict");
  assert.equal(f.jobs().length,1);assert.equal(f.remote.deleted.length,0);assert.equal(f.remote.created.length,0);
});

test("Issue ausente: null autenticado exige triagem sem descartar referencia", async t=>{
  const f=fixture(t);f.job(f.today-8,{number:42,id:"ISSUE_42"});
  assert.equal((await run(f)).code,"absence_unproven");
  assert.equal(f.jobs().length,1);assert.equal(f.jobs()[0].status,"review_required");
  assert.equal(f.remote.deleted.length,0);
});

for(const [name,response] of [["403",()=>json({},403)], ["404",()=>json({},404)],
  ["500",()=>json({},500)], ["redirect",()=>json({},302,{location:"https://elsewhere.example"})],
  ["GraphQL parcial",()=>json({data:{repository:null},errors:[{type:"FORBIDDEN"}]})]]) {
  test("nao esquece Issue quando API responde "+name, async t=>{
    const f=fixture(t);f.job(f.today-8,f.remote.add(f.today-8));
    f.remote.intercept=call=>call.query.startsWith("query Lookup")?response():null;
    assert.equal((await run(f)).ok,false);
    assert.equal(f.jobs().length,1);assert.equal(f.remote.deleted.length,0);
    const calls=f.remote.calls.length;
    assert.equal((await run(f)).code,"cooldown");
    assert.equal(f.remote.calls.length,calls);
    assert.equal(f.state().failures,1);
  });
}

for(const [name,response,delay] of [
  ["429 Retry-After",()=>json({},429,{"retry-after":"7200"}),7200],
  ["Retry-After data HTTP",()=>json({},429,{"retry-after":"Mon, 26 Oct 2026 14:00:00 GMT"}),7200],
  ["GraphQL rate limit HTTP 200",()=>json({errors:[{type:"RATE_LIMITED"}]},200,{"x-ratelimit-remaining":"0","x-ratelimit-reset":String(Date.parse("2026-10-26T13:00:00Z")/1000)}),3600]
]) {
  test("persiste espera e nao insiste: "+name,async t=>{
    const f=fixture(t);f.remote.intercept=response;
    assert.equal((await run(f)).code,"rate_limit");
    assert.ok(f.state().retry_at>=Math.floor(f.runtime.now()/1000)+delay);
    const before=f.remote.calls.length;f.advance(59);
    assert.equal((await run(f)).code,"cooldown");assert.equal(f.remote.calls.length,before);
  });
}

test("GraphQL errors sem dados impede considerar delete como sucesso",async t=>{
  const f=fixture(t);f.job(f.today-8,f.remote.add(f.today-8));
  f.remote.intercept=call=>call.query.startsWith("mutation Delete")?json({errors:[{type:"FORBIDDEN"}]}):null;
  assert.equal((await run(f)).code,"graphql");assert.equal(f.jobs().length,1);
});

test("delete com resposta perdida conserva obrigacao para triagem sem recriar",async t=>{
  const f=fixture(t);f.job(f.today-8,f.remote.add(f.today-8));f.remote.loseDeleteResponse=true;
  assert.equal((await run(f)).code,"network");assert.equal(f.jobs().length,1);
  f.advance(120);f.remote.loseDeleteResponse=false;
  assert.equal((await run(f)).code,"absence_unproven");
  assert.equal(f.jobs().length,1);assert.equal(f.jobs()[0].status,"review_required");
  assert.equal(f.remote.deleted.length,1);assert.equal(f.remote.created.length,0);
});

test("create aplicado com resposta perdida e reconciliado sem duplicata",async t=>{
  const f=fixture(t);f.count();f.enable();f.remote.loseCreateResponse=true;
  assert.equal((await run(f)).code,"network");assert.equal(f.jobs()[0].issue_number,0);
  f.advance(120);f.remote.loseCreateResponse=false;
  assert.equal((await run(f)).code,"idle");
  assert.equal(f.remote.created.length,1);assert.ok(f.jobs()[0].issue_number>0);
});

test("falha D1 apos create preserva intento e proxima varredura recupera",async t=>{
  const f=fixture(t);f.count();f.enable();
  f.db.fault=sql=>{if(sql.startsWith("UPDATE github_exports SET issue_number="))throw new Error("synthetic_d1");};
  assert.equal((await run(f)).code,"database");assert.equal(f.jobs()[0].issue_number,0);
  f.db.fault=null;f.advance(120);await run(f);
  assert.equal(f.remote.created.length,1);assert.ok(f.jobs()[0].issue_number>0);
});

test("falha D1 apos delete usa tombstone confirmado ja persistido no retry",async t=>{
  const f=fixture(t);f.job(f.today-8,f.remote.add(f.today-8));
  f.db.fault=sql=>{if(sql.startsWith("DELETE FROM github_exports"))throw new Error("synthetic_d1");};
  assert.equal((await run(f)).code,"database");assert.equal(f.jobs().length,1);
  f.db.fault=null;f.advance(120);await run(f);
  assert.equal(f.jobs().length,0);assert.equal(f.remote.deleted.length,1);
});

test("falha ao persistir intento impede qualquer create",async t=>{
  const f=fixture(t);f.count();f.enable();
  f.db.fault=sql=>{if(sql.startsWith("INSERT INTO github_exports"))throw new Error("synthetic_d1");};
  assert.equal((await run(f)).code,"database");assert.equal(f.remote.created.length,0);
});

test("crons concorrentes usam lease; reinicio recupera lease vencido",async t=>{
  const f=fixture(t);f.count();f.enable();
  let release,started;
  const gate=new Promise(r=>{release=r;}), entered=new Promise(r=>{started=r;});
  f.remote.intercept=async call=>{if(call.query.startsWith("query Repository")){started();await gate;}};
  const first=run(f);await entered;
  assert.equal((await run(f)).code,"busy");release();
  assert.equal((await first).code,"exported");assert.equal(f.remote.created.length,1);
  f.remote.intercept=null;
  f.db.sqlite.prepare("UPDATE github_maintenance SET lease_token='killed',lease_until=?").run(Math.floor(f.runtime.now()/1000)+600);
  assert.equal((await run(f)).code,"busy");f.advance(601);
  assert.equal((await run(f)).code,"idle");
});

test("limite de trabalho, espaçamento de mutacoes e backlog impedem explosao API",async t=>{
  const f=fixture(t);
  for(let i=1;i<=18;i++){const day=f.today-8-i;f.job(day,f.remote.add(day,i));}
  f.count();f.enable();
  assert.equal((await run(f)).code,"backlog");assert.equal(f.remote.created.length,0);
  assert.equal(f.remote.deleted.length,4);assert.ok(f.remote.calls.length<=30);
  const mutations=f.remote.calls.filter(c=>c.query.startsWith("mutation"));
  for(let i=1;i<mutations.length;i++)assert.ok(mutations[i].at-mutations[i-1].at>=1000);
  for(let n=0;n<4;n++)await run(f);
  assert.equal(f.jobs().length,1);assert.equal(f.remote.created.length,1);assert.equal(f.remote.deleted.length,18);
});

test("cursor invalido reinicia sweep sem apagar ledger",async t=>{
  const f=fixture(t);f.db.sqlite.prepare("UPDATE github_maintenance SET scan_cursor='bad'").run();
  f.remote.intercept=call=>call.query.startsWith("query Scan")?json({errors:[{type:"INVALID_CURSOR_ARGUMENTS"}]}):null;
  assert.equal((await run(f)).code,"cursor");assert.equal(f.state().scan_cursor,null);
  f.remote.intercept=null;f.advance(120);assert.equal((await run(f)).code,"cleanup_only");
});

test("nenhum erro bruto/credencial integra resultado ou estado persistido",async t=>{
  const f=fixture(t);f.remote.intercept=()=>{throw new Error("SENSITIVE_FIXTURE_ONLY");};
  const result=await run(f);
  assert.equal(result.code,"network");
  assert.doesNotMatch(JSON.stringify([result,f.state(),f.jobs()]),/SENSITIVE|synthetic-fixture|Bearer/);
});

test("metadados contem apenas referencia e controle, sem conteudo de relatorio",async t=>{
  const f=fixture(t);f.count();f.enable();await run(f);
  assert.deepEqual(Object.keys(f.jobs()[0]).sort(),["day","expires_at","exported_at","issue_node_id","issue_number","repo_id","status","operation_id","creator_id"].sort());
  assert.equal(issueContent(f.today-1,[{version:betaVersion,event:"MSIX.LEITURA",total:1}]),null);
});

test("dia suprimido nao impede exportar dia posterior com dados suficientes",async t=>{
  const f=fixture(t);f.count(f.today-3,1);f.count(f.today-1,9);f.enable();
  assert.equal((await run(f)).code,"exported");
  assert.equal(managedDay(f.remote.created[0]),f.today-1);
});

test("agregado D1 adulterado nao vira conteudo GitHub",async t=>{
  const f=fixture(t);f.count(f.today-1,17,"PATH.USER");f.enable();
  assert.equal((await run(f)).code,"aggregate");assert.equal(f.remote.created.length,0);
});

test("duplicata recuperada e excluida; preservar a referencia canonica",async t=>{
  const f=fixture(t),canonical=f.remote.add(f.today-1,1);f.job(f.today-1,canonical);f.remote.add(f.today-1,2).body=canonical.body;
  await run(f);assert.equal(f.remote.deleted[0].number,2);assert.equal(f.jobs()[0].issue_number,1);
});

test("intento sem prova permanece em revisao e nunca e repetido automaticamente",async t=>{
  const f=fixture(t);f.job(f.today-1,null);f.count();f.enable();
  assert.equal((await run(f)).code,"review_required");assert.equal(f.remote.created.length,0);
  f.advance(8*86400);assert.equal((await run(f)).code,"review_required");assert.equal(f.jobs().length,1);assert.equal(f.remote.created.length,0);
});

test("token ausente com obrigacoes falha fechado e preserva ledger",async t=>{
  const f=fixture(t);f.job(f.today-8,f.remote.add(f.today-8));delete f.env.GITHUB_TOKEN;
  assert.equal((await run(f)).code,"configuration");
  assert.equal(f.remote.calls.length,0);assert.equal(f.jobs().length,1);
});

test("sem schema v2 nao faz migracao implicita nem acessa GitHub",async t=>{
  const f=fixture(t);f.db.sqlite.exec("DROP TABLE github_maintenance");
  assert.equal((await run(f)).code,"database");assert.equal(f.remote.calls.length,0);
});

test("backlog grande e retomado em lotes sem descartar referencias",async t=>{
  const f=fixture(t);for(let i=1;i<=45;i++)f.job(f.today-8-i,f.remote.add(f.today-8-i,i));
  assert.equal((await run(f)).code,"backlog");
  for(let i=0;i<13&&f.jobs().length;i++)await run(f);
  assert.equal(f.remote.issues.length,0);assert.equal(f.jobs().length,0);assert.equal(f.remote.deleted.length,45);
});

test("perda de resposta no delete nao e tratada como sucesso por status HTTP isolado",async t=>{
  const f=fixture(t);f.job(f.today-8,f.remote.add(f.today-8));
  f.remote.intercept=call=>call.query.startsWith("mutation Delete")?json({data:{deleteIssue:null}}):null;
  assert.equal((await run(f)).code,"response");assert.equal(f.jobs().length,1);
});

test("depois de falhas repetidas persiste recuo exponencial sem busy loop",async t=>{
  const f=fixture(t);f.remote.intercept=()=>json({},500);
  for(let n=1;n<=4;n++){
    await run(f);assert.equal(f.state().failures,n);
    assert.equal(f.state().retry_at,Math.floor(f.runtime.now()/1000)+60*2**(n-1));
    f.advance(60*2**(n-1));
  }
});

test("vencimento entre consulta e create impede publicar dado ja expirado",async t=>{
  const f=fixture(t);f.advance(12*3600-1);f.count(f.today-6);f.enable();
  let checks=0;
  f.remote.intercept=call=>{if(call.query.startsWith("query Repository")&&++checks===2)f.advance(2);};
  assert.equal((await run(f)).code,"expired");assert.equal(f.remote.created.length,0);
});
