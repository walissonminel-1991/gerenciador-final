# GERENCIADOR FINAL — metas da expansão funcional VF01–VF16

Registrado em 21/11/2026 por decisão do proprietário de desenvolver as 16 famílias. Os IDs vêm do [backlog da PR #87](https://github.com/walissonminel-1991/gerenciador-final/pull/87). A referência funcional comparada é a [candidata da PR #88](https://github.com/walissonminel-1991/gerenciador-final/pull/88), no SHA `729d9c8912d7f97c677c9af7f0341f2dd4766716`. A [PR #89](https://github.com/walissonminel-1991/gerenciador-final/pull/89), no SHA `d6ae65ca7763972fc1898d69d2cb2d8cde5fee32`, é a primeira candidata de código G3.2, com validação física ainda pendente no momento do registro.

A candidata portátil de **23/11/2026** mantém como prioridade G3.2 e a entrega pessoal autorizada do EXE. A expansão começa na etapa pós-23/11, após o checkpoint da candidata. As ondas indicam a ordem de entrega. As datas das versões de expansão dependem das evidências de cada frente.

## Metas e dependências

| ID | Meta | Cobertura na PR #88 | Onda | Checkpoints relacionados |
|---|---|---|---|---|
| VF01 | Atualização de drivers | Parcial | 1 | `D2`, `D4`, `G1`, `G2`, `G3`, `G4` |
| VF02 | Drivers para jogos | Não localizado na candidata | 2 | `G1`, `G2`, `G3` |
| VF03 | Correção guiada de erros do sistema | Parcial | 1 | `D2`, `D4` |
| VF04 | Backup e restauração | Parcial | 1 | `G2`, `G4` |
| VF05 | Informações do sistema e dispositivos | Parcial | 2 | `D1`, `D2`, `D3`, `D4` |
| VF06 | Desinstalação Win32, Store e aplicativos indesejados | Parcial | 1 | `I1`, `I7`, `R1`, `R4`, `R7` |
| VF07 | Remoção assistida de programas difíceis e maliciosos | Parcial | 2 | `R4`, `R6`, `R7` |
| VF08 | Limpeza de sobras pós-desinstalação | Parcial | 1 | `R2`, `R3`, `R4`, `R6`, `R7` |
| VF09 | Desinstalação em lote | Implementado no escopo atual | 1 | `R4`, `R5`, `R7` |
| VF10 | Gerenciador de extensões de navegadores | Não localizado na candidata | 2 | Nova área |
| VF11 | Monitor de instalações, realocação e descarte seguro | Não localizado na candidata | 3 | `R3`, `R4` |
| VF12 | Limpeza do sistema | Parcial | 1 | `R4`, `P5` |
| VF13 | Otimização e desempenho | Implementado no escopo atual | 1 | `P1`, `P2`, `P3`, `P4`, `P5`, `V5` |
| VF14 | Privacidade, rastros online e impressão digital | Não localizado na candidata | 3 | Nova área |
| VF15 | Segurança, atualizações e proteção contra ameaças | Parcial | 2 | `U1`, `U2`, `U3`, `U4`, `U8` |
| VF16 | Modos manual, automático e assistência por IA | Parcial | 3 | `U3`, `U4`, `V3` |

Os vínculos indicam trabalho ou evidência reutilizável. As metas VF e os [42 checkpoints da linha original](ROADMAP.md#checklist) têm critérios próprios. O placar da expansão começa em **0/16 aceitos**, com dois recursos implementados no escopo atual, dez parciais e quatro não localizados na candidata. O placar dos checkpoints segue **27/42**.

## Entregas e critérios de aceite

### VF01 — Atualização de drivers

**Entrega.** Buscar e instalar drivers oficiais em cenários de dispositivo desatualizado, sem driver ou com falha, reutilizando inventário PnP, WUA, consentimento e diário da G3.

**Aceite.** Demonstrar caso elegível de cada cenário declarado, com ID do dispositivo, identidade e revisão da oferta, origem, confirmação e pós-verificação de versão e PnP. Oferta ambígua, alteração entre consulta e execução ou recuperação indisponível devem bloquear a ação. O diário deve distinguir falha, reinício necessário e resultado incerto.

### VF02 — Drivers para jogos

**Entrega.** Identificar a GPU e oferecer os canais oficiais de drivers para jogos da NVIDIA, AMD e Intel que correspondam ao modelo, ao Windows e à arquitetura.

**Aceite.** Validar pelo menos um modelo suportado por fabricante, exibir canal, versão e origem, distinguir canais de jogo e produtividade quando o fabricante os oferecer e comprovar que produto divergente, assinatura inválida e pacote inelegível são recusados. Reutilizar elegibilidade e recuperação do VF01.

### VF03 — Correção guiada de erros do sistema

**Entrega.** Criar fluxos por sintoma para áudio, rede, tela azul, chipset, impressora offline, USB e arquivos de sistema/DLLs, aproveitando DISM/SFC, rede e PnP.

**Aceite.** Ter um cenário reproduzível por sintoma, diagnóstico consultivo, ação específica quando houver reparo comprovável e nova checagem pós-ação. Se o reparo depender de hardware, política ou diagnóstico especializado, identificar a dependência e oferecer encaminhamento oficial. Restaurar arquivos de sistema por fonte oficial.

### VF04 — Backup e restauração

**Entrega.** Expor backup selecionado e restauração de drivers suportados, inventário dos backups e criação/uso de pontos de restauração pelo Windows.

**Aceite.** Criar e verificar backup com origem, identidade, manifesto e hash, restaurar um pacote elegível em Windows descartável e confirmar o vínculo após restauração. Testar backup alterado e destino incompatível. Exibir separadamente recuperação do driver e restauração do sistema, incluindo disponibilidade da Proteção do Sistema.

### VF05 — Informações do sistema e dispositivos

**Entrega.** Completar dados de sistema e dispositivo e permitir limpar registros de dispositivos anteriormente conectados quando o Windows oferecer operação suportada.

**Aceite.** Distinguir dispositivo presente, ausente e indeterminado. Mostrar prévia da instância, vínculo com o pacote e efeito da ação. Em fixture Windows com dispositivo ausente confirmado, remover só a instância escolhida, reconsultar e confirmar preservação de dispositivo ativo, pacote compartilhado e dados pessoais.

### VF06 — Desinstalação Win32, Store e aplicativos indesejados

**Entrega.** Ampliar a desinstalação de Win32, MSIX/Store e entradas ocultas elegíveis. Identificar bloatware e bundleware por evidência e verificar rotas suportadas para Edge, OneDrive e Copilot por edição e build do Windows.

**Aceite.** Demonstrar Win32, MSIX de usuário e entrada oculta elegível por identidade exata. Para Edge, OneDrive e Copilot, publicar matriz por build/edição com remoção suportada e verificada quando disponível e motivo de proteção ou encaminhamento quando indisponível. Preservar processos, dependências e componentes críticos fora do alvo.

### VF07 — Remoção assistida de programas difíceis e maliciosos

**Entrega.** Guiar diagnóstico e remoção suportada de desinstalador ausente, instalação travada ou cadastro inconsistente. Integrar encaminhamento e ações nativas de quarentena/remoção para detecções da proteção do Windows ou do antivírus ativo.

**Aceite.** Demonstrar falha do desinstalador, identificação inconclusiva e remoção recuperável em alvos sintéticos ou software de laboratório. Para ameaça detectada, mostrar fonte da detecção, ação suportada, resultado e recuperação aplicável. Conservar programas ativos, compartilhados ou de identidade incerta.

### VF08 — Limpeza de sobras pós-desinstalação

**Entrega.** Ampliar arquivos, executáveis, pastas e chaves já tratados para tarefas agendadas e serviços ligados exclusivamente ao programa desinstalado.

**Aceite.** Exibir prévia e evidência de propriedade, executar somente após confirmação e revalidação e informar removidos e preservados. Fixture Windows deve ter candidatos exclusivos e itens compartilhados, ativos, alterados e sem identidade comprovada. Os últimos quatro grupos devem permanecer intactos.

### VF09 — Desinstalação em lote

**Entrega.** Reutilizar seleção, execução sequencial e resultado por aplicativo. Completar o tratamento de cancelamento, falha parcial, pausa e retomada quando suportadas pelo executor.

**Aceite.** Validar fila com sucesso, falha, cancelamento e resultado incerto. Mostrar o estado de cada aplicativo, interromper ações conflitantes e retomar somente entradas elegíveis revalidadas. Reaproveitar evidência existente quando SHA e cenário corresponderem.

### VF10 — Gerenciador de extensões de navegadores

**Entrega.** Listar extensões por navegador e perfil em Chrome, Firefox e Edge e oferecer remoção pela via suportada. Tratar IE somente em ambientes que ainda exponham gerenciamento aplicável.

**Aceite.** Com perfis de teste, identificar origem, ID e estado de gerenciamento. Remover uma extensão comum pelos controles suportados e confirmar que perfis, credenciais, dados e extensões administradas foram preservados. Mostrar encaminhamento para os casos que exigem a interface do navegador.

### VF11 — Monitor de instalações, realocação e descarte seguro

**Entrega.** Registrar mudanças de uma instalação explicitamente monitorada, mover aplicativos que tenham mecanismo de realocação suportado e oferecer descarte seguro compatível com o meio e o método.

**Aceite.** Entregar três provas independentes. A captura local atribui alterações à instalação e é encerrável. A movimentação preserva dados, inicialização do app e uma rota de recuperação. O descarte atua apenas no alvo confirmado e explica o grau de verificação e os limites de mídia, snapshots e backups.

### VF12 — Limpeza do sistema

**Entrega.** Ampliar temporários, Lixeira e caches com busca de grandes arquivos, deduplicação por conteúdo, correção de atalhos inválidos e tratamento de entradas de registro com proprietário comprovado.

**Aceite.** A prévia e a execução usam os mesmos critérios. Exibir tamanho, identidade e hash onde aplicável, testar link, arquivo em uso, guarda de ao menos uma cópia no grupo de duplicados e revalidação antes de remover. Atalho ou chave de origem incerta deve ficar em revisão.

### VF13 — Otimização e desempenho

**Entrega.** Consolidar inicialização, CPU/RAM, energia, disco e rede em ações reversíveis e medições antes/depois, usando os controles já existentes.

**Aceite.** Apresentar métrica e condições comparáveis, demonstrar reativação de uma entrada de inicialização, restauração de uma configuração suportada e resultados de ações de disco/rede elegíveis. Documentar quando o ambiente impede concluir sobre ganho de desempenho.

### VF14 — Privacidade, rastros online e impressão digital

**Entrega.** Oferecer limpeza local seletiva de dados por navegador/perfil e redução de rastreabilidade por controles antirrastreamento e proteções contra identificação por impressão digital disponíveis no navegador ou no aplicativo.

**Aceite.** Testar perfis fictícios com cookies, histórico e cache, distinguir categorias e preservar credenciais por padrão. Demonstrar redução das superfícies expostas em teste controlado para pelo menos um perfil suportado e apresentar a proteção disponível por aplicativo. Mostrar limites quando o aplicativo não expuser ajuste legítimo.

### VF15 — Segurança, atualizações e proteção contra ameaças

**Entrega.** Consolidar Windows Update e atualizações de aplicativos, incluindo ferramentas de IA com fontes verificadas. Integrar detecção e resposta a processos/acessos suspeitos e ameaças às ferramentas nativas ou ao provedor ativo.

**Aceite.** Confirmar atualização do Windows e de ferramenta de IA elegível por editor, versão e resultado. Em Windows descartável, demonstrar detecção informada pelo provedor e uma resposta suportada a processo/conexão de teste, com escopo, consentimento, auditoria e reversão da regra onde aplicável. Preservar proteções e exclusões existentes.

### VF16 — Modos manual, automático e assistência por IA

**Entrega.** Oferecer seleção manual, verificações automáticas configuráveis e assistência de IA opcional e local. Reutilizar os mesmos autorizadores e executores em todos os modos.

**Aceite.** Demonstrar plano explicável, seleção manual, simulação, cancelamento, verificação automática ativada pelo usuário e recomendação local com indicação da evidência. A IA gera propostas. As confirmações, políticas e limites de cada operação continuam sendo aplicados pelo executor.

## Execução e evidências

- Antes de iniciar uma meta, conferir o SHA vigente e reaproveitar os motores e evidências compatíveis. Cada PR registra sua meta VF, mudanças, cenário positivo e recusas relevantes.
- Validar primeiro no menor cenário útil. Vincular a evidência ao SHA e, quando houver execução, ao artefato e ao Windows usado. Usar fixtures para regras e integração e Windows descartável ou equipamento de teste previsto para efeitos reais exigidos no aceite.
- Nas ações que alterem o sistema, usar identidade explícita do alvo, prévia quando aplicável, confirmação, revalidação, auditoria local e pós-verificação. Para operações reversíveis, exercitar a recuperação. Estados parciais ou ambíguos permanecem visíveis como tal.
- O orçamento recorrente permanece **US$0**. Usar recursos nativos e locais sempre que possível. As proteções `GF_BETA`, a política de distribuição e os requisitos de privacidade continuam aplicáveis.
- Atualizar o item correspondente em [PROGRESSO.json](PROGRESSO.json) quando houver prova de aceite. A evidência vincula PR, SHA, ambiente, resultado e limites. O roteiro de entrega está no [roadmap](ROADMAP.md#roadmap-de-entregas).
