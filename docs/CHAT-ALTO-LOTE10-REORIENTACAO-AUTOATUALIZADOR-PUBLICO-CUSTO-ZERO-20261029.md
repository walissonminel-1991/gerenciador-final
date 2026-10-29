# GERENCIADOR FINAL — CHAT ALTO — Lote 10
## Reorientação arquitetural: autoatualização pública, gratuita, sem cadastro e com privilégios mínimos
**Data:** 29/10/2026  
**Estado:** ADR experimental/documental — PROPOSTA de arquitetura, NÃO implementação nem autorização de publicação  
**Gates:** G1–G8 BLOCKED (0/8 homologados)

### 0. Baseline, preservação e limites desta rodada

- Repositório: walissonminel-1991/gerenciador-final.
- Branch exclusiva: experimento-g5-ipc-chat-alto-20261029; HEAD de entrada confirmado: 4c5dfedd409c570b3dc1de07bd5c9b237ebcb962. Comparação com a referência inicial: zero commits de diferença.
- PR #65: Draft, aberta e sem merge; branch work-integracao-final-20261027 no commit ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc, confirmado antes desta redação.
- Foram consultados os checkpoints dos Lotes 5, 6, 7 e 8, a matriz G1, o dossiê de revisão e a Etapa 13 do WORK. O parecer crítico WORK G1/G2/G8 anterior foi recuperado em síntese contextual, mas a sua **íntegra não é um arquivo verificável nesta branch**; não alegar auditoria documental completa dessa peça. Os principais alertas recuperados: autoridade restaurável no mesmo disco, seleção antes da barreira composta, idempotência pós-restart, timeout sem cancelamento de I/O, TOCTOU, divergência de interop TUF e manutenção D6-02.
- Evidência L5 existente: CI Windows 40922528166, seis fixtures aprovadas, sem witness durável ou recibos operacionais. Etapa 12 e Etapa 13 mantêm G2 bloqueado. Nenhuma fixture prova homologação física ou autoriza seleção/instalação.
- O csproj do produto permanece net10.0-windows10.0.26100.0, WinForms, versão 5.13.11.66, com distribuição de runtime lado a lado; protótipos isolados possuem fontes .cs.txt e não compilam no produto.
- Este lote cria **somente** este ADR. Nenhum teste, build, serviço, chave, segredo, commit em branch protegida, CI, release, deploy, solicitação de licença, telemetria, consentimento ou máquina pessoal é autorizado ou modificado.

### 1. Decisões do mantenedor recebidas — não extrapolar

**D1 Custo zero:** nenhum custo obrigatório, serviço pago, domínio pago ou certificado comercial obrigatório. Cotas gratuitas podem desaparecer; nenhuma opção Free equivale a SLA.

**D2 Distribuição anônima:** sem login, cadastro por computador, e-mail, token do usuário, inscrição per-device ou código pessoal de recuperação. GitHub pode hospedar artefatos públicos, mas o usuário não precisa de conta GitHub.

**D3 Rede sem controle remoto:** a API de autoatualização só publica bytes de metadados e pacotes. Proibir comando remoto, PowerShell/script enviado por rede, URL de execução arbitrária, ações de limpeza, alteração de registro, serviços/drivers e payload de instrução privilegiada.

**D4 Offline:** abrir e usar GF sem internet; apenas a atualização do próprio GF exige acesso eventual ao repositório público. Outras funcionalidades seguem suas regras independentes.

**D5 TPM opcional:** não bloquear máquinas antigas, sem TPM ou com TPM limpo.

**D6 Recuperação simples:** falha de atualização preserva GF anterior e oferece mensagem clara; não exige atendimento do mantenedor por instalação.

**D7 Não falsificar G1:** sem âncora externa de continuidade individual e resistente à restauração integral, **rollback do estado local completo não está resolvido**. Assumir risco residual ou propor prova pública sem cadastro; decisão formal de escopo ainda pendente.

Decisões acima são requisitos de produto. Não equivalem a aceite de uma implementação ou a fechar qualquer gate.

### 2. Decisão técnica recomendada: separar autenticidade, continuidade e privilégio

**Arquitetura-base proposta, a validar:** TUF público com raiz editorial pré-distribuída e verificada na primeira distribuição, múltiplos papéis e assinaturas por limiar conforme perfil adotado; metadados e pacotes de download públicos; verificador local com estado durável e políticas estritas de instalação. **Não existe testemunha por instalação, identidade por computador ou backend de escrita do cliente.**

