# WORK10 — limpeza integrada à desinstalação

Implementação entregue em **v5.13.11.52 TESTE**, com [CI final aprovada](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40021841254). Fonte validada: `c200ab25c0352d741dc505719842657338905c48`. Base inicial: `e7b342934eb2c81add28b6f011c81cc5f4a64e99` (.51). PR #40 permanece Draft; main preservada em `410d65e23546976db456337c71e4705f10217d36`. Homologação física e UAC interativo permanecem pendentes.

## Resultado no produto

O GF registra identidade e locais conhecidos antes da desinstalação, executa o comando autorizado, confere a saída e observa a ausência estável do aplicativo. Somente remoções confirmadas participam da investigação automática em segundo plano. Falha, reinício, retorno não padronizado, inventário parcial e cancelamento preservam os dados.

Uma única confirmação **SIM/NÃO** atende toda a seleção. Mostra totais, resumo de cada aplicativo, alvos e preservações. **SIM** exclui os alvos exclusivos revalidados permanentemente. **NÃO** preserva todos os resíduos e encerra a decisão. Sem alvos elegíveis, não há pergunta. A decisão recusada não é agendada novamente. Na bandeja, a confirmação aguarda restauração; cancelar descarta a pergunta.

Foram removidos os cartões e janelas de investigação, pesquisa, filtros, navegação e limpeza residual independente. As seis ferramentas Windows restantes ocupam até três colunas, preenchendo a largura. Temporários, caches, Lixeira, limpeza avançada nativa e discos continuam disponíveis.

## Um executor e os mesmos mecanismos administrativos

`IntegratedResidualPolicy`, `IntegratedResidualCleanup` e `IntegratedResidualBatch` consolidam políticas e execução. Reutilizam `InstallerFileGuard`, `RegistryTreeFingerprint`, catálogo de identidades, regras verificadas, inventário e diagnóstico existentes. Os pilotos antigos SmallApp/SevenZip continuam nas regressões, excluídos do assembly distribuído; a interface verifica essa ausência.

Arquivos usam handles estáveis, identidade do objeto, número de hardlinks, SHA-256 e identidades de toda a cadeia de diretórios. Diretórios são removidos por handle e somente vazios. Cada exclusão e a conferência final distinguem removido, já ausente, preservado, falha, permissão e cancelamento. Não há envio à Lixeira nem remoção recursiva de diretórios.

O Registro confere hive, visão, chave exclusiva, hierarquia, tipos, contagens e conteúdo. Aberturas por segmento não seguem links. A exclusão usa transação e reserva de escrita **antes** do fingerprint, inclusive nas subchaves. O marcador privado da reserva é escrito e removido dentro da transação, sem aparecer fora dela. Alterações externas conflitantes abortam a exclusão. Sem suporte/permissão para a transação, o alvo fica preservado.

Quatro notificações de Registro invalidam o inventário durante a execução. Há consulta inicial/final, nova consulta quando ocorre mudança e revalidação periódica em operações demoradas, evitando uma consulta completa por arquivo.

O **mesmo** `RunElevatedAsync` / `WorkerHost` ganhou a tarefa fixa `uninstall-residual-clean`. Planos trafegam apenas em memória, por pipe duplex com tamanho limitado. PID do cliente, token, executável e conta do servidor são conferidos. O worker reconstrói o escopo e revalida identidade/conteúdo. Uma elevação por lote recebe somente os alvos confirmados e recusados por permissão. Recusar UAC preserva os alvos. O produto não altera ACLs, assume propriedade ou pede reabertura manual como administrador nesse fluxo.

## Registro e Diagnóstico

O diagnóstico local existente recebe identidade técnica minimizada, etapa, estado, contagens, duração, cancelamento, motivo de bloqueio, código nativo quando disponível, autorização e conferência final. Eventos de Registro distinguem `hkcu32`, `hkcu64`, `hklm32` e `hklm64`, inclusive no retorno administrativo.

Caminhos pessoais, nomes brutos dos aplicativos e conteúdo dos arquivos não entram nesses eventos. Não foram criados novos logs por operação nem arquivos de planos. Falha de gravação solicita cancelamento dos próximos alvos; a interface informa diagnóstico indisponível e operação interrompida. O trabalho e a gravação dos resultados administrativos ficam fora da thread visual.

