# WORK14 — motores e capacidades verificáveis

24/10/2026 (Brasil). Candidata .60 em validação. PR Draft #50 direcionada à branch da PR #49; base imutável `954949eef8ee84785ee6434f964b6f1e3c9e0b50`, CI base 40151238332 aprovada. PRs #40/#47/#49, issue #48, handoff, roadmap, PROGRESSO, TESTES e WORK10–13 examinados. Main e branches anteriores preservadas. Nenhuma operação nos computadores pessoais.

## Causas identificadas e implementação

1. `RegistryKey.OpenBaseKey` marca a raiz como writable. Ao consultar `.Handle`, o .NET reabre essa raiz com `KEY_READ | KEY_WRITE`, antes do `RegOpenKeyEx` que o GF pretendia usar com direitos mínimos. O defeito foi reproduzido no runner com token reduzido em HKLM32/64: chamada antiga recusada, nova consulta permitida. Usa HKEY predefinido emprestado, sem mudar ACLs ou obter privilégios. Isso comprova o defeito no código; não determina sozinho todas as causas do erro físico da issue #48.
2. O fingerprint devolvia apenas false, perdendo etapa/erro. Agora mantém abertura de pai/alvo, verificação de link, enumeração de valores/subchaves, leitura de valor e abertura de filho. Contexto e plano guardam observações somente para apresentar cobertura; essas observações não autorizam exclusão. Falhas não entram na seleção.
3. A correção per-user da #49 não abrangia a inferência genérica `registered-install`, que ainda sondava HKLM. Essa promoção foi removida: máquina exige política independente revisada. Não foi inventada política HKLM para aumentar estatística.
4. Identidades locais de atualização e desinstalação divergiam em nomes com versão/arquitetura; símbolos como `+` podiam desaparecer na normalização consultiva. O reconhecimento executável passa por identidade comum, preservando canal/produto e conferindo arquitetura contra o PE. O mesmo comparador é usado no pós-filtro SQL do índice, na instalação e no manifesto. A consulta SQLite descartável e os nomes ARP decorados no catálogo público autenticado exercitam esse elo; o reconhecimento local isolado não é tomado como prova do percurso inteiro. MSI/WiX sem InstallLocation pode usar DisplayIcon **executável real dentro de base permitida**, com produto/versão/PE e unicidade; cache Windows Installer continua recusado.
5. NSIS genérico procurava o produto apenas abaixo da pasta do desinstalador. Agora considera a instalação registrada e dedicada, mesmo com desinstalador em subpasta. Fornecedor, formato, versão, PE, arquitetura, assinaturas iguais e ausência de ambiguidade continuam obrigatórios. Inferência silenciosa recusa pasta compartilhada no inventário. Outra entrada sem InstallLocation ainda pode comprovar compartilhamento por DisplayIcon ou pelo desinstalador registrado; a mesma barreira protege atualização, desinstalação inferida e limpeza residual. Metadados do MSI também usam a identidade comum, preservando símbolos de produto em vez da normalização consultiva.
6. Inno concedia flags a partir do metadado/log do motor sem comprovar o produto. Mantém o reconhecimento de formato, mas a autorização genérica silenciosa exige a mesma prova de produto/assinaturas. Sem prova, o comando registrado continua interativo. QuietUninstallString registrado e validado mantém seu contrato.
7. A CI encontrou download VLC recusado como endereço privado: o filtro excluía todo `169/8`. Somente `169.254/16` é link-local. A faixa pública do espelho deixou de ser indevidamente recusada; redirecionamentos, DNS, HTTPS, hash e assinatura continuam exigidos.