- **Autenticidade:** assinatura/limiar de TUF e cadeia de hashes/comprimentos ligam root → timestamp → snapshot → targets → pacote → manifesto. Nunca confiar apenas no HTTPS, nome da release, número mostrado na UI, checksum publicado no mesmo site ou rótulo GitHub Verified.
- **Continuidade local:** manter piso de versões/épocas da raiz e metadados TUF e piso de release admitida; impedir regressão com estado local íntegro. Retomada idempotente de transações e recuperação em falhas ficam obrigatórias.
- **Frescor:** expirações de metadados e tempo de tentativa imutável conforme TUF; relógio Windows manipulado, máquina offline e replay de dados ainda válidos são ameaças distintas. O sistema não conhece a última publicação em termos absolutos sem autoridade de tempo ou referência atual confiável.
- **Capacidade de alterar Windows:** nenhuma resposta de rede autoriza ação fora do envelope de atualização do GF. Um pacote assinável contém código executável capaz de causar danos ao rodar: assinatura prova autorização de publicação, **não inocuidade do código**. Exigem-se revisão da release, testes, privilégio mínimo, escopo de escrita estrito, recuperação e separação entre dados de rede e operações locais.
- **Bootstrap:** a primeira cópia do verificador e a raiz TUF precisam chegar por distribuição inicial confiável. Uma cópia inicial falsificada pode embutir chaves maliciosas; TUF não corrige o problema da origem inicial sozinho. Referência de hash na mesma página não é segundo domínio independente.
- **Rotação:** proteger chaves de Root/Targets offline; distinguir autoridade de release de Timestamp/Snapshot; rotação de root requer continuidade autenticada pelos limiares antigo e novo, com testes de invalidação conjunta timestamp/snapshot quando houver rotação de suas chaves. Não colocar chave privada de publicação no GF.
- **Certificação do EXE:** assinaturas TUF podem ser produzidas com software livre sem comprar certificado Authenticode. Isso **não** equivale a assinatura Authenticode reconhecida pelo Windows nem elimina alertas SmartScreen. Authenticode, quando presente e verificável por WinVerifyTrust, é defesa adicional independente a avaliar; a ausência de certificado pago não libera execução de conteúdo sem validação TUF.

**Perfil público sem contas:** cliente → consulta de metadados TUF em endpoint público fixado → verificação local → download de pacote exato → verificação integral → preparação local inerte → ativação restrita. Nenhum endpoint POST de inscrição, identificador de hardware, secret global, telemetria ou execução de ordem remota é necessário para esta função.

### 3. Contrato de rede mínimo — proposta (não protocolo implementado)

1. Aceitar somente HTTPS e hosts explicitamente permitidos por configuração embarcada e autenticada; redirecionamentos e mirrors exigem regras explícitas, não derivações de strings fornecidas por servidor.
2. Apenas GET/HEAD quando justificado; nenhum token de escrita, conta ou registro de instalação. Não transmitir lista de programas, caminhos pessoais, número de série, e-mail, identificador persistente ou eventos de diagnóstico pelo canal de update. O servidor poderá observar IP e user-agent; analisar privacidade antes da ativação.
3. Limitar tamanho de metadados, ZIP e arquivos extraídos; tempo total, conexões, hierarquia de roles, compactação e profundidade de archive. Tratar dados de rede sempre como não confiáveis.
4. Aceitar somente esquema/algoritmos/canonicalização estritamente suportados, chaves e limiares autenticados pela raiz, versões e expirações válidas, comprimento/hash e nome de target autorizados.
5. Resposta remota é **dado**, nunca comando. Proibir shell, argumentos arbitrários, scripts, extensão de PATH, atualização de driver, agendador, serviços, registro, políticas Windows ou ações de limpeza pelo manifesto.
6. Usar somente artefatos da própria distribuição GF e canal de atualização previsto; impedir downgrade cross-channel (beta versus estável), mudança de arquitetura, runtime incompatível ou troca imprevista de identidade de publisher.
7. Quando servidor falha, fica indisponível, 404/429/5xx, certificado TLS falha, relógio é inconclusivo ou metadados são inválidos: não instalar, manter GF anterior, registrar motivo técnico local e permitir tentar novamente mais tarde. Nunca contornar validação como fallback.
8. A publicação por GitHub Releases ou hospedagem estática gratuita é apenas transporte; commits, tags e URLs alteráveis não se tornam autoridade de confiança. Disponibilidade e política de cotas exigem validação antes de escolher hospedagem.

### 4. Modelo de atualização do GF no Windows — envelope restrito

**Sequência pretendida, ainda sem código operacional:**