## Evidências Windows da fonte final

| Verificação | Resultado |
| --- | --- |
| Compilação com analisadores | Aprovada, sem avisos |
| Desinstalação e resíduos | **576/576**, incluindo **47 controles WORK10** |
| Mutações adversariais | **14/14 rejeitadas**, quatro novas de limpeza |
| Atualizadores | 148/148 |
| Fontes e identidades | 159/159; catálogo 17/17 |
| MSIX / encerramento | 47/47 e 30/30 |
| Interface principal | 132 combinações, 22 cenários, bandeja/foco/fechamento |
| Confirmação integrada | 44 combinações de tamanho/escala simulada |
| Política / privacidade | 29 Python e 10 Node |

Os 47 controles WORK10 pertencem aos 576 testes; as contagens não são somadas novamente. Fontes/downloads externos opcionais ficaram skipped nesta rodada. O benchmark de classificação existente também passou.

### Exclusão efetiva comprovada

- Arquivo `.cfg` sintético e diretório vazio realmente desapareceram após SIM; NÃO manteve arquivo, diretório e conteúdo de Registro.
- Chaves sintéticas foram removidas em HKCU/HKLM e 32/64 bits. A autorização apenas de HKLM32 preservou a chave de mesmo caminho em HKLM64, equivalente ao problema do Notepad++.
- Um **processo PE real** de desinstalação removeu sua própria entrada ARP descartável, saiu com zero e passou pela conferência do lote. Depois de SIM, todos os arquivos sintéticos daquele escopo desapareceram.
- Um **worker administrativo em processo separado**, autenticado por PID/token/executável/conta, removeu arquivos reais dos alvos sintéticos e retornou a conferência.
- Permissão negada real preservou a chave. Recusa na fronteira UAC registrou 1223. A fronteira autorizada repetiu somente a chave negada e consolidou a exclusão real. A alteração/restauração de ACL pertence exclusivamente à fixture descartável; o executor do produto não realiza essa ação.
- Reinstalação durante a execução interrompeu os próximos alvos. Arquivo com tamanho/data iguais e conteúdo diferente foi preservado. Troca do diretório com o mesmo arquivo movido também foi bloqueada.
- Mudanças concorrentes no Registro, tanto na raiz como em subchave, preservaram a árvore alterada. Links de diretório, hardlinks e links intermediários no Registro foram preservados.
- Dados pessoais, XML desconhecido, backups, biblioteca de jogos, cancelamento após um arquivo, falha parcial de compartilhamento e indisponibilidade do diagnóstico foram exercitados.

As fixtures são identificadas por GUID e destruídas no runner descartável. Nenhum aplicativo nem dado real dos computadores pessoais foi instalado, desinstalado ou excluído.

### Testes que encontraram defeitos

| CI | Achado e correção |
| --- | --- |
| [40017788533](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40017788533) | `InvalidDataException` da chave alterada escapava do filtro. Passou a produzir preservação/resultado parcial. |
| [40018590905](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40018590905) | Transação aberta apenas para leitura aceitava escrita externa anterior à primeira escrita. A reserva de escrita anterior ao hash corrigiu a corrida. |
| [40019389757](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40019389757) | A fixture não restaurava a ACL porque o objeto de segurança original não tinha alterações marcadas. A restauração explícita do descritor corrigiu o teste positivo. |
| [40019876408](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40019876408) | Backend e nova confirmação aprovados; teste histórico ainda exigia oito cartões. Atualizado para as seis ferramentas Windows e distribuição em três colunas. |
| [40020884285](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40020884285) | Matriz completa aprovada; checkpoint anterior ao fechamento do diagnóstico. |
| [40021841254](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40021841254) | Fonte final aprovada, incluindo cancelamento por diagnóstico indisponível e identificação de hive/visão. |

As quatro novas mutações retiram consentimento, SHA-256, fingerprint do Registro ou identidade dos pais. Cada retirada provoca a falha esperada em uma cópia isolada. Erro de compilação não é aceito como mutação rejeitada.

## Pacote conferido independentemente

[**Baixar candidata .52 TESTE x64**](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40021841254/artifacts/12531531070) — artefato `12531531070`, conservado até **30/10/2026, 17:50 (Fortaleza)**. Extraia e mantenha a pasta completa.

