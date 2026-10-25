# Integração experimental — Lotes 1–4 e WORK14–WORK17

Data: 25/10/2026. **Candidata v5.13.11.64 TESTE: AINDA NÃO APROVADA.**

## Referências
- [PR #59](https://github.com/walissonminel-1991/gerenciador-final/pull/59) — branch de integração isolada, sem merge na main.
- Base Lote 4: `cb7ca5ccd986fd11c768ee410ac7c2b46296e1c1`; WORK17: `96d5f60f9cd28615e5c1d00792c16879eea724fe`; ancestral: `ef13aef49083f357f6a14f4de46f8bc976dbd830`.
- [PR #58](https://github.com/walissonminel-1991/gerenciador-final/pull/58) é apenas a comparação temporária no sentido inverso; não é alvo de merge.

## Integração efetuada
- Importados 71 blobs exclusivos da cadeia WORK14–17 sem alterar seu conteúdo: motor de catálogo, provedores, ecossistemas, resíduos, testes e documentos.
- Conciliados Program.cs, DarkControls, versões dos projetos, testes, progresso, autorização de lançamento, desinstalação e a política de empacotamento via base comum.
- O workflow conserva **os nove gates exclusivos de WORK14–17 e os testes dos lotes 1–4**. Os testes sintéticos não autorizam instalações nem exclusões reais.
- Erros pós-lançamento permanecem Pending/Indeterminate quando a conferência falha; a perda de confirmação não permite repetir nem prosseguir com limpeza.
- Verificador de distribuição mantém contrato fechado e versão exata de Microsoft.WindowsPackageManager.ComInterop 1.29.380; binários não declarados seguem recusados.
- Controle negativo Lote 4 troca só o método de relatório anterior, evitando sobrescrever a definição atual de ProgramEntry da cadeia WORK17.

## Evidência preliminar e limites
- [CI inicial da integração 40240588572](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40240588572) **falhou**, corretamente, na lista antiga de dependências e em teste regressivo que substituía Program.cs completo. Drivers Windows Server 2022/2025 e validação de instaladores (sem execução) passaram nesse run, mas não aprovam a integração.
- Correções submetidas; a versão .64 depende de nova CI Windows obrigatória, política Python/Node, testes adversariais, builds, inspeção de hashes e origem, e revisão visual.
- Nenhum instalador de terceiros foi executado em PC pessoal por esta integração. Nada foi mesclado na main, publicado como release estável ou assinado pelo fornecedor.
- AVG P6, desempenho físico P5, validação de drivers D2/G1/G2 e demais checkpoints não foram encerrados. **Roadmap: 27/42.**

## Critério para avançar
Todos os jobs obrigatórios verdes no mesmo HEAD, verificações pós-build sem afrouxar contratos, e artefato exato aprovado e verificado antes de propor homologação física separada. Falhas de fontes externas continuam recusas, não motivo para habilitar fallback inseguro.