Fontes primárias: [.NET 10 RegistryKey](https://github.com/dotnet/runtime/blob/v10.0.13/src/libraries/Microsoft.Win32.Registry/src/Microsoft/Win32/RegistryKey.cs), [RegOpenKeyEx](https://learn.microsoft.com/windows/win32/api/winreg/nf-winreg-regopenkeyexw), [direitos do Registro](https://learn.microsoft.com/windows/win32/sysinfo/registry-key-security-and-access-rights), [RFC 3927](https://www.rfc-editor.org/rfc/rfc3927), [Inno](https://jrsoftware.org/ishelp/topic_uninstcmdline.htm) e [NSIS](https://nsis.sourceforge.io/Docs/Chapter3.html). Nenhum fonte de terceiro foi incorporado.

## Capacidades e interface

`ApplicationCapabilities` separa somente leitura, disponível sem autorização, silenciosa elegível, payload verificado, interativa, política residual disponível, seleção autorizável, bloqueada e inconclusiva. O modelo é descritivo e não gera permits. Os motores existentes continuam únicos responsáveis por autorizar execução ou exclusão.

O inventário mostra a capacidade e motivo na dica do aplicativo. A fotografia expira para ações após dois minutos; conflito de fonte invalida a indicação anterior. O atualizador MSIX específico do Terminal mantém sua capacidade separada, sem autorizar desinstalação/limpeza genérica da Store. Consulta em segundo plano, cancelável entre aplicativos e limitada por tempo; esgotar orçamento deixa capacidade inconclusiva. Diagnóstico recebe somente totais, denominadores e códigos fixos, sem nomes/caminhos/valores. Atualização elegível ainda precisa de download, SHA, assinatura, identidade, instalação existente e confirmação posterior. O progresso já existente continua separando essas etapas; somente versão confirmada recebe sucesso.

Resumo residual e confirmação mostram escopo previsto, pastas com identidade anterior, HKCU/HKLM, antes/depois, lidos/ausentes/preservados/inacessíveis e exclusões propostas. Caminhos e chaves específicos ficam disponíveis somente na interface local, inclusive nos motivos de preservação. O evento de diagnóstico não inclui esses campos; a regressão verifica a separação. Fora do escopo não foi verificado. Mantém confirmação SIM/NÃO, revalidação, handles e transações. Diretórios conhecidos de perfis, Local Storage, Session Storage, bancos e cookies passam a ser preservados mesmo contendo extensões antes elegíveis. A fixture comprova exclusão do arquivo autorizado ao lado desses diretórios e preservação dos dois armazenamentos. Barreira pessoal, credenciais identificadas, jogos/saves, drivers/Windows/Prefetch/MRU e proprietários incertos continuam preservados.

## Matriz antes/depois

A execução Windows de `python scripts/measure_work14.py` compara probes idênticos contra a base imutável e a candidata. A comparação intermediária [CI 40154367790](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40154367790) já passou; [JSON completo](benchmarks/WORK14-comparacao.json), SHA-256 do ZIP `95c3754de8f6a6a1530ecb904a5a11fd31aaf7c309582df007a4065f50e6ffaa`, conferido fora do runner. A rodada final repetirá a comparação após o fechamento da coerência do índice e das capacidades. A amostra cobre configurações sintéticas de instalação, não o inventário dos PCs e não um número universal de programas.

| Medida e denominador | Antes #49 | Depois intermediário |
| --- | ---: | ---: |
| Entradas/configurações da amostra de atualização | 10/10 | 10/10 |
| Identidade local reconhecida (inclui falso positivo anterior) | 4/10 | 6/10 |
| Atualização silenciosa elegível entre configurações legítimas | 3/6 | 6/6 |
| Atualização: controles negativos recusados | 3/4 | 4/4 |
| Candidatos de atualização sem autorização silenciosa, apenas consulta | 6/10 | 4/10 |
| Desinstalação silenciosa elegível entre configurações legítimas | 3/4 | 4/4 |
| Desinstalação: controles negativos recusados | 0/4 | 4/4 |
| Total com flags silenciosas na amostra de desinstalação, incluindo aceitações indevidas anteriores | 7/8 | 4/8 |
| Políticas residuais explícitas / identidades do catálogo | 7/27 | 7/27 |
| Seleções residuais autorizadas apenas por catálogo | 0/27 | 0/27 |
| Abertura HKLM32/64 inconclusiva por escrita incidental, token reduzido (CI 40153605447) | 2/2 | 0/2 |

Ganhos legítimos: MSI/WiX por ícone executável corroborado, decoração ARP no NSIS/Inno e desinstalador NSIS em subpasta. A queda de 7 para 4 modos silenciosos é desejada: quatro aceitações sem prova suficiente foram retiradas e um caso legítimo foi habilitado. Não se usa o total bruto como indicador de progresso.

- Atualização: 10 configurações, seis legítimas e quatro negativas. Mede reconhecimento e elegibilidade **antes** da validação do payload; nenhum instalador é executado.
- Desinstalação: oito configurações, quatro legítimas e quatro negativas. O mesmo oráculo de assinante simulado é inserido somente nas duas cópias descartáveis. Prova decisão/escopo; não comprova Authenticode real nem compatibilidade física do fornecedor.
- Políticas residuais: denominador é o catálogo de identidades; política disponível não equivale a seleção autorizada. Não será alegado incremento de catálogo se a contagem não mudar.
- Operações consultivas e inconclusivas da máquina: `CAPACIDADES.RESUMO/MOTIVO`, três operações por entrada. Não há nova coleta física; dados posteriores dos PCs permanecem pendentes. Os números históricos 171/102 não são uma linha de base comparável.

## Validação

Compilações cruzadas locais de produto, atualização e resíduos. A primeira CI 40153605447 passou nas regressões Windows e política, incluindo reprodução real do defeito de abertura e negações em pai/alvo/filho. Falhou no download VLC pela faixa pública, causa corrigida nesta candidata. Uma CI parcialmente aprovada não é aprovação do pacote. A rodada completa da .60 e matriz comparativa permanecem em andamento.

Assinaturas simuladas pertencem somente a fixtures, sem instalação de certificados. Downloads reais e WinVerifyTrust têm job próprio obrigatório. Nenhum instalador de terceiros é executado. Alterar/restaurar ACL ocorre somente nas fixtures GUID descartáveis do runner. Código e hooks de teste não integram o produto distribuído.

## Roadmap e homologação

**27/42; U8/R7/R4 abertos.** PC FORTE .59 mantém duas atualizações `Confirmed` e duas desinstalações `RegistrationAbsent`. Resíduos ficaram inconclusivos, sem limpeza homologada. PC FRACO, AVG/SmartScreen, UAC humano, desempenho real, boot e novas regras HKLM continuam pendentes. Issue #48 permanece aberta para conferir a nova evidência física; não atribuir sua causa exclusivamente ao antivírus ou ACL sem leitura.

## Roteiro mínimo dos dois PCs

1. Usar somente o pacote .60 identificado por sua CI aprovada e BUILD-INFO, sem misturar arquivos da .59. Conferir SHA e versão. Manter antivírus ativo.
2. **PC FORTE, somente leitura:** Atualizar e verificar; conferir todos os aplicativos, capacidade/motivo de dois candidatos conhecidos e de um bloqueado. Guardar totais por capacidade e inventário completo/parcial. Nenhuma atualização/reinstalação é necessária para essa etapa.
3. **PC FORTE, quando houver uma operação já desejada:** escolher um aplicativo dispensável e autorizar individualmente. Atualização: etapas e versão posterior; desinstalação: progresso, ausência estável, cobertura residual e SIM/NÃO se houver alvos seguros. Anotar HKCU/HKLM, etapa e erro se uma leitura falhar. Acesso negado permanece inconclusivo; não insistir nem mudar ACL.
4. **PC FRACO, mesmo pacote:** consulta somente leitura, 1440×900/escala real, dicas, navegação/cancelamento, bandeja e resumo sem cortes. Registrar denominadores e tempos separadamente da rede. Operações mutáveis exigem alvo específico escolhido pelo usuário, em rodada separada.
5. Não provocar reboot; observar recuperação somente leitura apenas quando houver pendência técnica real. Enviar diagnóstico pelo fluxo opt-in do GF, sem publicar logs brutos ou caminhos pessoais no GitHub.