- Nome: `GerenciadorFinal-v5.13.11.52-c200ab25-TESTE-win-x64`.
- ZIP: **58.982.852 bytes**; extraído: **151.344.534 bytes**.
- **281 arquivos / 280 hashes** verificados fora do runner; digest do ZIP igual ao informado pelo GitHub.
- EXE e DLL: versão **5.13.11.52**, recursos de versão conferidos sem execução.
- Apphost AMD64; .NET/WindowsDesktop **10.0.13 self-contained**, licenças e links do pacote conferidos. Manifesto `asInvoker` aprovado no Windows; usuário final não precisa instalar SDK.
- Fonte `c200ab25c0352d741dc505719842657338905c48`; merge de compilação `e6a39c3002d4a0d8f419e00f57eedb4eac00c51c`; base `410d65e23546976db456337c71e4705f10217d36`.
- SHA-256: `427a9c226b79b002ee048967c701a60e74833f1574edf15b9c58c4648344151c`.

As 56 capturas estão no [artefato de layout](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40021841254/artifacts/12530343871). Foram inspecionados a confirmação, o layout 1440×900 e o estado de rolagem em 1280×720; os PNGs conferidos correspondem exatamente à fonte final. A moldura nativa clara no Server 2022 continua uma limitação visual do ambiente.

## Desempenho e limites

A amostra de **120 arquivos vazios reais** levou **71 ms**, com **duas chamadas ao inventário sintético**, no runner Windows. O custo do inventário completo dos PCs e de gravações reais do diagnóstico não está incluído nessa amostra. Não houve nova medição física de RAM, CPU, abertura ou disco nos dois computadores.

A cobertura é progressiva. Receitas verificadas abrangem Notepad++, SumatraPDF, 7-Zip, OBS, VLC, Audacity e foobar2000. Para outros aplicativos, a pasta de instalação precisa ser dedicada, estar em base conhecida e ser corroborada pelo comando registrado e pela identidade do produto. Locais personalizados, dados pessoais, objetos compartilhados e formatos desconhecidos podem permanecer preservados. Não há seleção arbitrária de chaves nem pesquisa de todo o disco.

Limites por aplicativo: 5.000 entradas, 256 MiB, profundidade 16 e orçamento de 12 segundos na consulta (conferido entre leituras); fingerprint do Registro limitado a 3.000 entradas/4 MiB. Frames administrativos têm limite de 4 MiB. Limites, falhas e indisponibilidade de transações preservam os alvos e entram no diagnóstico.

**UAC com credenciais de outra conta é recusado**: o perfil e o inventário da sessão original não podem ser presumidos na outra conta. O runner já possui direitos administrativos; os testes da decisão autorizada/recusada usam a fronteira de autorização. O clique humano no UAC e a experiência nos PCs ainda precisam de homologação. AVG/SmartScreen, assinatura e distribuição estável permanecem pendentes.

## Commits e roadmap

| Commit | Entrega |
| --- | --- |
| `e59bc499` | Política, motor e extensão do worker administrativo |
| `e22031b7` | Integração à desinstalação, confirmação e retirada da investigação manual |
| `ad91b40f` | Contenção de falhas e diagnóstico de bloqueios administrativos |
| `66c1b0ac` | Reserva transacional, links e mutações adversariais |
| `91e93637` | Fixture de permissões e contagens/duração |
| `f9ff3265` | Executor único distribuído, seis cartões e confirmação com resumo inicial |
| `c200ab25` | Interrupção por falha de diagnóstico e identificação das visões do Registro |

R4 avançou na implementação integrada, exclusão efetiva, confirmação, permissões e regressões nativas. Continua aberto para homologação física/cobertura. R7 e U8 também permanecem abertos para os testes reais previstos nos computadores. O roadmap conserva **27/42 (64,3%)**. [Próximo roteiro objetivo](TESTE-CONSOLIDADO.md).

Referências de API: [RegDeleteKeyTransacted](https://learn.microsoft.com/en-us/windows/desktop/api/winreg/nf-winreg-regdeletekeytransacteda), [RegOpenKeyEx](https://learn.microsoft.com/pt-br/windows/win32/api/winreg/nf-winreg-regopenkeyexw). Os testes acima demonstram o comportamento usado pelo produto no ambiente descartável.
