# Autoatualizador GF — Etapa 1: biblioteca, bootstrap e protótipo isolado

28/10/2026, America/Fortaleza. **Etapa 1 concluída no escopo isolado; Etapa 2
não iniciada.** Entrada `052ed3c8aefc3c90083325401dbf52fbd0de0554`, PR #65
aberta/Draft, branch `work-integracao-final-20261027`.

[Contrato da Etapa 0](WORK-AUTOATUALIZADOR-ETAPA0.md) e
[checkpoint](WORK-AUTOATUALIZADOR-CHECKPOINT.md) lidos; contrato preservado.
BETA v5.13.11.66, fonte `6df95fdd776644cd4b248b5cea61108974ca9ce9`, build
privada 40716548530/artefato 12828262039 preservados sem recompilação/download.
Avaliação física parcial e roadmap **27/42** conservados. Sem leitura operacional
ou alteração do repositório privado de entregas.

## Seleção de biblioteca

Escolha para o protótipo: **Tuf 1.1.1**, dependência **BouncyCastle.Cryptography
2.7.0**, ambas com versão exata e contentHash no lockfile. O cliente fornece o
fluxo de metadados TUF; Bouncy Castle fornece Ed25519. Não foi criado primitivo
criptográfico próprio ou cliente HTTP de atualização.

| Alternativa | Avaliação e decisão |
| --- | --- |
| Tuf 1.1.1 + BouncyCastle 2.7.0 | Escolhido: .NET 10, licença MIT, API de repository/cache substituível, rotação/threshold/versões na biblioteca. Dependências fechadas em dois pacotes e sem binário nativo adicional. Exige guardas do perfil GF e revisão futura; não equivale a auditoria independente. |
| BouncyCastle direto | Primitivo adequado, mas não substituiria fluxo TUF, gerenciamento de metadados e rotação. Usado diretamente somente no teste de vetor RFC; evitar escrever protocolo inteiro por conta própria. |
| NSec.Cryptography 26.4.0 | Alternativa Ed25519 documentada, baseada em libsodium. Acrescenta dependência nativa e ainda não resolve o fluxo TUF; não adicionada. |
| Aceitar hash/assinatura avulsa do ZIP | Não atende confiança inicial, replay/expiração e cadeia de rotação do contrato; não adotado. |

Versões estáveis disponíveis nas fontes oficiais foram conferidas; nenhum
pre-release/algoritmo experimental foi escolhido. Fonte do cliente revisada no
commit upstream `c0f5ff9747aa9b00c45a964dca275be4066cd489` (tag v1.1.1).
Essa revisão foi dirigida às APIs usadas e às fronteiras de confiança, não
uma auditoria completa da biblioteca.

NuGet restore locked/audit com falha para NU1900–NU1905 preservado. Consulta
de vulnerabilidades conhecidas não reportou pacotes vulneráveis nessas versões
na execução registrada; isso não prova ausência de vulnerabilidades. Assinaturas
dos dois nupkgs foram verificadas com `dotnet nuget verify --all` (sucesso):
Tuf com assinatura de repositório NuGet; Bouncy Castle com autor e repositório.
Nada foi copiado para o bundle ou dependências do GF .66.

| Pacote de desenvolvimento | SHA-256 do nupkg verificado |
| --- | --- |
| Tuf 1.1.1 | `f2312652169ed5a5ad394ff40a431ab93b19638cdff72c207a58ce0c2bba25bc` |
| BouncyCastle.Cryptography 2.7.0 | `f091ffccab4d03993e660bace277659a79dee0972f54d7f1f4bd46d680966241` |

## Guardas do adaptador e prova obtida

A biblioteca é usada com repository selado em memória e cache copiado. Nunca
acionar seus defaults HTTP/cache em disco. Um preflight limita bytes/profundidade,
recusa JSON duplicado/float/overflow e fecha schema/algoritmo/papéis antes de
entregar bytes à biblioteca. Valores GF só orientam a validação após a cadeia
assinada ser validada. Nenhum dado escolhe comando ou diretório de instalação.

A raiz em cache não se torna âncora apenas por estar autoassinada: o adaptador
reconstitui a cadeia desde o fingerprint incorporado e verifica vínculo da raiz
anterior. Metadados antigos mantêm pisos antirreplay; cache expirado pode receber
cadeia nova válida sem aceitar respostas novas já expiradas. Estes dois caminhos
têm casos positivos/negativos específicos.

