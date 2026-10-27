# Integração final — Etapa 3: regressões e CI

27/10/2026, America/Fortaleza. PR #65 Draft, branch `work-integracao-final-20261027`.
**EM EXECUÇÃO.** Entrada `89ee12a8322c5872c61c3fd40030490407cbb173`; código integrado anteriormente em
`06d7dce0bb47944f1efd18a10785fa77f5f0c8f7`.

[Checkpoint](WORK-INTEGRACAO-CHECKPOINT.md) · [Matriz](WORK-INTEGRACAO-MATRIZ.md) ·
[Recibo](evidencias/integracao-etapa3-ci.json) · [Adendo](WORK-INTEGRACAO-ADENDO-BETA-LOCAL.md).

Validar a própria árvore por SHA exato com os workflows existentes: regressões,
privacidade/consentimento, receptor/retenção sintéticos, drivers somente leitura
em runners descartáveis, interface/MSIX/desempenho/encerramento, integridade
externa autorizada e compilação interna Windows x64. Nenhuma operação nos PCs,
instalação de driver, transmissão ao receptor privado, deploy ou distribuição.

O build BETA mantém upload somente de manifestos/hashes/evidências, sem EXE/ZIP.
Endpoint/allowlist/hash revisado vazios; aprovações falsas; flags no. VLC somente
inventário sem ações/testes específicos/consultas, WinGet COM opcional consultivo,
AVG/proteções preservados. Roadmap 27/42.

Registrar aqui os runs, seus head/merge SHAs, resultados por área, defeitos
reproduzidos/correções necessárias, verificação de artefatos e limitações.
Somente concluir com CI aplicável aprovada. Parar antes da Etapa 4.
