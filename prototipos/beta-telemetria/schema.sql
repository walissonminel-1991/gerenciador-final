-- Somente agregados por dia, versao e categoria. NUNCA IP ou identidade.
CREATE TABLE IF NOT EXISTS daily_counts(
 day INTEGER NOT NULL, version TEXT NOT NULL, event TEXT NOT NULL,
 total INTEGER NOT NULL DEFAULT 0, expires_at INTEGER NOT NULL,
 PRIMARY KEY(day,version,event)
);
CREATE INDEX IF NOT EXISTS beta_expiry ON daily_counts(expires_at);
CREATE TABLE IF NOT EXISTS github_exports(
 day INTEGER PRIMARY KEY, issue_number INTEGER NOT NULL, exported_at INTEGER NOT NULL,
 issue_node_id TEXT, repo_id TEXT, expires_at INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS beta_github_expiry ON github_exports(expires_at);
CREATE TABLE IF NOT EXISTS github_maintenance (
 id INTEGER PRIMARY KEY CHECK(id=1),
 repo_name TEXT, repo_id TEXT, scan_cursor TEXT,
 lease_token TEXT, lease_until INTEGER NOT NULL DEFAULT 0,
 retry_at INTEGER NOT NULL DEFAULT 0, failures INTEGER NOT NULL DEFAULT 0,
 last_code TEXT NOT NULL DEFAULT 'never', last_success INTEGER NOT NULL DEFAULT 0
);
INSERT OR IGNORE INTO github_maintenance(id) VALUES(1);