[Código e instruções](../prototipos/autoatualizador-seguro/README.md) ·
[POUF experimental](../prototipos/autoatualizador-seguro/POUF.md) ·
[Resultados por teste](evidencias/autoatualizador-etapa1-testes.json).

Resultado final: **66/66 testes aprovados**, SDK 10.0.402/runtime .NET 10.0.13,
Linux x64. Build do protótipo: zero erros/avisos. 42 cenários assinados gerados
por Python cryptography 50.0.2, verificados pela implementação .NET independente;
vetor RFC8032 conhecido e mutação de assinatura também verificados. Contagens de
cenários, mutações e execuções de correção não são somadas aos 66 testes nem ao
roadmap. Nenhum processo GF ou tarefa física foi iniciado.

Durante o desenvolvimento, defeitos nas fixtures (mutação compartilhada de chave
entre cenários e transcrição de vetor) foram corrigidos sem relaxar rejeições.
A revisão encontrou também inclusão potencial de C# gerado por obj no glob do
produto: saídas foram movidas para temporário externo. A verificação final por
MSBuild **somente avaliação** confirmou **142 fontes do produto e zero do
protótipo**. O projeto principal/propriedades/manifesto não foram alterados.

## Modelo de bootstrap e custódia — definido, não provisionado

Responsável inicial: mantenedor nominal, sem convidar pessoas ou aplicativos.
Separação de chaves significa materiais e locais distintos; não exige adicionar
colaboradores ao GitHub. Nenhuma chave operacional, cofre, permissão, assinatura
de produção ou raiz confiável de produção foi criado nesta etapa.

| Papel | Custódia definida para adoção futura | Uso e recuperação |
| --- | --- | --- |
| root | Três chaves Ed25519 independentes; quórum 2-de-3. Materiais offline, cifrados, em suportes/locais separados; não reunir três cópias no mesmo host/conta/segredo. | Cerimônia de raiz/rotação, com verificação por quórum antigo e novo. Backup de cada material em proteção separada; testar recuperação antes de adotar a raiz. |
| targets | Uma chave offline exclusiva para autorização editorial; não usada no job de compilação, não igual à raiz ou credencial GitHub. | Assinar somente metadados que apontem aos bytes já validados por SHA, após decisão do mantenedor. Comprometimento exige revogação pela raiz e release corretiva; assinatura não garante qualidade. |
| snapshot | Chave própria, offline no primeiro fluxo manual. | Fechar versões/hashes de targets autorizados, sem poder escolher outro pacote isoladamente. |
| timestamp | Chave própria; primeiro fluxo manual offline. Não colocar no PC do avaliador ou EXE. Eventual serviço de assinatura seria outra implantação, fora desta missão. | Atualizar frescor dentro de 72 h/validade do artefato, sem mudar payload. Revogar pela raiz em caso de incidente. |

Cerimônia futura: gerar materiais com ferramenta/library adequada em ambiente
offline confiável; registrar apenas chaves públicas, IDs, quóruns, versão e
fingerprint; conferir separação/backup e restauração com desafio inerte; duas
raízes assinam o root inicial; conferir por revisão independente da cópia pública.
Não fornecer senha/seed ao GF, GitHub, CI ou ao gerador público de fixtures.

Primeiro bootstrap operacional será uma **nova entrega manual autorizada** em
pasta nova, contendo raiz pública/fingerprint incorporados. O mantenedor confere
esse fingerprint com registro previamente confiável; repeti-lo no mesmo ZIP não
resolve autenticação inicial. Build, source SHA, root version/fingerprint e hashes
devem constar do recibo dessa entrega futura. Sem chave confiável anterior, não
aceitar discovery automático, TOFU ou botão ignorar assinatura. A .66 não ganha
autoatualizador por flag e não é sobrescrita/convertida.

Perda de uma chave root: usar as outras duas para rotacionar e retirar a perdida.
Comprometimento/perda do quórum: parar autorização de updates e recuperar por
outro bootstrap manual autenticado, fora da cadeia comprometida. Não reduzir
quórum, reutilizar seed de fixture, restaurar confiança silenciosamente ou reduzir
pisos para aceitar release antiga. O backend conectado permanece fora desse fluxo.

