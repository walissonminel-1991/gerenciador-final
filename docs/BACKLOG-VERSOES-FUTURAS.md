# GERENCIADOR FINAL — backlog funcional para versões futuras

**Registro:** 21/11/2026 · **Natureza:** planejamento, sem autorização para implementação.
**Origem:** 16 grupos de recursos solicitados pelo proprietário, comparados ao código existente e ao roadmap.
**Horizonte:** somente **após a entrega da candidata portátil de 23/11/2026** e uma decisão expressa de priorização.

> Este backlog é **separado** do roadmap vigente: **27/42 checkpoints permanecem inalterados**. Os identificadores VF01–VF16 **não são checkpoints adicionais** dos 42, não representam funcionalidades concluídas e não ampliam o compromisso da candidata de 23/11. Não realizar merge, release ou modificações funcionais por causa deste documento.

## Regras de execução

1. **Primeiro fechar o produto atual:** interface e integração funcional de Drivers/G3, Aplicativos, resíduos, atualizações, limpeza e demais abas conforme escopo já aprovado. Não duplicar motores nem reabrir regressões concluídas.
2. **Após 23/11, reavaliar a base exata:** identificar funcionalidade já presente, parcialmente integrada ou ausente antes de programar cada item; registrar prova no código e no Windows quando a função envolver efeitos reais.
3. **Incremental e isolado:** uma frente por vez, PR Draft específica, critério de aceite verificável, rollback/recuperação quando tecnicamente suportado; não interferir em `main`, branches/PRs existentes, G5/autoatualizador ou infraestrutura.
4. **Segurança e consentimento:** privilegiar APIs nativas/documentadas e fontes oficiais, prévia, alvo explícito, revalidação, confirmação, auditoria local e bloqueio conservador diante de ambiguidade. Não executar alterações destrutivas em massa sem autorização específica.
5. **Não prometer universalidade:** drivers compatíveis, reversão, desinstalação difícil, ganho de desempenho, limpeza de registro e proteção antimalware dependem de limitações reais do Windows e das evidências obtidas.
6. **Orçamento:** manter princípio de custo recorrente US$0; não introduzir serviço hospedado, pagamentos, dependência de IA em nuvem ou coleta de dados sem decisão expressa.

## Ondas sugeridas (priorização sujeita a revisão)

- **Onda A — complemento de manutenção útil (P1):** VF01, VF03, VF04, VF06, VF08, VF12, VF13, VF16. Priorizar lacunas reais; *não reimplementar recursos já existentes*.
- **Onda B — ampliação de cobertura (P2):** VF02, VF05, VF07, VF09, VF10, VF15.
- **Onda C — experiências opcionais/sensíveis (P3):** VF11 e VF14; partes inadequadas ficam explicitamente fora de escopo.

## Matriz de funcionalidades futuras