1. Processo principal apenas oferece “Verificar atualizações” e apresenta resultado; verificação/download não requer admin.
2. Verificador isolado e previamente instalado/carregado pela distribuição confiável valida TUF, package/manifest, versão, arquitetura, hashes, comprimento e compatibilidade. Payload não inicia processo, não chama shell, não executa script.
3. Extrair apenas em diretório de staging de propriedade apropriada, com caminhos relativos canonizados, sem traversal, symlink/junction/reparse point e sem dispositivos/caminhos absolutos. Não atravessar limite da pasta GF nem permitir DLL planting.
4. Fechar GF de maneira cooperativa e obter lock local; revalidar antes da ativação os exatos bytes previamente atestados, identidade do arquivo aberto e autorização de diretório. TOCTOU pós-fechamento de handles continua risco a ensaiar.
5. Preferir versões lado a lado sob raiz de instalação controlada, preservando release anterior completa e manifesto de runtime; ativar com pequeno seletor/ponteiro transacional após validar viabilidade no NTFS. Renomear vários arquivos não é transação atômica; não alegar atomicidade de árvore nem resistência a power loss sem prova.
6. Nenhuma alteração automática em Documentos, Downloads, Fotos, Música, Desktop, outras aplicações, registro, serviços, drivers, boot ou conteúdo de sistema. A pasta de dados/configuração do usuário é preservada e fica fora da área substituível.
7. Instalação portátil em pasta comum: normalmente operar com privilégios do usuário, sem elevar automaticamente. Pasta protegida (Program Files): **não** converter resposta de rede em elevação; qualquer futuro helper privilegiado deve ser código local confiável, com interface tipada e permissão exclusivamente para atualizar GF, validando autoridade novamente. UAC pode ser necessário e será decisão explícita de UX.
8. Registro de transação e estado pending/failure com durabilidade adequada; na falha, tentar iniciar versão anterior sem apagar a nova evidência diagnóstica. Falha do disco ou malware/admin podem impedir recuperação; não prometer recuperação universal.
9. Versão nova só passa a ser efetiva após validação da ativação e sinal claro do processo esperado. Não considerar READY sintético, socket/nome de processo ou mera existência de arquivo como confirmação.
10. Manual download/instalação continua possível por fluxo autenticado e sem conta. Um usuário pode reinstalar versão antiga deliberadamente; distinguir controle sobre self-update do controle total do Windows.

**Princípio:** reduzir a capacidade do mecanismo atualizador, mas reconhecer que código GF assinado e executado sob privilégios do usuário ainda pode acessar recursos que o usuário acessa; não é sandbox de segurança garantida.

### 5. G1 sem identidade: três garantias que não podem ser confundidas

| Propriedade | TUF público + estado local | Dependência adicional |
|---|---|---|
| Rejeitar pacote trocado/falso com raiz inicial íntegra | Projetada; ainda exige implementação e homologação | Chaves protegidas, perfil TUF correto, hash/length, controle da release |
| Detectar rollback de metadados/versão enquanto o estado local persistiu | Projetada; ainda exige crash/power-loss tests | Persistência NTFS, epoch/pisos, relógio conforme TUF |
| Detectar rollback após restauração da **imagem inteira** do disco, inclusive verificador, root, relógio e estado | **NÃO garantida** | Referência externa autêntica, recente, não restaurável no mesmo domínio, ou escopo de risco explicitamente aceito |

**Contraexemplo G1-R10:** um PC tinha aceitado release R20; backup integral anterior a esse fato contém aplicativo, piso, chaves, diários e relógio em R10. Após restore, um adversário apresenta metadados e release R11 assinados, ainda admissíveis no contexto temporal do cliente restaurado. O cliente pode aceitar R11 por não ter memória confiável de R20. Hash, assinatura válida, nonce local, HTTPS, TPM opcional não resolvem o contraexemplo. Se o binário antigo estiver vulnerável, restauração pode também recuperar o verificador vulnerável.

**Referência pública comum a todos (estudo opcional):** um checkpoint global de versão/época da release pode ser assinado por autoridade separada e publicado sem instalaçãoId, cadastro ou POST. Para garantir que é **atual** após restore, seria necessário frescor verificável e proteção contra replay, idealmente por origem/administração independente e monitoração de consistência. Um JSON assinado hospedado na mesma conta de publicação ou uma segunda página controlada pelo mesmo operador não resolve A5 nem frescor diante de replay. Log de transparência pode detectar equivocação quando há monitor independente e prova de consistência; não vira contador monotônico imune à restauração por sua mera existência. Privacidade melhora porque o checkpoint não é por dispositivo, mas custo e custódia ainda exigem análise.

