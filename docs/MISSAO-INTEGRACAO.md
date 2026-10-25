# Integração experimental — Lotes 1–4 e WORK14–WORK17

Data: 25/10/2026. **Candidata v5.13.11.64 TESTE: TECNICAMENTE APROVADA NA CI WINDOWS; HOMOLOGAÇÃO FÍSICA PENDENTE.**

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

## Fechamento técnico da candidata .64
- Fonte selecionada: `5793ce1f5816309902207311bc5c171fe62616fd`; compilação de PR `538c7f7696eddc8a4977fcb6ef940d922cb5447a`; base `cb7ca5cc`.
- [CI Windows https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40241712164](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40241712164) aprovada integralmente na tentativa 2: política de privacidade, integridade de instaladores, regressões Windows, drivers Windows 2022/2025 e compilação x64; opt-in de consultas extras e atestação exclusiva da main permaneceram fora do escopo.
- Regressões incluíram Lote 3 e 4, WinGet COM somente leitura, provedores WORK14/16/17 e **30/30 mutações adversariais rejeitadas** em cópias descartáveis.
- A primeira passagem da fonte `5793ce1f` recusou um espelho VLC com redirect para HTTP. O job foi **reexecutado sem alterar código ou relaxar HTTPS/hash/assinatura**; todas as verificações externas passaram na segunda tentativa.
- [Artefato ZIP TESTE GerenciadorFinal-v5.13.11.64-5793ce1f-TESTE-win-x64](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40241712164/artifacts/12618005183) com **59.440.421 bytes**, SHA-256 `7d659b35319e2389c6266ef4fcee25e1733f0f5fa9c36558a0e38e66b12919c9` (digest retornado pela API GitHub).
- Artefato transferido e reconferido fora do runner, sem executar: **285 arquivos, 284 SHA-256 internos corretos**, sem nomes duplicados ou travessias, **152.980.804 bytes** expandidos, Win64 PE **AMD64**, `BUILD-INFO` exatamente vinculado ao código, build e base, runtimes .NET/WindowsDesktop 10.0.13 e NuGet COM 1.29.380.
- Distribuição sem assinatura Authenticode. Manifesto `asInvoker` aprovado na CI; AVG não escaneado nem homologado. Artefatos temporários do GitHub expiram em **01/11/2026**.
- **Homologação em PC FORTE/FRACO não realizada**. A main, a PR #40 e a linhagem original WORK14–17 continuam intactas. Nenhum merge ou release estável. Roadmap permanece 27/42.
