import { boundedText, expiresForDay, issueContent, managedDay, secondsPerDay } from "./policy.mjs";

const repositoryFields = "id isPrivate viewerPermission";
const issueFields = "id number title body";
const repoQuery = `query Repository($owner:String!,$name:String!) {
  repository(owner:$owner,name:$name) { ${repositoryFields} }
}`;
const scanQuery = `query Scan($owner:String!,$name:String!,$after:String) {
  repository(owner:$owner,name:$name) { ${repositoryFields}
    issues(first:100,after:$after,states:[OPEN,CLOSED],orderBy:{field:CREATED_AT,direction:ASC}) {
      nodes { ${issueFields} } pageInfo { hasNextPage endCursor }
    }
  }
}`;
const lookupQuery = `query Lookup($owner:String!,$name:String!,$number:Int!) {
  repository(owner:$owner,name:$name) { ${repositoryFields} issue(number:$number) { ${issueFields} } }
}`;
const deleteMutation = `mutation Delete($id:ID!,$operation:String!) {
  deleteIssue(input:{issueId:$id,clientMutationId:$operation}) { clientMutationId repository { id } }
}`;
const createMutation = `mutation Create($repo:ID!,$title:String!,$body:String!,$operation:String!) {
  createIssue(input:{repositoryId:$repo,title:$title,body:$body,clientMutationId:$operation}) {
    issue { ${issueFields} repository { ${repositoryFields} } }
  }
}`;
const codes = new Set(["configuration", "repository_access", "repository_changed", "identity_conflict",
  "network", "http", "rate_limit", "graphql", "cursor", "response", "database", "budget", "aggregate"]);
class MaintenanceError extends Error {
  constructor(code, retryAt = 0) { super(code); this.code = code; this.retryAt = retryAt; }
}
const fail = (code, retryAt) => { throw new MaintenanceError(code, retryAt); };
const unix = runtime => Math.floor(runtime.now() / 1000);

function config(env) {
  // Rejeitar escapes, caminhos/URLs e formatos especiais; destino nunca vem do cliente.
  const match = /^([A-Za-z0-9][A-Za-z0-9-]{0,38})\/([A-Za-z0-9_][A-Za-z0-9_.-]{0,99})$/.exec(env.GITHUB_PRIVATE_REPO || "");
  if (!match || typeof env.GITHUB_TOKEN !== "string" || env.GITHUB_TOKEN.length < 20) return null;
  return { owner: match[1], name: match[2], repo: env.GITHUB_PRIVATE_REPO.toLowerCase() };
}

// Chamadas sequenciais, no maximo 30 por execucao/120s; nunca reter resposta/erro bruto.
class GitHub {
  constructor(env, runtime) {
    this.env = env; this.runtime = runtime;
    this.deadline = runtime.now() + 120000;
    this.calls = 0; this.lastMutation = 0; this.cooldown = 0;
  }
  async call(query, variables, mutation = false) {
    const now = unix(this.runtime);
    if (this.cooldown > now) fail("rate_limit", this.cooldown);
    if (this.calls >= 30 || this.runtime.now() + 8000 > this.deadline) fail("budget");
    this.runtime.consume();
    if (mutation && this.lastMutation) {
      const delay = 1000 - (this.runtime.now() - this.lastMutation);
      if (delay > 0) await this.runtime.sleep(delay);
    }
    this.calls++;
    if (mutation) this.lastMutation = this.runtime.now();
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 8000);
    try {
      const response = await this.runtime.fetch("https://api.github.com/graphql", {
        method: "POST", redirect: "manual", signal: controller.signal,
        headers: { authorization: "Bearer " + this.env.GITHUB_TOKEN,
          accept: "application/vnd.github+json", "content-type": "application/json",
          "user-agent": "gf-beta-private-aggregation" },
        body: JSON.stringify({ query, variables })
      });
      const retry = response.headers.get("retry-after");
      const reset = Number(response.headers.get("x-ratelimit-reset"));
      const remaining = response.headers.get("x-ratelimit-remaining");
      const retryAt = retry === null ? 0 : (/^\d+$/.test(retry) ? now + Number(retry) : Math.ceil(Date.parse(retry) / 1000));
      if (retry !== null || remaining === "0" || response.status === 429)
        this.cooldown = Math.max(now + 60, Number.isFinite(retryAt) ? retryAt : 0,
          remaining === "0" && Number.isSafeInteger(reset) ? reset + 1 : 0);
      if (!response.ok) {
        void response.body?.cancel().catch(() => {});
        fail(this.cooldown ? "rate_limit" : "http", this.cooldown);
      }
      const body = await boundedText(response.body, 1024 * 1024, 8000);
      if (!body || body.oversize) fail("response");
      let json; try { json = JSON.parse(body.raw); } catch { fail("response"); }
      if (Array.isArray(json.errors) && json.errors.length) {
        const types = json.errors.map(x => x.type);
        if (types.includes("RATE_LIMITED")) fail("rate_limit", Math.max(this.cooldown, now + 60));
        // Cursor expirado/invalido: reiniciar varredura sem descartar obrigacoes.
        if (types.includes("INVALID_CURSOR_ARGUMENTS")) fail("cursor");
        fail("graphql", this.cooldown);
      }
      if (!json.data || typeof json.data !== "object") fail("response");
      return json.data;
    } catch (error) {
      if (error instanceof MaintenanceError) throw error;
      fail("network");
    } finally { clearTimeout(timer); }
  }
}

