// Planejador puro: nao possui transporte, credenciais ou aplicacao remota.
// 0002 permanece imutavel. O plano repara v1, v2 e execucoes parciais conhecidas.
const exportColumns = {
  issue_node_id: "TEXT", repo_id: "TEXT", expires_at: "INTEGER NOT NULL DEFAULT 0",
  status: "TEXT NOT NULL DEFAULT 'published' CHECK(status IN ('prepared','uncertain','published','review_required','deleted'))",
  operation_id: "TEXT", creator_id: "TEXT"
};
const maintenanceColumns = {
  repo_name:"TEXT",repo_id:"TEXT",scan_cursor:"TEXT",lease_token:"TEXT",
  lease_until:"INTEGER NOT NULL DEFAULT 0",retry_at:"INTEGER NOT NULL DEFAULT 0",
  failures:"INTEGER NOT NULL DEFAULT 0",last_code:"TEXT NOT NULL DEFAULT 'never'",
  last_success:"INTEGER NOT NULL DEFAULT 0",schema_version:"INTEGER NOT NULL DEFAULT 3",
  last_started:"INTEGER NOT NULL DEFAULT 0",last_finished:"INTEGER NOT NULL DEFAULT 0",
  last_error:"TEXT NOT NULL DEFAULT 'none'",last_error_at:"INTEGER NOT NULL DEFAULT 0",
  scan_started:"INTEGER NOT NULL DEFAULT 0",unverified_issues:"INTEGER NOT NULL DEFAULT 0"
};
export async function inspectSchema(db) {
  const tables = {};
  for (const name of ["daily_counts","github_exports","github_maintenance","ingest_budget","github_delete_refs"])
    tables[name]=(await db.prepare(`PRAGMA table_info(${name})`).all()).results;
  const indexes=(await db.prepare("SELECT name,tbl_name,sql FROM sqlite_master WHERE type='index' AND name LIKE 'beta_%'").all()).results;
  const definitions=(await db.prepare("SELECT name,sql FROM sqlite_master WHERE type='table'").all()).results;
  const invalid=(await db.prepare(`SELECT count(*) AS n FROM github_exports
    WHERE day<0 OR day>2958465 OR typeof(day)<>'integer' OR issue_number<0 OR issue_number>2147483647
      OR typeof(issue_number)<>'integer' OR exported_at<0 OR typeof(exported_at)<>'integer'`).first()).n;
  return {tables,indexes,definitions,invalid}; // somente schema e contagem; nao extrai referencias/dados.
}
export function migrationPlan(snapshot) {
  const {tables,indexes,definitions,invalid}=snapshot;
  if(invalid!==0)throw new Error("schema_invalid_rows");
  const requireColumn=(table,name,type,pk=false)=>{
    const col=tables[table]?.find(c=>c.name===name);
    if(!col||col.type.toUpperCase()!==type||(pk&&col.pk!==1))throw new Error("schema_incompatible");
  };
  for(const [name,type] of Object.entries({day:"INTEGER",version:"TEXT",event:"TEXT",total:"INTEGER",expires_at:"INTEGER"}))
    requireColumn("daily_counts",name,type);
  for(const [name,pk] of [["day",1],["version",2],["event",3]])
    if(tables.daily_counts.find(c=>c.name===name)?.pk!==pk)throw new Error("schema_primary_key");
  requireColumn("github_exports","day","INTEGER",true);
  requireColumn("github_exports","issue_number","INTEGER");
  requireColumn("github_exports","exported_at","INTEGER");
  const sql=[];
  const hasConstraint=(table,fragment)=>{
    const ddl=definitions?.find(x=>x.name===table)?.sql?.toLowerCase().replace(/\s+/g,"")||"";
    if(!ddl.includes(fragment.replace(/\s+/g,"").toLowerCase()))throw new Error("schema_constraint_conflict");
  };
  if(tables.github_exports.some(c=>c.name==='status'))
    hasConstraint("github_exports","CHECK(status IN ('prepared','uncertain','published','review_required','deleted'))");
  if(tables.ingest_budget.length){
    hasConstraint("ingest_budget","CHECK(reports BETWEEN 0 AND 1000)");
    hasConstraint("ingest_budget","CHECK(ticket IS NULL OR length(ticket)=36)");
  }
  if(tables.github_delete_refs.length)hasConstraint("github_delete_refs","CHECK(issue_number>0)");
  if(!tables.github_delete_refs.length)sql.push(`CREATE TABLE github_delete_refs(
    issue_node_id TEXT PRIMARY KEY NOT NULL,issue_number INTEGER NOT NULL UNIQUE CHECK(issue_number>0),day INTEGER NOT NULL,
    repo_id TEXT NOT NULL,expires_at INTEGER NOT NULL)`);
  else for(const [name,type] of Object.entries({issue_node_id:"TEXT",issue_number:"INTEGER",day:"INTEGER",repo_id:"TEXT",expires_at:"INTEGER"}))
    requireColumn("github_delete_refs",name,type,name==="issue_node_id");
  const addMissing=(table,columns)=>{
    for(const [name,definition] of Object.entries(columns)){
      const existing=tables[table]?.find(c=>c.name===name);
      if(existing){if(existing.type.toUpperCase()!==definition.split(" ")[0] ||
          (definition.includes("NOT NULL")&&existing.notnull!==1))throw new Error("schema_incompatible");}
      else sql.push(`ALTER TABLE ${table} ADD COLUMN ${name} ${definition}`);
    }
  };
  addMissing("github_exports",exportColumns);
  if(!tables.github_maintenance.length){
    sql.push("CREATE TABLE github_maintenance(id INTEGER PRIMARY KEY CHECK(id=1))");
  }else requireColumn("github_maintenance","id","INTEGER",true);
  addMissing("github_maintenance",maintenanceColumns);
  // Somente expiracao ausente/zero; jamais estender prazo ou apagar referencia.
  sql.push("UPDATE github_exports SET expires_at=(day+7)*86400 WHERE expires_at=0");
  sql.push("UPDATE github_exports SET status='review_required' WHERE issue_number=0 AND status='published'");
  sql.push("INSERT OR IGNORE INTO github_maintenance(id) VALUES(1)");
  sql.push("UPDATE github_maintenance SET schema_version=3 WHERE id=1");
  const wanted={
    beta_expiry:["daily_counts","CREATE INDEX beta_expiry ON daily_counts(expires_at)"],
    beta_github_expiry:["github_exports","CREATE INDEX beta_github_expiry ON github_exports(expires_at)"],
    beta_export_operation:["github_exports","CREATE UNIQUE INDEX beta_export_operation ON github_exports(operation_id) WHERE operation_id IS NOT NULL"],
    beta_ingest_expiry:["ingest_budget","CREATE INDEX beta_ingest_expiry ON ingest_budget(expires_at)"]
  };
  if(!tables.ingest_budget.length)sql.push(`CREATE TABLE ingest_budget(
    day INTEGER PRIMARY KEY,reports INTEGER NOT NULL CHECK(reports BETWEEN 0 AND 1000),
    event_total INTEGER NOT NULL CHECK(event_total>=0),expires_at INTEGER NOT NULL,
    ticket TEXT CHECK(ticket IS NULL OR length(ticket)=36))`);
  else for(const [name,type] of Object.entries({day:"INTEGER",reports:"INTEGER",event_total:"INTEGER",expires_at:"INTEGER",ticket:"TEXT"}))
    requireColumn("ingest_budget",name,type,name==="day");
  for(const [name,[table,ddl]] of Object.entries(wanted)){
    const index=indexes.find(i=>i.name===name);
    if(index){
      const normalize=s=>s.toLowerCase().replace(/\s+/g,"").replace("ifnotexists","");
      if(index.tbl_name!==table||normalize(index.sql)!==normalize(ddl))throw new Error("schema_index_conflict");
    }else sql.push(ddl);
  }
  return sql;
}
