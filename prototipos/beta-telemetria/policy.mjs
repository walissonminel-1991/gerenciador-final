// Valores revisados em fonte: nao aceitar versoes arbitrarias como chave no D1.
export const betaVersion = "v5.13.11.66";
export const allowed = new Set([
  "APP.FALHA_INTERFACE", "APP.FALHA_NAO_TRATADA",
  "REGISTRO_PROGRAMAS.FALHA", "MSIX.LEITURA",
  "MULTIFONTES.CONSULTA_FALHA", "MULTIFONTES.CONSULTA_CANCELADA",
  "DESINSTALACAO.FALHA", "INSTALACAO_OFICIAL.FALHA",
  "TERMINAL_GITHUB.FALHA", "JANELA.BANDEJA_RESTAURAR.INICIO",
  "JANELA.BANDEJA_RESTAURAR.FIM", "JANELA.BANDEJA_OCULTAR.INICIO",
  "JANELA.BANDEJA_OCULTAR.FIM", "DIAGNOSTICO_COMPLETO.FIM"
]);
export const secondsPerDay = 86400;
// Prazo contado do dia da coleta, nunca da exportacao/retry (nao prolongar dados).
export const expiresForDay = day => (day + 7) * secondsPerDay;
const description = "Somente contagens por versao/categoria. Nenhum diagnostico individual, IP ou identificador de PC.";
const dateForDay = day => new Date(day * secondsPerDay * 1000).toISOString().slice(0, 10);
const marker = day => `<!-- gf-beta-aggregate:v2 day=${day} -->`;

export function issueContent(day, rows, operationId = null) {
  const date = dateForDay(day);
  const proof = operationId ? `<!-- gf-beta-aggregate:v3 day=${day} operation=${operationId} -->` : marker(day);
  const lines = [proof, "# Dados agregados GF BETA — " + date, "", description, ""];
  for (const row of rows) {
    if (row.version !== betaVersion || !allowed.has(row.event) ||
        !Number.isSafeInteger(row.total) || row.total < 1 || row.total > 10000000)
      throw new Error("invalid_aggregate");
    // Atenuar contagens pequenas; NAO representa k pessoas nem anonimato.
    if (row.total >= 5) lines.push(`- ${row.version} · ${row.event}: ${Math.floor(row.total / 5) * 5}`);
  }
  if (lines.length === 5) return null;
  return { title: "GF BETA — estatísticas agregadas " + date, body: lines.join("\n") };
}

// Recuperar apenas o formato exato produzido por este receptor, incluindo v1.
// Titulo isolado, PRs, comentarios ou conteudo livre nunca autorizam exclusao.
export function managedDay(issue) {
  if (!issue || issue.pull_request || !Number.isSafeInteger(issue.number) || issue.number < 1 ||
      typeof issue.id !== "string" || !/^[A-Za-z0-9_=-]{1,200}$/.test(issue.id) ||
      typeof issue.body !== "string" || issue.body.length > 12000) return null;
  const date = /^GF BETA — estatísticas agregadas (\d{4}-\d{2}-\d{2})$/.exec(issue.title)?.[1];
  if (!date) return null;
  const day = Date.parse(date + "T00:00:00Z") / (secondsPerDay * 1000);
  if (!Number.isSafeInteger(day) || day < 0 || dateForDay(day) !== date) return null;
  const lines = issue.body.split("\n");
  if (lines[0] === marker(day) || /^<!-- gf-beta-aggregate:v3 day=\d+ operation=[0-9a-f-]{36} -->$/.test(lines[0])) lines.shift();
  if (lines.shift() !== "# Dados agregados GF BETA — " + date || lines.shift() !== "" ||
      lines.shift() !== description || lines.shift() !== "" || lines.length < 1 || lines.length > 14) return null;
  const seen = new Set();
  for (const line of lines) {
    const m = /^- (v\d{1,3}(?:\.\d{1,4}){3}) · ([A-Z._]+): ([1-9]\d{0,7})$/.exec(line);
    if (!m || !allowed.has(m[2]) || Number(m[3]) > 10000000 || seen.has(m[1] + m[2])) return null;
    seen.add(m[1] + m[2]);
  }
  return day;
}

// Correlacao de uma operacao do SERVIDOR, nunca identidade de participante/PC.
// Texto/titulo isolados nao provam propriedade. O nonce e o autor esperado
// precisam ter sido registrados antes do POST. Marcador removido => triagem.
export function matchesObligation(issue, job) {
  if (!job?.operation_id || !job.creator_id || issue?.author?.id !== job.creator_id ||
      typeof issue.body !== "string" || !Number.isSafeInteger(issue.number) ||
      issue.number < 1 || typeof issue.id !== "string" || !/^[A-Za-z0-9_=-]{1,200}$/.test(issue.id)) return false;
  return issue.body.split("\n")[0] ===
    `<!-- gf-beta-aggregate:v3 day=${job.day} operation=${job.operation_id} -->`;
}

// Compartilhado por entrada e respostas remotas: limite real de bytes e tempo.
export async function boundedText(stream, maxBytes, timeoutMs = 2500) {
  const reader = stream?.getReader();
  if (!reader) return null;
  let timer;
  const timeout = new Promise((_, reject) => {
    timer = setTimeout(() => reject(new Error("body_timeout")), timeoutMs);
  });
  const chunks = [];
  let size = 0;
  try {
    for (;;) {
      const { done, value } = await Promise.race([reader.read(), timeout]);
      if (done) break;
      size += value.byteLength;
      if (size > maxBytes) return { oversize: true };
      chunks.push(value);
    }
    const bytes = new Uint8Array(size);
    let offset = 0;
    for (const part of chunks) { bytes.set(part, offset); offset += part.byteLength; }
    return { raw: new TextDecoder("utf-8", { fatal: true }).decode(bytes), size };
  } catch { return null; }
  finally {
    clearTimeout(timer);
    // Nao aguardar cancelamento de uma origem lenta/travada.
    void reader.cancel().catch(() => {});
    reader.releaseLock();
  }
}