function checkRepository(repo, expectedId) {
  if (!repo || repo.isPrivate !== true || repo.viewerPermission !== "ADMIN" ||
      typeof repo.id !== "string" || repo.id.length < 1 || repo.id.length > 200) fail("repository_access");
  if (expectedId && repo.id !== expectedId) fail("repository_changed");
  return repo.id;
}

async function deleteIssue(client, issue, repoId) {
  const operation = "gf-beta-delete-" + issue.number;
  const result = await client.call(deleteMutation, { id: issue.id, operation }, true);
  if (result.deleteIssue?.repository?.id !== repoId ||
      result.deleteIssue?.clientMutationId !== operation) fail("response");
}

function limitedDatabase(db, consume) {
  const wrap = statement => ({
    bind: (...args) => wrap(statement.bind(...args)),
    run: () => { consume(); return statement.run(); },
    first: () => { consume(); return statement.first(); },
    all: () => { consume(); return statement.all(); }
  });
  return { prepare: sql => wrap(db.prepare(sql)) };
}

async function work(env, gh, state, client, runtime, db) {
  const now = unix(runtime), today = Math.floor(now / secondsPerDay);
  const vars = { owner: gh.owner, name: gh.name };
  const repoId = checkRepository((await client.call(repoQuery, vars)).repository, state.repo_id);
  if (state.repo_name && state.repo_name !== gh.repo) fail("repository_changed");
  await db.prepare("UPDATE github_maintenance SET repo_name=?,repo_id=? WHERE id=1").bind(gh.repo, repoId).run();
  const ledger = await db.prepare("SELECT * FROM github_exports WHERE expires_at<=? AND issue_number>0 ORDER BY day LIMIT 8")
    .bind(now).all();
  for (const job of ledger.results) {
    if (job.repo_id && job.repo_id !== repoId) fail("repository_changed");
    const repo = (await client.call(lookupQuery, { ...vars, number: job.issue_number })).repository;
    checkRepository(repo, repoId);
    const issue = repo.issue;
    // null sem erros em consulta autenticada ao repo privado confirma ausencia.
    // HTTP 404/403, GraphQL parcial e falta de permissao NUNCA confirmam exclusao.
    if (issue !== null) {
      if (!issue || (job.issue_node_id ? issue.id !== job.issue_node_id : managedDay(issue) !== job.day))
        fail("identity_conflict");
      await db.prepare("UPDATE github_exports SET issue_node_id=?,repo_id=? WHERE day=?")
        .bind(issue.id, repoId, job.day).run();
      await deleteIssue(client, issue, repoId);
    }
    await db.prepare("DELETE FROM github_exports WHERE day=? AND issue_number=?").bind(job.day, job.issue_number).run();
  }
  if (await db.prepare("SELECT day FROM github_exports WHERE expires_at<=? AND issue_number>0 LIMIT 1").bind(now).first())
    return "backlog"; // priorizar exclusao; nenhuma nova publicacao enquanto houver atraso.

  let after = state.scan_cursor;
  // Varredura completa pode exigir varios crons. Sem criaçao ate concluir todas as paginas.
  for (let page = 0; page < 3; page++) {
    const repo = (await client.call(scanQuery, { ...vars, after })).repository;
    checkRepository(repo, repoId);
    const connection = repo.issues;
    if (!Array.isArray(connection?.nodes) || connection.nodes.length > 100 ||
        typeof connection.pageInfo?.hasNextPage !== "boolean") fail("response");
    for (const issue of connection.nodes) {
      const day = managedDay(issue);
      if (day === null) continue;
      if (day >= today) fail("identity_conflict"); // nunca gerar datas futuras/atuais.
      const expiry = expiresForDay(day);
      if (expiry <= now) {
        await deleteIssue(client, issue, repoId);
        await db.prepare("DELETE FROM github_exports WHERE day=? AND issue_number=?").bind(day, issue.number).run();
      } else {
        const existing = await db.prepare("SELECT * FROM github_exports WHERE day=?").bind(day).first();
        if (existing?.issue_number > 0 && existing.issue_number !== issue.number) {
          // Duplicata do formato exato; conservar apenas a Issue canonica do ledger.
          await deleteIssue(client, issue, repoId);
        } else {
          if (existing?.repo_id && existing.repo_id !== repoId) fail("repository_changed");
          if (existing?.issue_node_id && existing.issue_node_id !== issue.id) fail("identity_conflict");
          await db.prepare(`INSERT INTO github_exports(day,issue_number,exported_at,issue_node_id,repo_id,expires_at)
            VALUES(?,?,?,?,?,?) ON CONFLICT(day) DO UPDATE SET
            issue_number=excluded.issue_number,issue_node_id=excluded.issue_node_id,repo_id=excluded.repo_id,
            expires_at=MIN(github_exports.expires_at,excluded.expires_at)`)
            .bind(day, issue.number, now, issue.id, repoId, expiry).run();
        }
      }
    }
    const info = connection.pageInfo;
    const next = info.hasNextPage ? info.endCursor : null;
    if (info.hasNextPage && (typeof next !== "string" || next.length < 1 || next.length > 2048 || next === after)) fail("cursor");
    // Checkpoint somente depois de processar a pagina inteira. Repetir e seguro.
    await db.prepare("UPDATE github_maintenance SET scan_cursor=? WHERE id=1").bind(next).run();
    after = next;
    if (!info.hasNextPage) break;
    if (page === 2) return "scanning";
  }
  // Intentos sem numero so podem ser aposentados apos uma varredura completa.
  await db.prepare("DELETE FROM github_exports WHERE issue_number=0 AND expires_at<=?").bind(now).run();
  if (runtime.allowCreate === false || env.PILOT_ENABLED !== "yes" || env.GITHUB_EXPORT_ENABLED !== "yes") return "cleanup_only";
  const candidates = await db.prepare(`SELECT DISTINCT d.day FROM daily_counts d
    WHERE d.day<? AND d.day>? AND d.expires_at>? AND NOT EXISTS
      (SELECT 1 FROM github_exports g WHERE g.day=d.day AND g.issue_number>0)
    ORDER BY d.day LIMIT 6`).bind(today, today - 7, now).all();
  if (!candidates.results.length) return "idle";
  let day, content;
  for (const candidate of candidates.results) {
    const rows = await db.prepare("SELECT version,event,total FROM daily_counts WHERE day=? AND expires_at>? ORDER BY version,event")
      .bind(candidate.day, now).all();
    try { content = issueContent(candidate.day, rows.results); } catch { fail("aggregate"); }
    if (content) { day = candidate.day; break; }
  }
  if (!content) return "suppressed";
  const pending = await db.prepare("SELECT * FROM github_exports WHERE day=?").bind(day).first();
  // POST com resposta perdida pode ter sido aplicado. Dar tempo de visibilidade
  // e reconciliar por marcacao antes de repetir, sem alegar exactly-once.
  if (pending && now < pending.exported_at + 600) return "reconciling";
  await db.prepare(`INSERT INTO github_exports(day,issue_number,exported_at,repo_id,expires_at)
    VALUES(?,0,?,?,?) ON CONFLICT(day) DO UPDATE SET exported_at=excluded.exported_at`)
    .bind(day, now, repoId, expiresForDay(day)).run();
  // Conferir novamente antes da mutacao de criacao; repo publico = bloqueio.
  checkRepository((await client.call(repoQuery, vars)).repository, repoId);
  if (expiresForDay(day) <= unix(runtime)) return "expired";
  const result = await client.call(createMutation, { repo: repoId, ...content, operation: "gf-beta-create-" + day }, true);
  const issue = result.createIssue?.issue;
  checkRepository(issue?.repository, repoId);
  if (managedDay(issue) !== day) fail("response");
  await db.prepare("UPDATE github_exports SET issue_number=?,issue_node_id=?,exported_at=? WHERE day=? AND issue_number=0")
    .bind(issue.number, issue.id, now, day).run();
  return "exported";
}

