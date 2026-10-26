-- Somente agregados por dia, versao e categoria. NUNCA IP ou identidade.
CREATE TABLE IF NOT EXISTS daily_counts(
 day INTEGER NOT NULL, version TEXT NOT NULL, event TEXT NOT NULL,
 total INTEGER NOT NULL DEFAULT 0, expires_at INTEGER NOT NULL,
 PRIMARY KEY(day,version,event)
);
CREATE INDEX IF NOT EXISTS beta_expiry ON daily_counts(expires_at);
CREATE TABLE IF NOT EXISTS github_exports(
 day INTEGER PRIMARY KEY, issue_number INTEGER NOT NULL, exported_at INTEGER NOT NULL
);