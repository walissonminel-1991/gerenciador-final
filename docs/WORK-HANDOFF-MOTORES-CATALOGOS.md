# WORK — evolução de motores e catálogos do GF (handoff da PR de escopo residual)

> Documento técnico para a segunda frente de trabalho. Leia o código atual antes de alterar arquivos. Não é uma declaração de homologação nem autoriza operar no PC do usuário sem consentimento.

## Base obrigatória e isolamento
- Repositório: `walissonminel-1991/gerenciador-final`.
- Começar lendo as PRs **#40 (candidata), #47 (estados pós-lançamento)** e a PR de `auditoria-gf-escopo-catalogo-20261025` (base #47), além da **issue #48**.
- Checar SHA e branch efetivos antes de criar commits. Trabalhar em **branch nova a partir da nossa branch de escopo quando ela estiver íntegra**, com PR **Draft** apontando para essa branch. Não fazer merge nem alterar `main`, tags, releases, branches históricas ou artefatos já emitidos.
- Não repetir trabalho implementado: `IntegratedResidualPolicy.Build` limita regras explícitas **per-user** a HKCU Registry64; `Prepare` não incorpora chave com fingerprint falhado; `TestesResiduos/Work12RegressionTests` exercita esse limite; `docs/PROGRESSO.json` já registra evidências de PC FORTE sem concluir checkpoints.
- Usar `GerenciadorAplicativos.csproj`, `docs/TESTES.md`, `docs/ORGANIZACAO-REPOSITORIO.md`, `docs/ROADMAP.md`, `docs/PROGRESSO.json`, `docs/MISSAO-WORK13.md` e `docs/TESTE-CONSOLIDADO.md` como fontes. Preservar evidências históricas únicas.

## Diagnóstico conhecido (não confundir inferência com causa demonstrada)
- Catálogo de identidades embutido: 27 regras; catálogo de limpeza específica: **7 regras**, declaradas no JSON como exclusivos *per-user*.
- No teste do PC FORTE foram realizadas duas atualizações silenciosas (SumatraPDF e Notepad++), ambas com download/validações, retorno 0 e versão `Confirmed`, e duas desinstalações silenciosas com `RegistrationAbsent` após múltiplas leituras. Não foi comprovada limpeza forense de todos os vestígios.
- A inspeção residual das duas desinstalações registrou `prepare_hklm32` e `prepare_hklm64` com `registry_unreadable / 5`, ficando `Inconclusive`, sem alvos comprovados para excluir. **Não sabemos ainda em qual segmento/ACL falhou**. A correção de escopo evita inspeção HKLM indevida para catálogo per-user, mas não valida cobertura HKLM de políticas novas.
- A última consulta com 171 aplicativos apresentava 102 sem correspondência no catálogo, 6 a 8 candidatos conforme momento, 4 incertos. Esses números são contextuais, **não** medem taxa global de compatibilidade.
- Testes automatizados anteriores: 318 atualização e 645 resíduos/desinstalação; quantidade de testes sozinha não altera checkpoints. O roadmap permanece **27/42**, com U8/R7/R4 e vários outros pontos em aberto.
- Arquivos de `C:\Windows\Prefetch`, MRU, dados pessoais e chaves compartilhadas mostrados por ferramentas de terceiros **não são prova de propriedade exclusiva** e não devem passar à lista de exclusão por nome.

## Missões técnicas em ordem de prioridade

### A — Modelo de capacidade verificável
1. Investigar o fluxo de `ApplicationIdentityCatalog`, `UninstallRuleCatalog`, `IntegratedResidualPolicy`, `AutomaticAppInstallation`, `AutomaticPackageManifest`, `AutomaticUpdateDiscovery` e `SilentUninstallPolicy`.
2. Propor e implementar distinção clara entre: inventário somente leitura, atualização verificável, desinstalação interativa, desinstalação silenciosa comprovada e limpeza residual explicitamente autorizada. Uma identidade de catálogo nunca é sozinha permissão de executar ou apagar.
3. Criar matriz/diagnóstico local de capacidade por aplicativo, com **denominadores e motivos** (sem correspondência, versão não comparável, assinatura, mecanismo, escopo, dependências, MSIX, ambiguidade, acesso negado, sem regra residual). Manter detalhes sensíveis locais; apenas contagens genéricas em relatórios públicos. Não confundir ausência de regra com ausência de resíduos.
4. Usar um único mecanismo de identidade normalizada para reduzir divergências entre atualização/desinstalação, sem associar produtos pelo nome parecido ou trocar arquitetura/escopo/usuário.

