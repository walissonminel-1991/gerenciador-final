// Somente metadados operacionais agregados. Nenhum corpo, IP, repo, ator ou ID
// de operacao entra no snapshot/alerta. Fonte do erro e enum fixo, nunca texto.
const safeCodes=new Set(["none","never","disabled","cleanup_only","idle","exported","suppressed","expired",
  "review_required","ledger_capacity","scanning","backlog","budget","rate_limit","authentication","authorization",
  "configuration","repository_access","repository_changed","identity_conflict","network","http","graphql","cursor",
  "response","database","schema","lease_lost","credential_expiry","absence_unproven"]);
const count=value=>Number.isSafeInteger(value)&&value>=0?Math.min(value,100000000):0;
const age=(now,then)=>then>0&&then<=now?Math.max(0,now-then):null;
export function healthFromSnapshot(state,totals,now,credentialExpiry) {
  const expiry=Date.parse(credentialExpiry||"")/1000;
  const result={schema:1,maintenance_age_s:age(now,state.last_started),
    success_age_s:age(now,state.last_success),last_error:safeCodes.has(state.last_error)?state.last_error:"database",
    last_code:safeCodes.has(state.last_code)?state.last_code:"database",failures:count(state.failures),
    overdue:count(totals.overdue),unresolved:count(totals.unresolved),obligations:count(totals.obligations),
    unverified:count(state.unverified_issues),lease_active:state.lease_until>now,
    lease_age_s:state.lease_until>now?Math.max(0,600-(state.lease_until-now)):0,
    reconciliation_age_s:age(now,totals.oldest_unresolved),overdue_age_s:age(now,totals.oldest_overdue),
    credential_days_left:Number.isFinite(expiry)?Math.floor((expiry-now)/86400):null,alerts:[]};
  if(result.maintenance_age_s===null||result.maintenance_age_s>7200)result.alerts.push("cron_missing");
  if(result.success_age_s===null||result.success_age_s>14400)result.alerts.push("maintenance_stale");
  if(result.failures>=3)result.alerts.push("consecutive_failures");
  if(["authentication","authorization"].includes(result.last_code))result.alerts.push("github_authentication");
  if(result.overdue)result.alerts.push("retention_overdue");
  if(result.last_code==="absence_unproven")result.alerts.push("retention_review_required");
  if(result.unresolved||result.unverified)result.alerts.push("administrative_review");
  if(result.obligations>=32)result.alerts.push("ledger_capacity");
  if(result.credential_days_left===null)result.alerts.push("credential_expiry_unknown");
  else if(result.credential_days_left<=0)result.alerts.push("credential_expired");
  else if(result.credential_days_left<=14)result.alerts.push("credential_rotation_due");
  if(state.lease_until && state.lease_until<=now && state.lease_token)result.alerts.push("lease_expired");
  result.severity=result.overdue||result.unresolved||result.unverified||result.failures>=3||
    result.alerts.includes("cron_missing")||result.alerts.includes("credential_expired")||result.alerts.includes("retention_review_required")?"critical":result.alerts.length?"warning":"ok";
  return result;
}
export async function readHealth(db,now,expiry) {
  const state=await db.prepare("SELECT * FROM github_maintenance WHERE id=1").first();
  if(!state||state.schema_version!==3)throw new Error("health_schema");
  const totals=await db.prepare(`SELECT count(*) AS obligations,
    SUM(CASE WHEN expires_at<=? THEN 1 ELSE 0 END) AS overdue,
    SUM(CASE WHEN issue_number=0 THEN 1 ELSE 0 END) AS unresolved,
    MIN(CASE WHEN issue_number=0 THEN exported_at END) AS oldest_unresolved,
    MIN(CASE WHEN expires_at<=? THEN expires_at END) AS oldest_overdue FROM (
      SELECT issue_number,exported_at,expires_at FROM github_exports WHERE status<>'deleted'
      UNION ALL SELECT issue_number,0 AS exported_at,expires_at FROM github_delete_refs
    )`).bind(now,now).first();
  return healthFromSnapshot(state,totals,now,expiry);
}
export function logHealth(snapshot,enabled,logger=console.log) {
  if(enabled==="yes")logger(JSON.stringify({event:"GF_BETA_HEALTH",...snapshot}));
}
