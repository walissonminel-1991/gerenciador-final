// MODELO NAO IMPLANTADO. Sem endpoint publico nem chaves do GitHub.
// Recebe somente agregados ja consentidos, com D1 privado e expiracao curta.
const allowed = new Set([
  "APP.FALHA_INTERFACE", "APP.FALHA_NAO_TRATADA",
  "REGISTRO_PROGRAMAS.FALHA", "MSIX.LEITURA",
  "MULTIFONTES.CONSULTA_FALHA", "MULTIFONTES.CONSULTA_CANCELADA",
  "DESINSTALACAO.FALHA", "INSTALACAO_OFICIAL.FALHA",
  "TERMINAL_GITHUB.FALHA", "JANELA.BANDEJA_RESTAURAR.INICIO",
  "JANELA.BANDEJA_RESTAURAR.FIM", "JANELA.BANDEJA_OCULTAR.INICIO",
  "JANELA.BANDEJA_OCULTAR.FIM", "DIAGNOSTICO_COMPLETO.FIM"
]);
const maxBody = 4096;
const ttlSeconds = 7 * 86400;

export function validateReport(value) {
  if (!value || typeof value !== "object" || Array.isArray(value)) return null;
  if (Object.keys(value).sort().join(",") !== "counts,exit,schema,version") return null;
  if (value.schema !== 1 || value.exit !== "normal" ||
      typeof value.version !== "string" ||
      !/^v\d{1,3}(\.\d{1,4}){3}$/.test(value.version) ||
      !value.counts || typeof value.counts !== "object" || Array.isArray(value.counts))
    return null;
  const keys = Object.keys(value.counts);
  if (keys.length === 0 || keys.length > 14) return null;
  for (const key of keys) {
    const n = value.counts[key];
    if (!allowed.has(key) || !Number.isInteger(n) || n < 1 || n > 100) return null;
  }
  return JSON.stringify({ schema: 1, version: value.version, exit: "normal",
    counts: Object.fromEntries(keys.sort().map(key => [key, value.counts[key]])) });
}

// RECEPTOR DESATIVADO. Um prototipo sem autenticação e rate limit nao deve
// receber relatos reais. Este codigo nao tem flag de habilitacao por ambiente.
export default {
  async fetch() {
    return new Response(null, {
      status: 503,
      headers: { "cache-control": "no-store" }
    });
  },
  async scheduled(_event, env) {
    const now = Math.floor(Date.now() / 1000);
    await env.DB.prepare("DELETE FROM daily_counts WHERE expires_at <= ?")
      .bind(now).run();
  }
};
