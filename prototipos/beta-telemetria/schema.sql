-- Somente agregados por dia, versao e categoria. NUNCA IP ou identidade.
CREATE TABLE IF NOT EXISTS daily_counts(
 day INTEGER NOT NULL, version TEXT NOT NULL, event TEXT NOT NULL,
 total INTEGER NOT NULL DEFAULT 0, expires_at INTEGER NOT NULL,
 PRIMARY KEY(day,version,event)
);
CREATE INDEX IF NOT EXISTS beta_expiry ON daily_counts(expires_at);
CREATE TABLE IF NOT EXISTS github_exports(
 day INTEGER PRIMARY KEY, issue_number INTEGER NOT NULL, exported_at INTEGER NOT NULL,
 issue_node_id TEXT, repo_id TEXT, expires_at INTEGER NOT NULL,
 status TEXT NOT NULL DEFAULT 'published' CHECK(status IN ('prepared','uncertain','published','review_required','deleted')),
 operation_id TEXT, creator_id TEXT
);
CREATE INDEX IF NOT EXISTS beta_github_expiry ON github_exports(expires_at);
CREATE UNIQUE INDEX IF NOT EXISTS beta_export_operation ON github_exports(operation_id) WHERE operation_id IS NOT NULL;
CREATE TABLE IF NOT EXISTS github_delete_refs(
 issue_node_id TEXT PRIMARY KEY NOT NULL,issue_number INTEGER NOT NULL UNIQUE CHECK(issue_number>0),day INTEGER NOT NULL,
 repo_id TEXT NOT NULL,expires_at INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS github_maintenance (
 id INTEGER PRIMARY KEY CHECK(id=1),
 repo_name TEXT, repo_id TEXT, scan_cursor TEXT,
 lease_token TEXT, lease_until INTEGER NOT NULL DEFAULT 0,
 retry_at INTEGER NOT NULL DEFAULT 0, failures INTEGER NOT NULL DEFAULT 0,
 last_code TEXT NOT NULL DEFAULT 'never', last_success INTEGER NOT NULL DEFAULT 0,
 schema_version INTEGER NOT NULL DEFAULT 3,
 last_started INTEGER NOT NULL DEFAULT 0, last_finished INTEGER NOT NULL DEFAULT 0,
 last_error TEXT NOT NULL DEFAULT 'none', last_error_at INTEGER NOT NULL DEFAULT 0,
 scan_started INTEGER NOT NULL DEFAULT 0, unverified_issues INTEGER NOT NULL DEFAULT 0
);
INSERT OR IGNORE INTO github_maintenance(id) VALUES(1);
CREATE TABLE IF NOT EXISTS ingest_budget(
 day INTEGER PRIMARY KEY, reports INTEGER NOT NULL CHECK(reports BETWEEN 0 AND 1000),
 event_total INTEGER NOT NULL CHECK(event_total>=0), expires_at INTEGER NOT NULL,
 ticket TEXT CHECK(ticket IS NULL OR length(ticket)=36)
);
CREATE INDEX IF NOT EXISTS beta_ingest_expiry ON ingest_budget(expires_at);