**Recomendação de escopo:** para o primeiro protótipo, testar a arquitetura pública sem witness individual; manter G1 BLOCKED enquanto não existir decisão explícita sobre risco residual de restauração integral e verificador substituído. Não converter aceitação de risco em prova técnica.

### 6. Ameaças e reação esperada

| Ameaça / falha | Resposta exigida ou limite |
|---|---|
| MITM, pacote alterado, JSON falsificado, ZIP manipulado | Bloqueio antes de qualquer escrita executável; validação de limiar + encadeamento de hash/length |
| Replay local comum / release anterior após atualização íntegra | Recusa por piso de root/metadados/release; testes de rotação legítima para evitar falsos bloqueios |
| Restauração integral do disco e relógio | Limitação G1-R10 não resolvida; não usar linguagem “antirrollback absoluto” |
| Editor/root de publicação comprometido | Revogar/rotacionar com processo independente; se limiar de Root comprometido, rebootstrap autenticado fora da banda; nenhuma atualização automática sob confiança suspeita |
| Build CI ou release maliciosa porém validamente assinada | Assinatura não fornece sandbox; defesa por revisão, proveniência, isolamento de chaves, política de release e menor privilégio |
| Manifesto tenta executar shell ou limpar C:\Users | Formato incapaz de expressar essa ação; rejeição; alvo de teste negativo |
| ZIP traversal, junction, symlink, hard link, DLL planting e TOCTOU | Rejeitar antes de preparar; isolar staging e testar identidade/ACL/handles em Windows |
| Crash entre download, preparação e ativação | Manter estado pending + rollback/reconciliação idempotente, sem sucesso falso |
| Perda de resposta, timeout, sync write após prazo G8 | Estado UNKNOWN/PENDING; não assumir cancelamento de escrita; não liberar seleção/confirm |
| Sessão rodando com privilégios de administrador | Não herdar autoridade ampla como permissão de pacote; restringir updater, alvo, helper e operações locais |
| Sem internet, quotas de hospedagem e bloqueio do antivírus | GF básico permanece funcional; self-update indisponível; não instruir o usuário a desabilitar antivírus ou SmartScreen |
| Troca de hardware / TPM ausente / reinstalação | Sem inscrição ou recuperação por pessoa; reinstalação confiável com nova base local; mesmo risco residual G1 de perda de histórico |
| Operador público comprometido / equivocation A5 | TUF protege conteúdo dentro da confiança editorial íntegra, mas não prova continuidade global após perda de estado; exigiria domínio independente e revisão do bootstrap |

### 7. Plano de evidências — não executado nesta rodada

**L10-A — contrato e arquitetura isolados:** congelar esquema de metadados, chaves de TESTE e limiares, política de versão de canal, raiz inicial, UX e allowlist de escrita. Revisar com WORK uma vez após haver diferenças técnicas substanciais, não depois de cada documento.

**L10-B — test harness inerte:** reutilizar Stage11/Stage12/L5 em projeto separado .cs.txt, sem conectar executável GF; introduzir adaptador de transporte GET fictício e manifesto declarativo. Foco em inputs hostis e limites de privilégio.

**L10-C — testes negativos Windows/CI descartável:** casos prioritários:
- L10-01 assinatura correta e bytes de target errados → reject;
- L10-02 rotação root legítima com invalidação timestamp/snapshot conforme TUF → accept em fixture, sem autorizar produção;
- L10-03 replay antigo com piso local intacto → reject;
- L10-04 restore integral incluindo piso/relógio → contraexemplo reproduzido, garantia NÃO alcançada;
- L10-05 resposta com “execute powershell / delete Windows” → impossível executar/reject;
- L10-06 archive traversal e caminhos absolutos → reject;
- L10-07 reparse point/junction trocado entre check e write → reject ou estado explicitamente não resolvido;
- L10-08 bitness/canal/runtime incompatível → reject;
- L10-09 GF portátil sem admin instala somente no escopo permitido → teste;
- L10-10 pasta Program Files sem helper homologado → falha segura, sem prompt oculto;
- L10-11 perda de resposta após commit + reboot → reconsulta sem instalação dupla;
- L10-12 timeout G8 com gravação tardia → UNKNOWN, jamais aprovado;
- L10-13 interrupção/energia → preservação do boot anterior, validar em máquina descartável;
- L10-14 substituição de updater/launcher e DLL injection → resposta segura segundo ameaça assumida, sem alegar defesa contra admin malicioso;
- L10-15 offline/HTTP 429/repo indisponível → todo GF não relacionado ao self-update permanece funcional;
- L10-16 relógio movido para trás e metadados expirados → fail-closed da atualização, demais funções disponíveis.
Resultados exigem logs de teste com versões/SHAs e classificação PASS/FAIL/NOT_RUN/INCONCLUSIVE; não contar testes não realizados.

