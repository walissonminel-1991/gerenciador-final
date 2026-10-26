// Apenas testes locais: SQLite real e simulador GitHub sem sockets/credenciais.
import { DatabaseSync } from "node:sqlite";
import { readFileSync } from "node:fs";
import { expiresForDay, issueContent, betaVersion } from "./policy.mjs";

export class LocalD1 {
  constructor(schema = readFileSync(new URL("./schema.sql", import.meta.url), "utf8")) {
    this.sqlite = new DatabaseSync(":memory:");
    this.sqlite.exec(schema);
    this.fault = null;
  }
  prepare(sql) {
    const db = this;
    const statement = args => ({
      bind: (...values) => statement(values),
      async run() {
        db.fault?.(sql, args, "run");
        const result = db.sqlite.prepare(sql).run(...args);
        return { success: true, meta: { changes: Number(result.changes) } };
      },
      async first() {
        db.fault?.(sql, args, "first");
        return db.sqlite.prepare(sql).get(...args) ?? null;
      },
      async all() {
        db.fault?.(sql, args, "all");
        return { success: true, results: db.sqlite.prepare(sql).all(...args) };
      }
    });
    return statement([]);
  }
  async batch(commands) {
    this.sqlite.exec("BEGIN");
    try {
      const results = [];
      for (const command of commands) results.push(await command.run());
      this.sqlite.exec("COMMIT");
      return results;
    } catch (error) { this.sqlite.exec("ROLLBACK"); throw error; }
  }
  close() { this.sqlite.close(); }
}

const json = (data, status = 200, headers = {}) => new Response(JSON.stringify(data), {
  status, headers: { "content-type": "application/json", ...headers }
});

export function fixture(t) {
  const db = new LocalD1();
  t.after(() => db.close());
  let now = Date.parse("2026-10-26T12:00:00Z");
  const today = Math.floor(now / 86400000);
  const remote = {
    issues: [], calls: [], deleted: [], created: [], private: true, permission: "ADMIN", id: "REPO_FIXTURE",
    intercept: null, loseCreateResponse: false, loseDeleteResponse: false,
    repo() { return { id: this.id, isPrivate: this.private, viewerPermission: this.permission }; },
    add(day, number = this.issues.length + 1, legacy = false) {
      const issue = { id: "ISSUE_" + number, number, ...issueContent(day,
        [{ version: betaVersion, event: "MSIX.LEITURA", total: 17 }],crypto.randomUUID()) };
      if (legacy) issue.body = issue.body.split("\n").slice(1).join("\n");
      issue.author={id:"ACTOR_FIXTURE"};
      this.issues.push(issue);
      return issue;
    },
    async fetch(url, options) {
      if (url !== "https://api.github.com/graphql" || options.redirect !== "manual") throw new Error("unexpected_transport");
      const { query, variables } = JSON.parse(options.body);
      const call = { query, variables, at: now, signal: options.signal };
      remote.calls.push(call);
      const override = await remote.intercept?.(call);
      if (override) return override;
      if (query.startsWith("query Repository")) return json({ data: { repository: remote.repo(), viewer:{id:"ACTOR_FIXTURE"} } });
      if (query.startsWith("query Lookup")) return json({ data: { repository: { ...remote.repo(),
        issue: remote.issues.find(x => x.number === variables.number) ?? null } } });
      if (query.startsWith("query Scan")) {
        const remaining = remote.issues.filter(x => x.number > Number(variables.after || 0)).sort((a, b) => a.number - b.number);
        const nodes = remaining.slice(0, 25);
        return json({ data: { repository: { ...remote.repo(), issues: { nodes,
          pageInfo: { hasNextPage: remaining.length > 25, endCursor: nodes.length ? String(nodes.at(-1).number) : null } } } } });
      }
      if (query.startsWith("mutation Delete")) {
        const index = remote.issues.findIndex(x => x.id === variables.id);
        if (index < 0) return json({ errors: [{ type: "NOT_FOUND" }] });
        remote.deleted.push(remote.issues.splice(index, 1)[0]);
        if (remote.loseDeleteResponse) throw new Error("synthetic_lost_response");
        return json({ data: { deleteIssue: { repository: { id: remote.id }, clientMutationId: variables.operation } } });
      }
      if (query.startsWith("mutation Create")) {
        const issue = { id: "ISSUE_NEW", number: Math.max(0, ...remote.issues.map(x => x.number)) + 1,
          title: variables.title, body: variables.body, repository: remote.repo(),author:{id:"ACTOR_FIXTURE"} };
        remote.issues.push(issue);
        remote.created.push(issue);
        if (remote.loseCreateResponse) throw new Error("synthetic_lost_response");
        return json({ data: { createIssue: { issue } } });
      }
      throw new Error("unexpected_query");
    }
  };
  const env = { DB: db, PILOT_ENABLED: "no", GITHUB_EXPORT_ENABLED: "no",
    GITHUB_PRIVATE_REPO: "fixture-owner/private-fixture", GITHUB_TOKEN: "synthetic-fixture-not-a-secret",GITHUB_TOKEN_EXPIRES_AT:"2026-11-25" };
  const runtime = { now: () => now, fetch: remote.fetch,
    sleep: async ms => { now += ms; } };
  const job = (day, issue, extra = {}) => {
    const values = { issue_number: issue?.number ?? 0, issue_node_id: issue?.id ?? null,
      repo_id: remote.id, exported_at: Math.floor(now / 1000), expires_at: expiresForDay(day), ...extra };
    const operation_id=issue?.body?.match(/operation=([0-9a-f-]{36})/)?.[1]??null;
    db.sqlite.prepare(`INSERT INTO github_exports(day,issue_number,issue_node_id,repo_id,exported_at,expires_at,status,operation_id,creator_id)
      VALUES(?,?,?,?,?,?,?,?,?)`).run(day,...Object.values(values),issue?"published":"review_required",operation_id,"ACTOR_FIXTURE");
  };
  const intent=(day,issue)=>{
    job(day,issue);
    db.sqlite.prepare("UPDATE github_exports SET issue_number=0,issue_node_id=NULL,status='uncertain' WHERE day=?").run(day);
  };
  const count = (day = today - 1, total = 17, event = "MSIX.LEITURA", version = betaVersion) => {
    db.sqlite.prepare("INSERT INTO daily_counts VALUES(?,?,?,?,?)").run(day, version, event, total, expiresForDay(day));
  };
  return { db, remote, env, runtime, today, job, intent, count,
    advance: seconds => { now += seconds * 1000; },
    state: () => db.sqlite.prepare("SELECT * FROM github_maintenance WHERE id=1").get(),
    jobs: () => db.sqlite.prepare("SELECT * FROM github_exports ORDER BY day").all(),
    enable: () => { env.PILOT_ENABLED = "yes"; env.GITHUB_EXPORT_ENABLED = "yes"; } };
}
export { json };