O teste usa outro universo: raiz/fingerprint determinísticos públicos, seeds
de teste deriváveis e dados inertes. Chaves de fixture não constituem execução
da cerimônia acima ou custódia real aprovada. A operação de custódia continua
pré-requisito para qualquer adoção em produto.

## Cobertura do contrato sem confundir com homologação

| Casos da Etapa 0 | Resultado alcançado na Etapa 1 |
| --- | --- |
| V01/V02/V03 | Cobertos em fixtures: adulteração, tupla/versão/seq, replay, validade, relógio, versão exata/no-op. |
| V04 | Política de canal/arquitetura/perfil/origem em formato canônico; sem prova de PE/build real. |
| V05/V06 | Sem acesso a credenciais/rede e alvo remoto recusado; autenticação GitHub/redirect/header de rede real não implementados. |
| V07/V08 | Nomes, symlink/collision, ZIP/CRC, limites de leitura/expansão/JSON e cancelamento em memória. Hard links/NTFS, disco cheio e stream de rede real não homologados. |
| V09/V10/V11/V12/V13 | TOCTOU por handles, DLL em uso, journal/crash, concorrência entre processos e IPC pelo kernel não implementados; flags de contexto locais são somente mocks. |
| V14 | Entradas/cache do chamador preservados em memória; rollback e preservação de dados físicos não ensaiados. |
| V15 | Âncora, papéis/quórum, duplicatas, rotações antiga/nova e cache sem vínculo testados; custódia de produção apenas definida. |
| V16 | Nenhuma execução AVG, WinVerifyTrust, health check ou alteração das proteções. |
| V17/V18 | Âncora de teste fixa, nenhum caminho de adoção/aplicação real e schemas/JSON/overflow testados. Bootstrap real e conformidade TUF integral não declarados. |

Essas evidências não completam os 18 casos de homologação em Windows ou toda
a Etapa 0. Nenhuma mudança de status físico ou de checkpoint de roadmap é feita.

## Continuidade e preservação

Etapa 1 termina neste protótipo. **Etapa 2 NÃO INICIADA.** Permanecem gates:
cache/pisos duráveis, relógio uniforme, NTFS/handles/TOCTOU, seletor/diário/IPC,
PE/runtime/licenças reais e operação de custódia/bootstrap. Não desenvolver ou
acionar esses componentes agora, nem publicar uma versão por causa dos testes.

Canal privado, sete dias da entrega existente, acesso do mantenedor, BETA .66,
main/branches históricas, CI do produto e metadados conectados preservados.
AVG/proteções intactos, VLC só inventário sem consultas/ações/testes específicos,
WinGet COM opcional/consultivo/não bloqueante, drivers de instalação/remoção/
reversão desativados, diagnóstico remoto bloqueado, PILOT_ENABLED=no e
GITHUB_EXPORT_ENABLED=no. Sem Worker/D1/DNS, publicação, merge, Release, deploy,
agenda, novo binário de produto ou repetição de testes físicos.

## Fontes primárias e recibos

Consultadas em 28/10/2026; escolhas de escopo do GF são decisões deste protótipo.

- [Tuf 1.1.1](https://www.nuget.org/packages/Tuf/1.1.1) e [fonte fixada](https://github.com/mitchdenny/sigstore-dotnet/tree/c0f5ff9747aa9b00c45a964dca275be4066cd489/src/Tuf).
- [BouncyCastle 2.7.0](https://www.nuget.org/packages/BouncyCastle.Cryptography/2.7.0) e [distribuição oficial](https://www.bouncycastle.org/download/bouncy-castle-c/).
- [NSec 26.4.0](https://www.nuget.org/packages/NSec.Cryptography/26.4.0), alternativa não adicionada.
- [TUF 1.0.36](https://theupdateframework.github.io/specification/v1.0.36/) e [RFC8032 §7.1](https://www.rfc-editor.org/rfc/rfc8032.html#section-7.1).
- [Recibo desta etapa](evidencias/autoatualizador-etapa1.json) · [checkpoint](WORK-AUTOATUALIZADOR-CHECKPOINT.md).
