# Autoatualizador GF — Etapa 3: gates isolados e limites comprovados

28/10/2026, America/Fortaleza. **Etapa 3 concluída no escopo experimental,
com gates explicitamente abertos. Integração no produto e Etapa 4 NÃO INICIADAS.**
PR #65 aberta/Draft; entrada `41848534602ef2f9bc84fe3e4eb26de3937f48e2`.
Checkpoint, protocolo e evidências da Etapa 2 lidos. Contrato `gf-self-update/1`,
fontes e evidências anteriores preservados. Nenhum teste físico repetido.

Código final testado: **`6e17b7268fb1182669e9a2646b34cfb52071dd00`**.
O commit de fechamento posterior só registra documentação/resultados.

## Resultado e rastreabilidade

| Execução | Resultado | Escopo comprovado |
| --- | --- | --- |
| [Windows 40764777624](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40764777624) | **47/47**, número 3/tentativa 1, success; job 123085854771 | windows-2022, SDK 10.0.402, runner .NET 10.0.14; sonda self-contained .NET/WindowsDesktop 10.0.13 |
| Linux | **19/19**, SDK 10.0.402, .NET 10.0.13 | Casos comuns de relógio TUF, frames, PE limitado e imagens de persistência; sem alegação NTFS/WinForms |
| Isolamento | **142 itens Compile do GF; zero do protótipo**, nos dois ambientes | Produto apenas avaliado, nunca compilado/executado; saídas fora do repositório |
| Entrega/canal | **Zero artefatos nas três runs** | Sem ZIP/EXE/DLL publicados, Release, merge, repositório privado ou permissões novas |

Há **47 casos distintos**; os 19 Linux também estão no Windows. Não somar
plataformas, subcasos, rodadas de correção ou testes históricos ao total/roadmap.
Builds finais sem avisos/erros. Não foram reexecutadas as suítes físicas, a
compilação de produto nem as 61/66 verificações completas das etapas anteriores.

[Windows por teste](evidencias/autoatualizador-etapa3-windows.json) ·
[Linux](evidencias/autoatualizador-etapa3-linux.json) ·
[Rodadas anteriores preservadas](evidencias/autoatualizador-etapa3-rodadas.json) ·
[Recibo](evidencias/autoatualizador-etapa3.json) ·
[Fontes, execução e decisões](../prototipos/autoatualizador-seguro/stage3/README.md).

## Gates: resultado separado da autorização operacional

| Gate | Prova obtida | Ainda não aprovado |
| --- | --- | --- |
| Relógio TUF | Fonte upstream fixada, 20 arquivos/hash conferidos; assembly experimental com sete consultas usando relógio imutável por ciclo e igualdade tratada como expiração. Raiz/timestamp/snapshot/targets, limites e assinatura inválida exercitados; 10 leituras do mesmo relógio no caso positivo | Pacote Tuf 1.1.1 original continua intacto. Adoção/manutenção/revisão da mudança em produto ou upstream e conformidade TUF integral não foram aprovadas |
| Durabilidade | 16 imagens de prefixo de diário/seletor: 6 seleções seguras e 10 recusas. Flush nativo de arquivo conferido; flush de diretório do fixture aceito pelo Windows sem abrir volume ou alterar privilégios | Nenhum corte de energia, disco realmente cheio ou garantia de cache/hardware. Perda integral do sufixo/seletor restaura piso antigo no modelo; esse gate permanece aberto |
| Extração ZIP | Texto autenticado, CRC/conjunto/hashes antes da escrita, CreateNew, diretórios exclusivos, handles NTFS presos, revalidação por hash, recusa de junction/corrida/preexistência/paths, reserva e cancelamento | ZIP de até 64 KiB/expansão 1 MiB, limites históricos de fixture. Não foi extraído ZIP GF real, ZIP máximo do contrato ou bundle completo de produção |
| PE/runtime/licenças | Sonda real com 274 arquivos/123.062.971 bytes: Win32/AMD64, DLLs gerenciadas admissíveis, manifesto asInvoker/uiAccess=false, versão 0.0.0.1, runtimes 10.0.13, grafo e conjunto exato, licenças reais e metadado MIT do WindowsDesktop | Receita exclusiva da sonda, parser PE limitado; sem atestar o pacote GF, assinatura Authenticode/WinVerifyTrust, AVG ou segurança de todo formato PE |
| IPC autenticado | Apphosts de teste, CurrentUserOnly/FirstPipeInstance, PID do kernel ligado ao processo retido, SID/sessão/elevação/imagem, pai verificado pelo kernel, nonce/transação/perfil e frames limitados. Alteração de DLL antes de spawn recusa; arquivos críticos presos durante a execução | Outro usuário/sessão não foi criado/testado; não há autenticação operacional GF/helper ou transporte remoto |
| Encerramento/prontidão | Sonda cria HWND e entra no loop WinForms antes de READY. CLOSE/BYE e saída real conferidos. Falhas, duplicata, busy, nonce/tx/PID errado, saída antecipada e recusa de fechar não confirmam prontidão. Timeout real **30,03 s** | Não é saúde funcional/UI do GF, nem fechamento coordenado de todas as suas instâncias. Camadas ensaiadas separadamente, sem pipeline operacional de atualização do GF |
| Elevação | Runner estava elevado; tokens reais foram lidos e o classificador de operação recusou elevação. Canal de fixture exercitado com os dois processos na mesma elevação herdada | Isso não autoriza updater elevado; caminho positivo sem elevação não foi executado neste runner. Nenhum UAC, token/ACL ou privilégio foi alterado |