**L10-D — homologação posterior:** somente com autorização separada, revisão independente TUF/G2 e D6-02, laboratório Windows físico descartável, NTFS com interrupções, presença de antivírus, avaliação de runtime/portable, segurança do helper, análise de release, recuperação e UX. Nenhum teste em PC FORTE/FRACO como efeito deste documento.

### 8. Decisões técnicas que permanecem abertas

1. **Risco G1 de restore integral:** o mantenedor aceita um escopo inicial que não garante antirrollback quando todo o estado confiável é restaurado, ou exige primeiro referência pública independente e fresca? Não presumir aceite.
2. **Chaves:** perfil de limiar para root/targets, papéis online/offline, rotação/revogação e guardião humano responsável; sem guardião e política, não publicar.
3. **Distribuição inicial confiável:** como fornecer bootstrap autêntico sem certificado pago; distinguir verificação verificável da experiência SmartScreen e risco de binário inicial falsificado.
4. **Hospedagem pública:** validar se GitHub Releases/Pages com quotas e limites atende ao uso, sem segredos na aplicação e sem credenciais de usuário. Não escolher/reconfigurar hospedagem nesta fase.
5. **Updater/instalação portátil:** permitir atualização sem admin somente em diretório seguro; política explícita para Program Files e para instalação em locais graváveis por terceiros.
6. **Tempo e expiração:** clocks, offline prolongado e recuperação quando metadata root/time expiram; não aceitar atualização insegura por conveniência.
7. **G2/D6-02:** revisão qualificada da conformidade TUF, divergências da referência Python e aceite formal de manutenção de dependências/patches.
8. **G5/G6/G8:** processo de instalação real, prontidão autêntica, durabilidade/NTFS/timeout e transações entre diários independentes seguem BLOCKED.

### 9. Critérios para CONCLUIR o Lote 10, não fechar gates

- [x] HEAD da branch experimental e diff desde checkpoint conferidos.
- [x] PR #65 Draft e SHA de referência conferidos; nenhuma alteração de linhas protegidas.
- [x] Lotes 5–8, matriz/dossiê G1 e Etapa 13 lidos; síntese contextual WORK consultada com limite explicitado.
- [x] Modelo público sem identidade por instalação e custo obrigatório zero documentado.
- [x] Distinção entre autenticidade, rollback local, restore integral, frescor e poder do executável explicitada.
- [x] Contrato de rede estrito, menor privilégio, offline e recuperação sem conta definidos como requisitos.
- [ ] Modelo implementado e testes negativos executados.
- [ ] Revisão WORK do novo modelo, parecer externo formal ou revisão qualificada de G2.
- [ ] Aceite de risco G1, manutenção D6-02, homologação Windows e liberação operacional.

**Status ao finalizar:** avanço documental de direção, NÃO avanço de gate. G1–G8 BLOCKED (0/8). Nenhum instalador, self-update real, chave operacional, backend, CI, release, deploy, assinatura operacional ou comunicação remota foi criado neste lote.

### 10. Fontes primárias para auditoria futura

- TUF Specification v1.0.36, 05/08/2026: https://theupdateframework.github.io/specification/v1.0.36/ — §§ 5.1–5.6, incluindo invalidação conjunta de timestamp e snapshot em 5.3.11.
- TUF Roles: https://theupdateframework.org/docs/metadata/
- Microsoft WinVerifyTrust: https://learn.microsoft.com/en-us/windows/win32/api/wintrust/nf-wintrust-winverifytrust
- Microsoft, opções de assinatura e distribuição: https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options
- Evidências do repositório: docs/CHAT-ALTO-LOTE5-MESMOS-BYTES-TUF-STAGE2-L4-CHECKPOINT-20261029.md, docs/CHAT-ALTO-LOTE6-ADR-TESTEMUNHA-G1-INDEPENDENTE-20261029.md, docs/CHAT-ALTO-LOTE7-DECISAO-CONDICIONAL-G1-AMEACAS-RECUPERACAO-20261029.md, docs/CHAT-ALTO-LOTE8-CONTRATO-BOOTSTRAP-ENROLLMENT-RECOVERY-G1-20261029.md, docs/WORK-AUTOATUALIZADOR-ETAPA13.md, docs/CHAT-ALTO-DOSSIE-REVISAO-INDEPENDENTE-G1-G2-G8-20261029.md.

**Fim do Lote 10 documental — nenhum gate de segurança homologado.**
