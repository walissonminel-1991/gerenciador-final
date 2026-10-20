-- Modelo PRIVADO de sete dias: nao salvar dados de perfis.
CREATE TABLE IF NOT EXISTS reports (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    data TEXT NOT NULL CHECK(length(data) <= 4096),
    created_at INTEGER NOT NULL,
    expires_at INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS reports_expires_idx ON reports(expires_at);
