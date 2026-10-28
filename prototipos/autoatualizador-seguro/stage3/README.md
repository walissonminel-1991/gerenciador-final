# Etapa 3 — experimentos isolados dos gates pendentes

**Não é uma atualização GF, instalador ou bootstrap operacional.** Só o test
runner e uma sonda inerte com versão 0.0.0.1 são compilados. Nunca executar o
Gerenciador Final ou importar o ZIP privado para estes testes. Contrato externo
`gf-self-update/1` e todos os fontes/evidências das Etapas 0–2 permanecem iguais.

```sh
python run_tests.py --report /temporario/gf-u3.json
```

SDK 10.0.402. Windows descartável executa os casos NTFS/PE/WinForms/IPC; Linux
só executa os casos comuns. Saídas, dependências geradas, fonte experimental e
sonda ficam em temporários externos. C# versionado usa `.cs.txt`. O produto é
apenas avaliado por MSBuild para conferir o isolamento; não é compilado. O
workflow só lê esta branch de ensaio e não faz upload/Release/artefato.

## Relógio TUF: experimento separado do pacote aprovado

Tuf 1.1.1 original e BouncyCastle 2.7.0 continuam fixados. Não existe API para
injetar TimeProvider no pacote original. `prepare_clock.py` obtém a fonte no
commit upstream c0f5ff9747aa9b00c45a964dca275be4066cd489, verifica o conjunto de
20 arquivos e respectivos SHA-256, preserva LICENSE/THIRD-PARTY-NOTICES e aplica
somente o [diff revisável](clock-experiment.patch): sete consultas de relógio e
suas comparações de expiração, mais uma propriedade required nas opções.

O resultado é outra assembly, `GF.Tuf.ClockExperiment`, referenciada por alias
somente neste projeto. Sem alterar parser, assinaturas ou primitivas. Um relógio
imutável por ciclo chega ao cliente e a todos os papéis; igualdade de expires é
expiração. Fixtures assinadas por implementação independente verificam raiz,
timestamp, snapshot, targets, limite exato e assinatura inválida. O pacote
original é exercitado separadamente, sem fingir que recebeu esta correção.

Isso prova viabilidade na cópia experimental, **não uma correção upstream
oficial, conformidade TUF integral ou adoção em produto**. Antes de adotar,
decidir suporte/manutenção/revisão da mudança ou biblioteca apropriada, sem
trocar silenciosamente a dependência aprovada. Nenhuma mensagem/PR foi enviada
ao projeto upstream. Os repositórios TUF usados nos testes são selados em
memória, com URI .invalid e sem exporter de telemetria.

## Durabilidade: evidência e limite explícitos

O ensaio restaura 16 combinações de prefixo do diário/seletor, obtidas do
protocolo real da Etapa 2. Só uma seleção confirmada ou recuperação exata pode
ser inspecionada; imagens inconsistentes param. Outra imagem mostra o limite:
perder/restaurar o sufixo inteiro e o seletor antigo também restaura o piso
antigo. Checksum/encadeamento não resolvem isso sem outra âncora durável.

Windows testa retorno de FlushFileBuffers de arquivo e bytes reabertos. Uma
sondagem de flush de diretório registra sucesso/erro real, sem exigir sucesso
artificial ou habilitar privilégios. Nenhum volume é aberto. Esses resultados
**não demonstram perda de energia, corrupção real do dispositivo ou garantia
de cache/hardware**. O gate de durabilidade contra energia permanece aberto;
não há aplicação operacional que possa usar o experimento como autorização.

## ZIP e inspeção estática

ZIPs de texto autenticados são validados integralmente pelo parser/CRC limitado
da Etapa 1 e só depois escritos via CreateNew. Diretórios são criados de modo
exclusivo; ancestrais/destinos/arquivos são presos pelos handles NTFS da Etapa 2.
Hashes são conferidos no mesmo handle após escrita/flush. Não truncar item que
apareça numa corrida, seguir junction ou apagar staging falho para passar.
Há reserva de espaço, cancelamento, conjunto exato e limites de paths/bytes.
Teste de espaço insuficiente é injetado, não disco real cheio.

Limites menores das fixtures (ZIP 64 KiB/expansão 1 MiB) continuam intactos.
Não se afirma ter extraído um bundle GF real de tamanho máximo. O verificador
aprovado `scripts/verificar_pacote.py` não é alterado ou afrouxado.

