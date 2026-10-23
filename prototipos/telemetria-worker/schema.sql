-- Prototipo PRIVADO; nao armazena um relatorio por instalacao.
-- Um registro por dia, versao e categoria (limitado pela cardinalidade).
CREATE TABLE IF NOT EXISTS daily_counts (
    day INTEGER NOT NULL,
    version TEXT NOT NULL,
    event TEXT NOT NULL,
    total INTEGER NOT NULL DEFAULT 0,
    expires_at INTEGER NOT NULL,
    PRIMARY KEY(day, version, event)
);
CREATE INDEX IF NOT EXISTS counts_expires_idx ON daily_counts(expires_at);
