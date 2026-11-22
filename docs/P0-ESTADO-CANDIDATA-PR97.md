# GF — Estado da candidata P0 (#97) e pendências

**Fonte funcional imutável:** PR Draft #97, HEAD d0a5156ab548cf4365ca9d1babe44deaca0da6bf, árvore d806562f703c4245b035ce589b4f132181637ae9. [Windows](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/44966279577) e [integridade](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/44966279588) aprovados. **Sem ZIP pessoal entregue e sem homologação física.** Progresso 27/42; expansão VF 0/16.

## P2-01 — Documentação (corrigida nesta branch)
README, PROGRESSO e ROADMAP apontam para a candidata .66, preservando o histórico .65. Critérios VF01–VF16 recuperados da PR #90, sem aceitar metas automaticamente. A comparação 2 implementadas / 10 parciais / 4 não localizadas pertence à PR #88 histórica, e não à #97.

## P1-02 — G2: integridade integral de driver e recuperação (aberto)
A [PR #83](https://github.com/walissonminel-1991/gerenciador-final/pull/83) foi encerrada NO-GO sem merge: 0 pacotes inteiros integralmente verificados nos runners Windows 2022/2025, apesar de INFs assinados. Não tratar assinatura INF como prova integral. G3.2 exige elegibilidade, revalidação, backup OEM, consentimento e diário; não permite ampliar indiscriminadamente as classes. Reabrir G2 só após prova positiva de pacote completo real, teste negativo alterado, identidade da origem e cobertura de todos os membros; recusar Incomplete/Rejected. Recuperação física e pós-verificação permanecem pendentes.

## P1-01 — Cobertura incompleta (aberto)
O botão global **Atualizar todos** de Aplicativos não está presente. Aceite: inventário completo, candidatos exatos aptos, confirmação única, revalidação por item, executor preexistente, resultado individual e cancelamento; nenhuma oferta consultiva pode ser instalada automaticamente. Drivers só têm classes restritas elegíveis. As 16 metas e aceites individuais estão em [BACKLOG-VERSOES-FUTURAS.md](BACKLOG-VERSOES-FUTURAS.md); 0/16 aceitas e sem motores duplicados.

## P0-02 — Homologação do conjunto (aberto)
Primeiro, somente consultas nos dois PCs: conferir SHA/versão, AVG ativo, abertura como usuário normal sem UAC, abas/bandeja, Drivers GPU/USB/Detalhes, Windows Update consulta/cancelamento, cinco categorias de Limpeza com prévia/cancelamento, Reparo e Rede, Inicialização, Sistema e desempenho. Depois, somente com autorização específica e alvo elegível, uma operação real de cada módulo com efeito, backup/reversão e pós-verificação. Sem afirmar homologação por CI.

## P0-01 — Entrega pessoal do ZIP (aberto)
O runner compilou EXE/runtime x64, mas o workflow público GF_BETA guardou apenas BETA-BUILD.json, BUILD-INFO.txt e SHA256SUMS.txt (286 entradas). Rota antiga de gf-beta-entregas fixada à fonte #78. Usar apenas a rota local preexistente, num **host Windows x64 autorizado**, com Git e SDK 10.0.402: conferir SHA da fonte e GfSourceCommit, executar Publicar-Portatil-Windows-x64.bat, inspecionar EXE/runtime/manifesto, conferir hashes e calcular SHA-256 do ZIP final. Não modificar GF_BETA/workflows/infraestrutura, não fazer merge nem Release pública.
