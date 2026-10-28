# Integração final — Etapa 8: preparação da BETA LOCAL

28/10/2026, America/Sao_Paulo. Somente Etapa 8 nesta sessão.
Branch work-integracao-final-20261027, [PR #65 aberta/Draft](https://github.com/walissonminel-1991/gerenciador-final/pull/65).

**Estado: BLOQUEADA para formar e entregar o pacote executável privado.**
A conferência técnica, a proposta de entrega e o roteiro estão documentados.
Não existe ZIP executável disponível, entregue ou homologado nesta sessão.
Etapas 1–7 não foram reexecutadas; nenhum defeito funcional novo foi demonstrado.
**Etapa 9 não iniciada. BETA conectada/pública continua bloqueada.**

## A. Entrada e fonte exata

| Referência relida no GitHub | Resultado |
| --- | --- |
| HEAD documental de entrada | 4ccb20c8e724d9d630b288e3cd8e997c12daad80 |
| Árvore documental | 4fcaae411b05bb9e1f7361d037d5fb15f89186a8, 447 blobs |
| Fonte corrigida e aprovada | 6df95fdd776644cd4b248b5cea61108974ca9ce9 |
| Árvore da fonte | e29acb3619b85050a4cf5becaf81bfa3fdda0bd2, 445 blobs |
| Merge sintético testado | 384f0a96acb8ccc5f99cccb0a930d8a9cedc50ad, mesma árvore da fonte |
| Base da PR | 2bc9f4228fd448445fbf181652e5af93005def20, preservada |
| Versão/identificação | Gerenciador Final v5.13.11.66 — BETA LOCAL TESTE — NÃO HOMOLOGADA |

Checkpoint, matriz, adendo local, correções 7 e defeitos foram relidos.
Os 447 blobs de entrada foram conferidos localmente por SHA Git. Contra a
fonte aprovada, existem exatamente onze diferenças Markdown/JSON de
continuidade, incluindo dois documentos novos; nenhuma remoção ou mudança
em código, testes, workflows, projeto, recursos ou arquivos publicados.
LICENSE, PRIVACY.md, guia portátil, aviso BETA e avisos de terceiros são
idênticos aos da fonte de CI. Não há diferença executável que exija nova CI.

## B. Compilação existente e escopo da aprovação

| CI da própria candidata, tentativa 1 | Estado reconferido |
| --- | --- |
| [40675784213 — Windows/principal](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40675784213) | completed/success, head_sha 6df95fdd776644cd4b248b5cea61108974ca9ce9 |
| [40675784155 — integridade externa](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40675784155) | completed/success, mesmo head_sha |

Sete jobs obrigatórios aprovados e dois condicionais skipped conforme desenho.
Os logs existentes confirmam 18/18 verificações locais, callback automático
zero, transporte fictício zero, saída normal/falha sem nova pendência e
reabertura sem coleta. Não houve repetição dessas suítes nesta etapa.

No job 122777811188, publish self-contained x64, identidade Win32 e
CreateActCtxW passaram. A inspeção do assembly exigiu perfil local e ausência
de endpoint/aprovações conectadas. O verificador fechado aprovou **287
arquivos e 286 hashes**, AMD64, 153.051.536 bytes expandidos na CI.
Os runtimes Microsoft.NETCore.App e Microsoft.WindowsDesktop.App eram
10.0.13; o SDK em global.json é 10.0.402, rollForward=disable.
Uma nova build privada deverá verificar os seus próprios bytes e metadados;
não herda automaticamente os hashes da compilação do merge sintético.

## C. Impedimento preciso e artefato realmente disponível

| Controle/evidência | Consequência nesta sessão |
| --- | --- |
| build-windows.yml, linhas 158–168: GF_BETA publica somente BETA-BUILD.json, BUILD-INFO.txt e SHA256SUMS.txt | O EXE/DLL/runtime não foram conservados como artefato recuperável |
| Mesmo workflow, linhas 170–177: upload de Distribuicao exige beta != true | Alterar o símbolo/resultado BETA ou adicionar upload paralelo contornaria o gate; não realizado |
| test_politicas_seguranca.py mantém a restrição de upload BETA | Nenhum teste/gate foi removido ou afrouxado |
| BETA-BUILD.json: distribution=requires_owner_approval | A build técnica não declara entrega aprovada |
| Repositório consultado: public, private=false | Artefato neste repositório não oferece exclusividade ao mantenedor |
| Nenhum canal privado seletivo/receita aprovada para o pacote local foi fornecido ou encontrado no contrato | Não existe URL privada verificável para preencher a entrega |
| Ambiente Work Linux sem .NET/PowerShell/Windows | Não há ambiente local de build/validação Windows usado como substituto |

O [workflow na fonte validada](https://github.com/walissonminel-1991/gerenciador-final/blob/6df95fdd776644cd4b248b5cea61108974ca9ce9/.github/workflows/build-windows.yml#L158) mantém esses gates.
O acesso a artefatos exige sessão GitHub e leitura do repositório; autenticação
não equivale a acesso exclusivo do mantenedor. Referência primária consultada:
[GitHub — baixar artefatos](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/download-workflow-artifacts).
Não foi criado repositório, workflow alternativo, Release, upload ou canal
externo para escapar da restrição. Scripts genéricos de desenvolvimento não
foram executados como substitutos de candidata com proveniência validada.

O arquivo docs/BETA-APROVACOES.json continua distribution_approved=false,
com aprovações conectadas pendentes. Esse arquivo pertence ao fluxo conectado:
**não exigir endpoint, Worker, D1 ou aprovação do aviso remoto para viabilizar
a edição local** e não alterá-lo para simular autorização local. A instrução
do mantenedor autoriza a preparação local dentro dos controles; não oferece
uma exceção executável ao upload somente de manifestos.

| Entrega executável solicitada | Estado real |
| --- | --- |
| Nome reservado, apenas proposta | GerenciadorFinal-v5.13.11.66-6df95fdd-BETA-LOCAL-TESTE-NAO-HOMOLOGADA-win-x64.zip |
| Arquivo executável privado criado/disponível | Não |
| SHA-256 do pacote executável | Indisponível; não calculado nem inferido |
| Link privado do pacote executável | Inexistente; nenhum link apresentado como entrega |
| Conteúdo integral/varredura independente de binários | Não acessível fora da CI; não afirmar ausência comprovada de segredos no pacote inteiro |

Existe somente o artefato de evidência
[BETA-manifesto-6df95fdd, ID 12811032013](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40675784213/artifacts/12811032013),
13.932 bytes, sem expiração na consulta, vencimento informado 04/11/2026,
11:04:39 UTC. **Esse acesso não é um link privado de pacote executável.**

Ele foi baixado novamente e conferido, sem executar o GF:

- Exatamente três arquivos: BETA-BUILD.json, BUILD-INFO.txt, SHA256SUMS.txt.
- SHA-256 do ZIP de manifestos:
  50b7a8c4d8bc15a826ebe2d54c7ada8f291d6cd2dbd19b8f805572f962c4c35c.
- Fonte/versão esperadas, perfil local no BUILD-INFO,
  transport_configured=false, endpoint e hash de aprovação vazios.
- 286 entradas de checksum com nomes únicos e sem caminhos de escape;
  hashes dos dois documentos incluídos conferidos contra o manifesto.
- Dependências/avisos essenciais listados, sem nomes de scripts, fixtures,
  arquivos de credenciais ou certificados privados no manifesto.

Essas verificações não permitem reconstruir os outros 284 arquivos a partir
dos hashes, examinar o conteúdo dos seus binários ou produzir o hash de um
ZIP executável inexistente. Nenhum pacote foi entregue.

## D. Confirmações de segurança e limites da inspeção

| Requisito | Evidência preservada / limite |
| --- | --- |
| GF_BETA_LOCAL | Perfil local padrão no projeto, macro condicional, metadados e 18 verificações no SHA aprovado |
| Identificação NÃO HOMOLOGADA | Program.cs:661–662 e aviso local; janela real coberta na CI |
| Abertura sem HTTP/WUA automático | BeginStartupChecks aplica a política antes da cadeia de consultas; callback zero na CI |
| Consultas explícitas permitidas | Caminhos consultivos e controles oficiais existentes preservados; nenhuma consulta do produto foi iniciada nesta sessão |
| Coleta/gravação/transmissão remota | Barreiras compiladas no gravador, lease, endpoint e transporte; consentimento herdado, falha, saída e reabertura cobertos |
| Relatórios locais | DiagnosticTrace e LocalReliabilityHistory preservados; dados locais não são transmitidos |
| Drivers/WinGet/exclusão permanente | Nenhum arquivo funcional alterado; gestão de drivers indisponível, COM opcional/consultivo, alvo excluído somente inventário |
| Identidade/hash/assinatura/arquitetura/consentimento/dados | Guardas e regressões da fonte aprovada intactos |
| Privilégios | app.manifest 5.13.11.66, asInvoker, uiAccess=false; sem elevação obrigatória na abertura |
| Dependências | Target net10.0-windows10.0.26100.0; YamlDotNet 18.1.0, ComInterop 1.29.380 e runtimes resolvidos conferidos pela CI |
| Licenças/recursos | MIT, avisos dos packs Microsoft/WinGet e cinco documentos publicados presentes no contrato/manifesto; recursos incorporados e ícone do projeto intactos |
| Segredos/endpoints | Nenhum endpoint aprovado/ativo no assembly local; manifesto fechado sem arquivos de segredo; binários completos indisponíveis para varredura independente nesta sessão |
| Serviços/instalador/infraestrutura | Nada adicionado; sem Worker/D1/DNS/EU.org, instalador automático ou serviço persistente para funcionar localmente |

PILOT_ENABLED=no e GITHUB_EXPORT_ENABLED=no versionados são preservados.
Não foi acessada ou alterada configuração operacional remota.
AVG, permissões do Windows e proteções não foram modificados.
Não houve operação nos PCs, atualização/desinstalação real ou gestão de driver.
Não foi feita consulta, verificação ou teste específico do alvo permanentemente
excluído, nem reinvestigação de WinGet COM.

## E. Alternativa privada proposta, sem execução

**Proposta concreta: compilação privada controlada pelo mantenedor, em ambiente
Windows x64 descartável, separada do repositório público e do PC de uso.**
O fluxo público continua com upload somente de manifestos.
Não criar uma segunda rota pública, trocar o resultado beta para false ou
usar o fluxo conectado. A proposta requer primeiro uma decisão local de
distribuição registrada e um destino com controle de acesso comprovado.

Registro proposto, ainda não aprovado/implementado como gate:

| Campo da decisão local | Valor/condição |
| --- | --- |
| Destinatário único | Mantenedor walissonminel-1991; não voluntários ou terceiros |
| Escopo | Apenas v5.13.11.66 BETA LOCAL TESTE — NÃO HOMOLOGADA |
| Fonte e CI | 6df95fdd776644cd4b248b5cea61108974ca9ce9; dois runs aprovados acima |
| Perfil obrigatório | local/GF_BETA_LOCAL; GF_BETA mantido |
| Canal | Armazenamento privado escolhido pelo mantenedor, acesso nominal exclusivo, sem opção “qualquer pessoa com link”; ainda não definido/verificado |
| Build | Windows x64 descartável, SDK fixo, diretório novo; sem depuração em PC pessoal |
| Proteções | Diagnóstico remoto e gestão de drivers desativados; AVG ativo; nenhuma mudança conectada |
| Registro da entrega | Identificador único da build, SHA fonte/build, relatório de validação, lista de arquivos e SHA-256 dos bytes finais |
| Situação | Proposta; nenhum canal provisionado, pacote gerado, upload efetuado ou autorização conectada alterada |

Após decisão específica compatível com o contrato, a continuação da Etapa 8
deverá: obter fonte imutável; conferir novamente CI; compilar no perfil local;
inspecionar assembly sem iniciar o GF; manter CreateActCtxW, verificador
fechado de pacote, licenças, metadados, checksums e inspeção de segredos;
gerar ZIP sem sobrescrever saídas anteriores; verificar os bytes finais e
transferir somente ao mantenedor pelo canal nominal verificado. Registrar
hash também por referência confiável separada. Não prometer reprodutibilidade
binária automática entre builds/contextos distintos.

O roteiro e o recibo de entrega podem acompanhar o ZIP separadamente: não
inserir arquivos extras no diretório publicado sem revisão do contrato fechado.
O bloqueio só se encerra com pacote real, SHA-256 e acesso privado comprovado.
Este relatório não cria nem executa essa exceção.

## F. Uso portátil, limitações e retorno

[Guia e roteiro físico mínimo preparados](WORK-INTEGRACAO-ETAPA9-PC-FORTE-ROTEIRO.md).
São instruções condicionais, não solicitação de execução nesta sessão.
Incluem conferência de integridade/versão, extração em pasta nova, AVG ativo,
amostras comparáveis, privacidade dos relatos e retorno sem sobrescrever a
versão anterior. Portátil não significa ausência de dados locais:
diagnósticos/histórico podem ser gravados em AppData/TEMP segundo PRIVACY.md.

Persistem AVG/Authenticode, validação física de DPI/foco/CPU/RAM, cobertura
física e Issue #48 aberta. WUA nos runners sem ofertas/vínculos não homologa
G1 em hardware com oferta. GF6-001–004 e GF7-001 permanecem corrigidos no
escopo técnico; nenhum novo defeito foi demonstrado nesta preparação.
Não declarar BETA pronta, pacote entregue ou missão ampliada concluída.
Roadmap permanece **27/42**, sem promoção dos 42 IDs/done.

## G. Continuidade e parada

[Checkpoint](WORK-INTEGRACAO-CHECKPOINT.md) · [Matriz](WORK-INTEGRACAO-MATRIZ.md) ·
[Recibo da Etapa 8](evidencias/integracao-etapa8-preparacao.json) ·
[Correções 7](WORK-INTEGRACAO-ETAPA7-CORRECOES.md) · [Defeitos](WORK-INTEGRACAO-DEFEITOS.md).

Próxima ação: resolver a decisão/canal privado e concluir a entrega dentro da
**Etapa 8**, em outra sessão. Etapa 9 continua dependente de pacote real
verificado e autorização manual. Nenhum teste físico iniciado ou solicitado.
Commits desta sessão são documentais; a CI citada continua pertencendo ao SHA
fonte aprovado, sem transferir aprovação para uma build inexistente.
**Encerrar aqui; não iniciar Etapa 9 nem liberar BETA conectada/pública.**

## H. Retomada para resolver a entrega — decisão específica pendente

28/10/2026, America/Sao_Paulo. Esta seção complementa o registro anterior,
sem substituir suas evidências. HEAD de entrada
0facf42518f91c60ea9a9055de5ab81683c9d63d; fonte 6df95fdd e dois runs
40675784213/40675784155 reconferidos success/tentativa 1. Nenhuma mudança
funcional desde a fonte de CI; quinze documentos de continuidade.

[Proposta P8-ENTREGA-01, ações A1/A2/A3 e procedimento verificável](WORK-INTEGRACAO-ETAPA8-ENTREGA-PRIVADA-PROPOSTA.md) ·
[Recibo da retomada](evidencias/integracao-etapa8-entrega-privada.json).

A proposta define recurso privado separado, runner Windows x64 descartável,
verificação independente, versão/fonte/SDK/perfil fixados, manifesto/licenças/
conteúdo, hash do ZIP real e entrega autenticada exclusiva. Mantém intacto o
workflow público. Repositório privado de outra finalidade não foi reutilizado.
Nenhum recurso privado/workflow novo foi criado, acesso concedido ou binário
transferido; essas ações dependem de autorização específica conforme o pedido
atual. Não há ferramenta Windows acessível nesta sessão, e criação/acionamento
não são expostos pelo conector. Aceite não comprova capacidade operacional.

**Etapa 8 permanece BLOQUEADA; Etapa 9 NÃO INICIADA.** Pacote executável,
SHA-256 e URL privados continuam indisponíveis. Não houve nova build/CI ou
repetição das Etapas 1–7. O próximo passo é decidir A1/A2/A3 e confirmar o
meio de implantação/acionamento, sem antecipar teste físico ou fluxo conectado.
