-- Somente preparar/aplicar em ambiente local nesta missao. Backup/conta e
-- destino originais devem ser conferidos antes de uma futura migracao remota.
-- Executar UMA vez sobre o schema v1, sem apagar exportacoes pendentes.
ALTER TABLE github_exports ADD COLUMN issue_node_id TEXT;
ALTER TABLE github_exports ADD COLUMN repo_id TEXT;
ALTER TABLE github_exports ADD COLUMN expires_at INTEGER NOT NULL DEFAULT 0;
UPDATE github_exports SET expires_at = (day + 7) * 86400;
CREATE INDEX beta_github_expiry ON github_exports(expires_at);
CREATE TABLE github_maintenance (
 id INTEGER PRIMARY KEY CHECK(id=1),
 repo_name TEXT, repo_id TEXT, scan_cursor TEXT,
 lease_token TEXT, lease_until INTEGER NOT NULL DEFAULT 0,
 retry_at INTEGER NOT NULL DEFAULT 0, failures INTEGER NOT NULL DEFAULT 0,
 last_code TEXT NOT NULL DEFAULT 'never', last_success INTEGER NOT NULL DEFAULT 0
);
INSERT INTO github_maintenance(id) VALUES(1);
