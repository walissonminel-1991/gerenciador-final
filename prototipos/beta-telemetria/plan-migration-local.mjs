// Somente SQLite local, sem import de Worker, fetch, Cloudflare ou credencial.
// Sem argumentos de rede ou de aplicacao remota. Default: mostrar plano.
import {DatabaseSync} from "node:sqlite";
import {createHash} from "node:crypto";
import {statSync} from "node:fs";
import {inspectSchema,migrationPlan} from "./schema-migration.mjs";
const [file,mode]=process.argv.slice(2);
if(!file||![undefined,"--apply-local-fixture"].includes(mode)||!statSync(file).isFile())
  throw new Error("usage: node plan-migration-local.mjs LOCAL.sqlite [--apply-local-fixture]");
const sqlite=new DatabaseSync(file,{readOnly:!mode});
const db={prepare:sql=>({all:async()=>({results:sqlite.prepare(sql).all()}),first:async()=>sqlite.prepare(sql).get()})};
try{
  const snapshot=await inspectSchema(db), plan=migrationPlan(snapshot);
  const fingerprint=createHash("sha256").update(JSON.stringify(snapshot)).digest("hex");
  if(mode){
    sqlite.exec("BEGIN IMMEDIATE");
    try{for(const sql of plan)sqlite.exec(sql);migrationPlan(await inspectSchema(db));sqlite.exec("COMMIT");}
    catch(error){sqlite.exec("ROLLBACK");throw error;}
  }
  process.stdout.write(JSON.stringify({schema_fingerprint:fingerprint,local_applied:!!mode,sql:plan},null,2)+"\n");
}finally{sqlite.close();}
