# Lote 3 — segurança, desempenho e investigação AVG

## Base conferida antes de editar

25/10/2026: main `410d65e23546976db456337c71e4705f10217d36`;
PR #40 aberta em Draft, `ef13aef49083f357f6a14f4de46f8bc976dbd830`.
Base desta missão: Lote 2 `6a020c86ba5024b780334217bb7f38b18b23bb34`,
[CI aprovada 40219971723](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40219971723).
Nova branch `work-lote3-seguranca-desempenho`, [PR #56 em Draft](https://github.com/walissonminel-1991/gerenciador-final/pull/56).
As branches WORK16/17 foram examinadas e continuam independentes. Sem merge,
release estável ou acesso a PCs pessoais.

## Achados reproduzidos e correções

| Área | Antes | Implementação / evidência |
|---|---|---|
| Atualização | Erro Win32 1223 na conferência posterior parecia UAC recusado | Mantém resultado indeterminado após retorno; autorização consumida não repete instalação |
| Desinstalação | Erros 5/1223 posteriores pareciam falha de início/cancelamento | Mantém Pending; lote não avança sobre resultado desconhecido nem autoriza limpeza |
| Concorrência | Dispose podia soltar a proteção enquanto o preparador entrava em Process.Start | Posse transferida atomicamente para o único lançamento; Dispose concorrente não libera o recurso transferido |
| Download | Cada chamada alocava novo vetor de 128 KiB | ArrayPool com devolução em finally e limpeza do vetor; tamanho, EOF e cancelamento preservados |
| Registro visual | Fila e TextBox cresciam sem teto obrigatório | Prévia em memória limitada a 65.536 caracteres, preservando a cauda e indicando omissão; relatórios estáticos completos |
| Pacote | DLL/EXE extra podia passar se fosse incluído no SHA256SUMS | Contrato de dependências e arquivos declarados; biblioteca/fixture desconhecida não ganha autorização pelo hash |
| Compatibilidade do pacote | Nomes de dispositivos Windows e runtimes divergentes não eram recusados por estes controles | Recusa nomes reservados, bibliotecas inesperadas, target divergente, binário declarado ausente e versões de runtime inconsistentes |
| Antivírus | Falha de início genérica | Erros Windows 225/226 descritos como bloqueio antivírus; acesso negado/compartilhamento não atribuído ao AVG |

As correções de estado já estavam isoladas na PR #47 e presentes na frente
WORK17; faltavam nesta base de lotes. A adaptação preserva essa evidência
anterior, sem reivindicar descoberta inédita nem integrar toda a outra frente.

A revisão do despachador revelou os modos de layout `--drivers-readonly` e
`--lote3` dentro do bloco `--work9`: isoladamente, as chamadas executavam a
suíte geral e não esses cenários. O encaminhamento foi corrigido e os dois
passos da CI agora exigem a marca final do cenário específico, além do código
de saída. A execução anterior não é evidência desses dois cenários; eles
precisam passar novamente com o despachador corrigido.

O novo SingleLaunchResource protege o intervalo até Process.Start retornar,
sem impedir que o fornecedor remova seus próprios arquivos depois. Não é
autorização de comando: identidade, PE, política de assinatura e advertências, escopo, consentimento,
inventário e proteção de arquivos continuam exigidos.
Os testes cobrem 32 tentativas concorrentes de atualização, cem disputas de
Claim/Dispose e posse única, além de falha posterior e tentativa já consumida.
Isso não certifica serialização universal entre usuários/sessões/processos.

O limite da prévia atua apenas no painel de mensagens. O arquivo de diagnóstico
mantém sua política e limites próprios. Nenhum relatório de investigação
estático é cortado. Esta alteração não encerra processos externos nem apaga
cache do Windows. Contagem de caracteres omitidos não representa RAM física
liberada nem limpeza da memória de outros programas.

## Escopo da auditoria

- Atualização: `VerifiedUpdaters`, `InstallerFileGuard` e
  `UpdateInstallationContract` mantêm origem HTTPS restrita, limites de tamanho,
  hash e política de editor, arquivo protegido até o lançamento e conferência
  de identidade/escopo/versão. A exceção existente do 7-Zip continua restrita
  ao hash fixado. Nome semelhante não autoriza instalação nem limpeza.
- Desinstalação: `UninstallCommand`, `RegisteredUninstaller` e `UninstallBatch`
  preservam gramática restrita, recusa de hosts/interpretadores, identidade do
  registro, inventário completo e revalidação antes de lançar. Advertências
  de assinatura continuam exigindo revisão; adulteração/revogação não ganha
  exceção. Pendência não autoriza a limpeza residual integrada.
- Concorrência: o bloqueio da interface e as tentativas consumidas continuam;
  a correção de posse elimina uma disputa no preparador. Os testes não
  certificam exclusão mútua global entre todas as instâncias do Windows.
- Memória: `MemoryRamDashboard` e `SelectiveMemoryTrim` conservam consentimento
  para um processo específico, PID/início/caminho e nova identidade no mesmo
  handle antes da API. A missão não executou esse ajuste em programas reais.
  Redução de working set e descarte de buffers não provam ganho duradouro.
- Restauração da bandeja: `WindowChrome.RestoreFromTray` evita recriar o handle,
  restaura a janela, ajusta amostragem e pode retomar uma confirmação pendente.
  A consulta de RAM global não enumera PIDs nesse caminho. Isso descreve o
  código revisado; não identifica a regra comportamental do AVG.
- Distribuição: manifesto `asInvoker`, importação nativa restrita a System32,
  publicação sem extração single-file e exclusão dos projetos de testes
  permanecem. O verificador reforçado complementa esses controles.

## Medição reproduzível

Microbenchmark local com SDK 10.0.402: 64 respostas sintéticas de 512 KiB,
destino Stream.Null, vetor de origem reutilizado, uma rodada de aquecimento
antes de medir GC.GetTotalAllocatedBytes. São alocações do processo naquele
intervalo, não working set, RAM total do GF ou ganho prometido em PCs reais.

| Estado | Bytes alocados nas 64 rodadas |
|---|---:|
| Base, vetor novo por chamada | 8.426.608 |
| Vetor compartilhado, após aquecimento | 35.840 |
| Windows 2022, CI `40229759024`, mesmo teste | 35.840 |

O teste mantém teto de alocação com margem e exercita corpo maior que o
Content-Length, cancelamento e recuperação após exceção. A prévia recebe
6.000 mensagens antes da primeira drenagem e 3.000 adicionais; confere teto,
mensagem mais recente e relatório estático de 131.072 caracteres intacto.
Na CI Windows, a prévia reteve exatamente 65.536 caracteres depois das
6.000 mensagens iniciais; o teste completo terminou com sua marca PASS.

Comandos dos cenários, nos runners Windows descartáveis desta PR:

```powershell
dotnet run --project TestesAtualizador/TestesAtualizador.csproj -c Release -- --lote3
dotnet run --project TestesResiduos/TestesResiduos.csproj -c Release -- --lote3
dotnet run --project TestesLayout/TestesLayout.csproj -c Release -- --lote3
dotnet run --project TestesLayout/TestesLayout.csproj -c Release -- --drivers-readonly
python -B -m unittest discover -s scripts -p 'test_p*.py' -v
```

## Empacotamento e preparação do ambiente

O contrato aceita o produto, os runtime packs .NET/WindowsDesktop/Windows SDK
e YamlDotNet 18.1.0 já usados pelo projeto. Referência nova exige revisão
explícita do contrato. Recursos localizados do WinForms seguem aceitos;
DLLs/EXEs de testes ou sobras não são autorizados por estarem com hash correto.
Isso verifica composição e consistência; não substitui autenticidade do ZIP,
revisão de código ou scanner.

Controle positivo independente, sem executar o produto: ZIP do Lote 2,
SHA-256 `d66ec2e925bf678d133a20fd8ed992e88ae1fd468d2c3df5d28433c6fd164b43`,
282 arquivos e 281 hashes, 151.889.847 bytes expandidos, aprovado pelo
verificador reforçado. A assinatura do GF continua ausente, declarada no pacote.

Primeira CI do Lote 3: [40228710682](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40228710682),
fonte `3c08386cd69f61d62b4e4dc05e5f97f9cfae70e2`. Falhou **antes de compilar**:
setup-dotnet resolveu canais para runtime 10.0.14 / SDK 10.0.403 e recebeu
HTTP 409. Os testes não foram declarados aprovados nessa execução. A correção
fixa setup-dotnet em 10.0.402, o SDK já declarado e validado, e global.json
exige versão exata. Não suprime nenhum teste ou verificação de segurança.
Atualizações desse SDK devem ocorrer por mudança explícita e CI.
O action ainda tenta instalar o runtime LTS como etapa auxiliar e emite
aviso HTTP 409; a seleção exata do SDK passa e as regressões executam no
runtime 10.0.13. O aviso não foi ocultado nem tratado como aprovação de teste.

Na [CI 40229759024](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40229759024),
fonte `ff6aa1d78700827677041e4fb4ca5dc980080f71`, as regressões, políticas e
runners de drivers passaram: 318 atualizadores, 655 resíduos, 27 mutações
rejeitadas, 39 testes Python, 10 testes Node e os cenários de layout específicos.
O job externo de downloads falhou primeiro com HTTP 404 no VLC 3.0.24 e,
na tentativa 2, com redirecionamento não autorizado. O mesmo manifesto havia
passado minutos antes na execução cancelada 40229436947. A CI completa não
foi considerada aprovada e o empacotamento permaneceu bloqueado. O teste
passa a registrar protocolo, host e status sem query/credenciais; HTTPS,
validação de destino público, limite de redirects, hash, assinatura e motor
continuam obrigatórios. Nenhum fallback para HTTP foi adicionado.

Evidências finais por HEAD, logs, tentativas e artefatos na
[PR #56](https://github.com/walissonminel-1991/gerenciador-final/pull/56).

## AVG: investigação técnica e limite de conclusão

O relato anterior era IDP.Generic comportamental ao restaurar da bandeja.
Essa coincidência não prova causa nem falso positivo. Foram revisados o
plano AVG existente, restauração da janela, início de processos, trabalhador
elevado, importação de DLLs do Windows, validação de instaladores e pipeline.
A matriz WinForms existente exercita minimizar/restaurar em dados de teste.

Nos fluxos auditados, não foi encontrado nem introduzido código para desabilitar AV, criar exceção,
restaurar quarentena, alterar ACL para contornar bloqueio, ofuscar ou renomear
payload para fugir da detecção. A resolução de DLLs continua System32.

Registrar-Evidencia-Antivirus.ps1 gera JSON separado da distribuição contendo
origem, versão, tamanho, SHA-256, status de assinatura e signatário dos dois
binários do produto no runner. Declara scan_performed=false,
sample_submitted=false e avg_validation=pending. Sem executar o produto,
consultar PCs pessoais, enviar amostra a terceiro ou divulgar caminhos locais.
Erro 225/226 identifica relato antivírus do Windows, não o fabricante. Erro
5/32 e WSC não provam bloqueio AVG; assinatura ausente não é prova de malware.

As fontes oficiais AVG foram verificadas em 25/10/2026. O fornecedor descreve
revisão das amostras e possibilidade de aprovação por arquivo ou assinatura;
a decisão permanece com ele. Diretrizes favorecem identificação, transparência,
consentimento e assinatura. A auditoria não atesta cumprimento integral nem
aprovação AVG. Nenhuma amostra foi enviada por esta missão.

**P5 e P6 continuam abertos. Roadmap 27/42.** Não há diagnóstico conclusivo
da detecção original, varredura AVG desta build, resposta do fornecedor ou
homologação de estabilidade com AVG ativo nos PCs. Testes sintéticos e CI
não substituem isso. U8, R7, R4 e outros checkpoints físicos também permanecem
abertos. Nenhum teste nos PCs é exigido para revisar esta implementação.

Fontes primárias:

- [Erros Windows 0–499](https://learn.microsoft.com/en-us/windows/win32/debug/system-error-codes--0-499-).
- [Nomes de arquivos Windows](https://learn.microsoft.com/en-us/windows/win32/fileio/naming-a-file).
- [Seleção exata de SDK](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json).
- [AVG Whitelisting FAQ](https://support.avg.com/SupportArticleView?l=en&urlName=avg-threat-lab-file-whitelist-faqs).
- [AVG Clean Guidelines](https://support.avg.com/SupportArticleView?l=en&urlname=avg-threat-lab-clean-guideline).
- [Quarentena AVG](https://support.avg.com/SupportArticleView?l=pt&urlName=Use-AVG-Antivirus-Quarantine).