### B — Reestruturar a limpeza residual, sem exclusão heurística
1. Confirmar com testes somente leitura se o erro 5 apareceu ao abrir pai, chave final, enumerar ou fazer fingerprint; diferenciar falta de acesso, chave ausente, escopo não autorizado, inventário parcial e alvo alterado. Jamais declarar inexistente um local não lido.
2. Manter **catálogo per-user = HKCU**. Se houver evidência suficiente de uma regra HKLM exclusiva, exigir **regra diferenciada, versionada e revisada**, com compatibilidade de vista 32/64 e prova de propriedade antes/depois; não fazer promoção automática de uma regra HKCU.
3. Tratar regras `registered-install` com maior rigor de âmbito e vinculação por identidade. Não incorporar uma chave que não passou no fingerprint na fase de preparação.
4. Separar `verificação completa dentro do escopo comprovado` de `abrangência de todos os vestígios possíveis`. A UI deve descrever locais efetivamente verificados, inacessíveis, preservados e elegíveis, sem prometer limpeza universal.
5. Garantir preservação por padrão de: dados pessoais, perfis, credenciais, arquivos de usuário, jogos/saves, drivers, componentes Windows, instalações de outra conta, raízes compartilhadas, symlinks/reparse, Prefetch e MRU. Sem elevar privilégios automaticamente, alterar ACLs, forçar tomada de posse nem excluir chaves/fichários baseados apenas em nome.
6. Exclusão permitida apenas após remoção confirmada, proposta visível, consentimento único, revalidação de Registro, filesystem e inventário, execução com direitos mínimos e relatório separando removido, preservado, erro e pendente. Reboot apenas quando tecnicamente justificado e sem limpeza adicional automática.

### C — Atualização e desinstalação silenciosas de abrangência progressiva
1. Instrumentar cobertura real de Win32 MSI/WiX, NSIS e Inno por família/arquitetura/escopo, em relação ao inventário da máquina, sem alegar universalidade.
2. Melhorar reconhecimento de instalação **somente quando houver verificação de fornecedor, assinatura, hash, identidade do executável, versão e preflight**. Não executar scripts ou switches recebidos de terceiros.
3. Preservar `UpdateLaunchGate` e `UninstallBatch`: operação iniciada + falha de pós-verificação não é cancelamento de UAC nem autorização para repetição. Estados indeterminados continuam indeterminados.
4. MSIX/Store, servidores, runtimes, drivers, componentes compartilhados e qualquer instalação com evidência insuficiente seguem sem execução genérica. Fornecer URL oficial quando cabível.
5. Priorizar casos de prova de incremento concreto (fixtures assinados/instalações isoladas explicitamente autorizadas), nunca atualizar componentes compartilhados apenas para produzir um número.

### D — Qualidade, medição e roadmap
1. Reproduzir primeiro, com testes descartáveis Windows, a insuficiência exata que será corrigida. Adicionar testes de sucesso e adversariais proporcionais à mudança; não multiplicar baterias de testes só para aumentar o contador.
2. Executar toda a CI obrigatória e conferir package provenance, manifesto, recursos de licença, integridade, SHA-256, assinatura de fornecedores e ausência de código de fixtures no produto. Não executar instaladores reais na CI.
3. Comparar antes/depois uma matriz de cobertura real: inventariados, elegíveis por motor, apenas consultivos, recusados por motivo, limpáveis com prova, cobertos parcialmente, inconclusivos. Relatar denominadores, amostra, limite e diferença de comportamento.
4. Atualizar `docs/PROGRESSO.json` e `docs/ROADMAP.md` com estados intermediários e links de evidência, **sem marcar U8, R7, R4 ou elevar 27/42** sem todos os critérios físicos. PC FORTE comprovado não substitui PC FRACO.
5. Manter relatório curto de commits, arquivos alterados, regressões/CI, riscos remanescentes, compatibilidade, limitações e um roteiro físico mínimo separado PC FORTE/PC FRACO.

## Critérios de aceite e parada
- Melhorias implementadas em branch e PR Draft, CI Windows verde, comportamento novo mensurável com casos reproduzidos (não apenas mais testes).
- `Inconclusive` não vira `Complete` por ocultar erro de permissão, desabilitar verificações ou reduzir silenciosamente a cobertura declarada.
- Não apagar dados de usuários nem executar atualizações/desinstalações reais sem autorização explícita para cada alvo e contexto. No Work autônomo, só fixtures isoladas; no computador físico, orientar testes supervisionados, jamais executar remotamente sem acesso/autorização.
- Se bloquear por política de segurança, API, ausência de evidência ou acesso físico, **documentar o bloqueio com precisão**, manter o estado não concluído e avançar em outras tarefas independentes. Não improvisar bypass.
- Antes de concluir, comparar com os commits da nossa PR para eliminar conflitos, identificar o que já foi feito e não duplicar. Entregar PR revisável e resumo objetivo de ganhos práticos, métricas honestas e pendências.