A sonda WinForms é compilada self-contained win-x64, runtime fixado 10.0.13,
manifesto asInvoker/uiAccess=false e versão 0.0.0.1, sob outro nome. O runner
copia LICENSE/THIRD-PARTY-NOTICES reais do .NET e LICENSE/nuspec MIT reais do
WindowsDesktop 10.0.13, que não contém notices separado. ID/versão/licença e
commit oficial 4099f83977f4d29604a9aeb971fb0a7bc1d5f699 são conferidos no nuspec;
nenhum aviso é inventado. O SDK pode baixar implicitamente o runtime ASP.NET fixado no
restore; ele não integra o grafo/sonda aceitos. A configuração NuGet mapeia
apenas os pacotes necessários e os três lockfiles são versionados.

A inspeção é estática: conjunto/hashes da receita confiável de teste, PE/AMD64,
CLI AnyCPU admissível em DLL, recursos Win32 de manifesto/versão, runtimeconfig
self-contained, versões exatas, dependências declaradas e licenças. Os dois
pacotes do deps usam o prefixo literal runtimepack. Um conjunto fechado de
52 satélites WinForms (13 culturas x 4 nomes), copiado pelo SDK mas omitido no
deps, também é exigido e autenticado; não existe exceção livre para DLL extra.
Parser PE
com leituras limitadas; sem Assembly.Load, LoadLibrary ou PEReader sobre dados
hostis. Não se declara parser universal/auditoria de todos os formatos PE.
Receita é exclusiva da sonda, **não substitui o contrato de dependências do GF**.
Authenticode/WinVerifyTrust/AVG de um pacote GF não são testados nesta etapa.

## IPC, encerramento e prontidão reais da sonda

Pipes locais com CurrentUserOnly/FirstPipeInstance; SID, sessão, elevação,
imagem e PID vindos do kernel são comparados ao processo retido pelo criador.
O filho confere também o pai pelo snapshot do kernel e o servidor pelo pipe;
nome do pipe, argumento PID ou host dotnet.exe isoladamente não bastam.
Frames limitados a 2 KiB, JSON fechado, transação/nonce aleatório, perfil fixo
e ordem de mensagens. Nenhum comando/caminho de aplicação vem do frame.

A sonda só é criada após validação estática, por uma capability de fixture.
Arquivos críticos são novamente autenticados e presos antes de Process.Start,
permanecendo presos até a saída. Falha de inspeção bloqueia o grupo IPC, sem
executar candidato para tentar torná-lo válido. Os arquivos da BETA não entram
nessa rota. O runner é apphost próprio; o teste não autentica qualquer assembly
apenas por estar no mesmo dotnet.exe.

READY ocorre após criação de HWND e entrada no loop WinForms. O prazo normal
é 30 s e um caso espera esse prazo real. Fechamento pede CLOSE, verifica BYE e
aguarda a saída real. Falha, busy, ausência/duplicata de READY, nonce/tx/PID
errado ou recusa de saída nunca confirmam a sonda. A limpeza do harness pode
encerrar somente filhos de teste que ele próprio criou; não há force-kill de
GF/processos terceiros. Esses testes não homologam a UI/saúde funcional do GF.

CurrentUserOnly compara usuário/elevação; a política ainda lê os tokens. Se o
runner já é elevado, os testes do canal usam essa condição herdada para testar
o kernel e o classificador operacional recusa elevação em teste separado.
Isso não aprova operação elevada. Usuário/sessão estrangeiros e caminho
positivo sem elevação só contam se realmente executados; não criar contas,
alterar ACLs/tokens ou inferir aprovação dessas situações.

Permanecem sem implementação operacional: bootstrap/custódia de produção,
transporte/canal, atualização do GF, launcher/helper, recuperação contra energia,
limpeza operacional e integração. Etapa 4 não iniciada. AVG/proteções, privacidade,
flags no/remoto bloqueado, drivers, VLC inventário e COM consultivo preservados.

Fontes primárias consultadas em 28/10/2026: fonte TUF fixada acima,
[PipeOptions](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.pipeoptions),
[PID cliente](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getnamedpipeclientprocessid),
[PID servidor](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getnamedpipeserverprocessid),
[GetTokenInformation](https://learn.microsoft.com/en-us/windows/win32/api/securitybaseapi/nf-securitybaseapi-gettokeninformation),
[FlushFileBuffers](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-flushfilebuffers)
e restrições/contrato das Etapas 0–2. Os limites experimentais são decisões do GF.