export async function runMaintenance(env, overrides = {}) {
  const runtime = { now: Date.now, fetch: globalThis.fetch,
    sleep: ms => new Promise(resolve => setTimeout(resolve, ms)), ...overrides };
  if (!env.DB) return { ok: false, code: "database" };
  const db = env.DB, now = unix(runtime), lease = crypto.randomUUID();
  // Limite combinado conservador: 44 operacoes SQL/GitHub + 6 reservadas
  // ao lock/estado/limpeza local. Cabe inclusive em planos pequenos (50).
  let operations = 0;
  runtime.consume = () => { if (operations >= 44) fail("budget"); operations++; };
  let acquired = false, state, client;
  try {
    state = await db.prepare("SELECT * FROM github_maintenance WHERE id=1").first();
    if (!state) fail("database"); // schema v1 nao deve ser atualizado implicitamente no cron.
    if (state.retry_at > now) return { ok: true, code: "cooldown" };
    const lock = await db.prepare(`UPDATE github_maintenance SET lease_token=?,lease_until=?
      WHERE id=1 AND lease_until<=?`).bind(lease, now + 600, now).run();
    if (lock.meta?.changes !== 1) return { ok: true, code: "busy" };
    acquired = true;
    state = await db.prepare("SELECT * FROM github_maintenance WHERE id=1").first();
    if (state.retry_at > unix(runtime)) return { ok: true, code: "cooldown" };
    const gh = config(env);
    if (!gh) {
      if (state.repo_id || env.GITHUB_EXPORT_ENABLED === "yes" ||
          await db.prepare("SELECT day FROM github_exports LIMIT 1").first()) fail("configuration");
      return { ok: true, code: "disabled" };
    }
    client = new GitHub(env, runtime);
    const code = await work(env, gh, state, client, runtime, limitedDatabase(db, runtime.consume));
    await db.prepare(`UPDATE github_maintenance SET failures=0,retry_at=?,last_code=?,last_success=?
      WHERE id=1 AND lease_token=?`).bind(client.cooldown, code, unix(runtime), lease).run();
    return { ok: true, code, calls: client.calls };
  } catch (error) {
    const code = error instanceof MaintenanceError && codes.has(error.code) ? error.code : "database";
    if (acquired) {
      const failures = Math.min(20, (state?.failures || 0) + 1);
      const backoff = Math.min(21600, 60 * 2 ** (failures - 1));
      const retryAt = Math.max(unix(runtime) + backoff, error instanceof MaintenanceError ? error.retryAt : 0);
      try {
        await db.prepare(`UPDATE github_maintenance SET failures=?,retry_at=?,last_code=?,
          scan_cursor=CASE WHEN ?='cursor' THEN NULL ELSE scan_cursor END WHERE id=1 AND lease_token=?`)
          .bind(failures, retryAt, code, code, lease).run();
      } catch { /* Sem log bruto/segredo. Proxima invocacao reconcilia o ledger. */ }
    }
    return { ok: false, code, calls: client?.calls || 0 };
  } finally {
    if (acquired) {
      try { await db.prepare("UPDATE github_maintenance SET lease_token=NULL,lease_until=0 WHERE id=1 AND lease_token=?").bind(lease).run(); }
      catch { /* Lease expira em 10min mesmo apos falha D1/terminacao do Worker. */ }
    }
  }
}