O resultado de flush de diretório é uma observação de API, não prova de energia.
O contraexemplo de perda de sufixo é mantido como limite, sem reduzir segurança,
reinicializar confiança ou declarar o problema resolvido. As imagens são
simulações de persistência em diretórios descartáveis, sem afetar dados reais.

## Decisões e correções dirigidas

O [patch de relógio](../prototipos/autoatualizador-seguro/stage3/clock-experiment.patch)
é aplicado só à cópia externa da fonte upstream
`c0f5ff9747aa9b00c45a964dca275be4066cd489`, com licença/notices preservados.
Não reimplementa criptografia, não altera os pacotes/lockfiles aprovados e não
é incluído no produto. Repositórios TUF dos testes ficam em memória e URI
.invalid; nenhum exporter de telemetria ou acesso ao canal foi introduzido.

Primeira run **40763350794**, SHA b7038784: o coletor exigia notices separado
ausente no pacote oficial WindowsDesktop 10.0.13. **Suíte não executada** nessa
run. Conferido pacote real: LICENSE e nuspec MIT, repo dotnet/dotnet no commit
4099f83977f4d29604a9aeb971fb0a7bc1d5f699. Corrigida coleta e validação desse
metadado, mantendo LICENSE/NOTICES .NET e LICENSE WindowsDesktop obrigatórios;
nenhum texto de licença/aviso foi inventado. O contrato do produto já exigia
LICENSE WindowsDesktop, não um notices inexistente, e permanece intacto.

Segunda run **40763986158**, SHA f12d3eb3: **30/33**, IPC bloqueado e seus 14
casos não executados. Corrigido reconhecimento de Win32Exception de seletor
ausente no modelo de falha. O SDK real emite nomes `runtimepack.*` no deps e
copia 52 satélites WinForms fora desse grafo; esse perfil foi conferido na
saída de uma compilação isolada da sonda, sem executá-la no Linux. Os nomes e
13 culturas/4 assemblies são fechados e todos os hashes continuam obrigatórios;
nenhuma exceção livre de DLL foi adicionada. DLL extra segue rejeitada.

Terceira run **40764777624**, SHA 6e17b726: **47/47**, incluindo os casos IPC
antes bloqueados. Três runs distintas, tentativa 1 em cada SHA, sem retry
automático; repetições apenas após defeito demonstrado. Falhas anteriores não
foram apagadas ou convertidas em aprovação por equivalência de ambiente.

As únicas execuções adicionais são do runner e da sonda nomeada, produzidos
nos temporários do ensaio. A sonda só pode ser iniciada após inspeção estática
e autenticação de seus arquivos críticos; falha de inspeção bloqueia o grupo
IPC. Limpeza do harness limita eventual término forçado a filhos de teste que
ele próprio criou. Não há force-kill do GF ou de processos terceiros.

## Preservação e ponto de parada

BETA **v5.13.11.66**, fonte `6df95fdd776644cd4b248b5cea61108974ca9ce9`,
run privada 40716548530/tentativa 1/artefato 12828262039 e ZIP SHA-256
`8e11f2dda5b7e2df2f85c61b57cc6a31a9596628523d2be7ad155dfdd5dda7f2`
preservados sem download, recompilação, conversão ou publicação. Repositório
privado, retenção, acessos e workflows de produto não foram alterados.

AVG/proteções, consentimento e dados pessoais preservados; VLC exclusivamente
inventário sem ações/consultas/testes específicos; WinGet COM opcional/consultivo;
drivers de instalação/remoção/reversão desativados; diagnóstico remoto bloqueado,
PILOT_ENABLED=no e GITHUB_EXPORT_ENABLED=no. Sem PC FORTE, Worker/D1/DNS,
infraestrutura, permissões Windows, chave operacional ou bootstrap instalado.
Avaliação física segue parcial e roadmap **27/42**, mesmos 42 IDs/done.

Branch de ensaio mantida para evidência:
`work-autoatualizador-etapa3-validacao-20261028`. Workflow novo só nessa branch,
contents:read, sem uploads/secrets/cron; fechamento documental da PR usa skip ci.
Main, históricos e PRs preservados; PR #65 continua aberta/Draft.

Permanecem gates de energia/armazenamento, adoção/revisão do relógio experimental,
receita e extração GF reais/Authenticode, contextos IPC não executados, ciclo de
vida operacional e cerimônia de bootstrap/custódia. **Nenhuma integração ao GF,
versão nova ou Etapa 4 iniciada. Encerrar após salvar/conferir o checkpoint.**