| ID | Grupo | Estado-base observado em 21/11/2026 | Incremento futuro proposto | Critério mínimo de aceite |
|---|---|---|---|---|
| **VF01** | Atualização de drivers ausentes, com falhas ou desatualizados | Inventário/PnP, consulta oficial WUA, backend e diário existem em branches; interface nativa G3.1 bloqueia instalação | Consolidar cobertura por dispositivo/estado, interoperando com G1–G5 atuais e sem novo motor | Seleção PnP inequívoca, elegibilidade oficial, consentimento, operação real controlada, resultado pós-operação e falha recuperável; *não antecipar desbloqueio de G3*. |
| **VF02** | Drivers para jogos — NVIDIA/AMD/Intel | Consulta WUA não comprova versões Game Ready/Adrenalin/Arc mais recentes | Integrações opcionais com páginas/APIs oficiais de fabricantes e identificação de GPU | Modelo e SO compatíveis, versão/origem exibidas, assinatura verificada e confirmação individual; não afirmar que WUA oferece sempre o mais recente. |
| **VF03** | Correção guiada de áudio, rede, BSOD, chipset, impressora, USB e DLLs | DISM/SFC, rede e verificações Windows específicas existem | Diagnósticos guiados por sintoma com resultados verificáveis e reparos graduais | Cada sintoma possui teste reproduzível, prévia, opção apenas diagnosticar, reparo específico e resultado/limitação explícitos; não baixar DLL de sites terceiros. |
| **VF04** | Backup, restauração de drivers e ponto de restauração | Serviço de ponto de restauração e exportação OEM específica antes de remoção já existem | Inventário de backups, exportação selecionada e restauração suportada | Integridade, origem e alvo validados; caminho de recuperação documentado/testado; ponto de restauração nunca tratado como backup pessoal nem rollback universal. |
| **VF05** | Informações de hardware e histórico de dispositivos conectados | WMI, detalhes de hardware, PnP e histórico consultivo existem | Relatórios mais completos; remoção opcional de *registros órfãos* comprovados, não de dispositivos ativos | Diferencia dispositivos presentes/ausentes, requer seleção inequívoca, não toca dados do usuário nem drivers ativos e apresenta prévia. |
| **VF06** | Desinstalação Win32/MSIX/Store/bloatware | Win32 e lote implementados; MSIX/Store inventariados, muitos somente leitura | Ampliar remoção suportada por API/Windows com distinção entre app do usuário e componente protegido | Remoção verificada por identidade/pacote; componentes críticos preservados; Edge/OneDrive/Copilot não recebem remoção forçada genérica. |
| **VF07** | Remoção de programas difíceis | Há mecanismos conservadores de desinstalação e bloqueios | Diagnóstico de travamentos, desinstaladores inválidos e reparos de cadastro quando seguros | Prova da identidade e exclusividade, acompanhamento e conciliação; software malicioso é encaminhado à proteção do Windows/AV, sem prometer eliminação universal. |
| **VF08** | Limpeza de resíduos pós-desinstalação | Auditoria, prévia, atribuição exclusiva e exclusão contextual já desenvolvidas | Cobertura adicional de tarefas e serviços apenas quando comprovadamente exclusivos | Nenhum arquivo/registro/serviço compartilhado ou dado pessoal afetado; evidência antes/depois e relatório de preservados. |
| **VF09** | Desinstalação em lote | Seleção e execução sequencial já implementadas | Melhorar pausa, retomada segura, relatório por item e tratamento de pendências | Identidade e confirmação por lote, execução não paralela de ações conflitantes, sem repetir itens com resultado incerto. |
| **VF10** | Extensões de navegadores | Não localizada implementação dedicada na análise preliminar | Inventário local de extensões Chrome/Edge/Firefox e opções oficiais de remoção; IE somente se aplicável | Sem apagar perfil, senhas ou dados pessoais; remoção respeita APIs/políticas do navegador; extensões gerenciadas informadas como protegidas. |
| **VF11** | Monitor de instalações, realocação e descarte seguro | Não identificada cobertura completa | Monitoramento local **opt-in** de mudanças; realocação apenas por método suportado; avaliar utilitário de descarte | Registro local revogável, sem vigilância persistente oculta; movimentação com integridade e reversibilidade; não prometer apagamento irreversível em SSD. |
| **VF12** | Limpeza adicional, duplicados, grandes arquivos e atalhos | Temporários, caches, Lixeira e ferramentas nativas já existem | Análise informativa de duplicados/grandes arquivos e correção seletiva de atalhos | Prévia por caminho/identidade/hash; nunca apagar duplicados automaticamente nem “otimizar” Registro por heurística genérica. |
| **VF13** | Desempenho, inicialização, RAM, disco e rede | Recursos de RAM, energia, inicialização, rede e ferramentas Windows implementados | Consolidar indicadores, comparativo antes/depois e recomendações reversíveis | Medições reproduzíveis, configurações restauráveis, nenhuma promessa de ganho nem limpeza agressiva de memória de terceiros. |
| **VF14** | Privacidade e rastros de navegação | Consentimento e telemetria do GF existem, não equivalem a limpeza de navegadores | Opções locais por navegador de limpar caches/rastros **apenas com escolha explícita** | Prévia das categorias afetadas, preservação de credenciais e perfis por padrão; **não incluir falsificação ou disfarce de fingerprint**. |
| **VF15** | Segurança, atualizações de software/IA e spyware | Atualizações/Windows Update e diagnósticos de segurança já existem | Painel de status, atualização de aplicativos com fonte verificada e orientação para proteção do Windows | Não desativar antivírus; não substituir scanner antimalware; validar editor, pacote, versão e resultado das atualizações. |
| **VF16** | Modos manual, automático e assistência inteligente | Ações manuais e automatizações pontuais existem | Perfis *Diagnosticar*, *Revisar e aplicar* e execução limitada de itens pré-aprovados; recomendações inteligentes opcionais | Plano explicável, simulação/prévia, consentimento por classe de ação, parada/cancelamento e relatório; IA nunca autoriza exclusões/instalações críticas sozinha. |

## O que **não** entra automaticamente

- Remoção forçada de componentes essenciais do Windows ou de mecanismos de segurança.
- Exclusão indiscriminada do Registro, Driver Store, serviços, tarefas ou arquivos com proprietário incerto.
- Substituição de antivírus, bypass de proteção, instalação de drivers sem compatibilidade/assinatura comprovadas.
- “Otimização” de RAM/disco/rede sem métrica ou reversão; garantia de eliminação irreversível de arquivos em SSD.
- Disfarce/falsificação de identidade digital de navegadores.
- Serviços remotos pagos ou telemetria adicional sem consentimento e autorização do proprietário.

## Condição para transformar item em meta

Somente **depois da candidata de 23/11 e de uma decisão do proprietário**: escolher uma onda ou ID, definir branch/PR isolada, verificar se já existe motor reutilizável, registrar critérios de aceitação, riscos e teste em Windows, e então inserir no roadmap de uma **versão futura explicitamente nomeada**. Até essa decisão, todos os itens ficam em **backlog pendente**, sem prazo e sem alterar **27/42**.

## Referências de continuidade

- [Roadmap funcional vigente](ROADMAP.md) — os 42 checkpoints atuais permanecem intactos.
- [PR #84 — preservação R7/G3 REV3](https://github.com/walissonminel-1991/gerenciador-final/pull/84).
- [PR #85 — preservação candidata de integração](https://github.com/walissonminel-1991/gerenciador-final/pull/85).
- [PR #86 — UX Drivers, Draft](https://github.com/walissonminel-1991/gerenciador-final/pull/86).

*Documento de planejamento. Nenhum teste, instalação, remoção, merge ou compilação é autorizada por ele.*
