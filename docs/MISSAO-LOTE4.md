# Lote 4 — consolidação dos lotes 1–3

## Base e escopo conferidos antes de editar

Main `410d65e23546976db456337c71e4705f10217d36`; PR #40 aberta em Draft,
HEAD `ef13aef49083f357f6a14f4de46f8bc976dbd830`. Base cumulativa escolhida:
Lote 3 `8f8d1c8fb109ba4956876b398f02dee6ce70f2e3`,
[CI 40231535067 aprovada](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40231535067).
Lotes [1](MISSAO-LOTE1.md), [2](MISSAO-LOTE2.md) e [3](MISSAO-LOTE3.md)
estão na ancestralidade; não houve merge de PR.

Branch `work-lote4-consolidacao`, [PR #57 Draft](https://github.com/walissonminel-1991/gerenciador-final/pull/57),
baseada no Lote 3. WORK14–17 estão em outra cadeia de PRs, sem integração
neste pacote. Não anunciar cobertura ou funcionalidades exclusivas delas.
A numeração .63 evita reutilizar .60–.62 dessas frentes.

## Mudanças pequenas e verificáveis

| Área | Achado | Correção e limite |
|---|---|---|
| Relatório local | TextBox com barras nativas e linhas sem quebra; sem Fechar/Esc | Mesmo DarkLogView dos demais relatórios, quebra de linha, barra escura e texto estático integral |
| Janela do relatório | Sem ajuste à área útil e decoração escura explícita | Ajuste existente de diálogos, modo escuro quando suportado, Fechar/Enter/Esc e janela associada ao GF |
| Controle compartilhado | Dock Fill do editor ocupava também a área da barra escura | Ordem de Dock corrigida em DarkLogView; testes exigem retângulos sem interseção no histórico e nos drivers |
| Documentação | TESTES ainda citava .49 e downloads opcionais em PR; roteiro físico citava .57 | Procedimentos atuais, SDK 10.0.402 exato e distinção entre jobs obrigatórios e opt-in |
| Referências | PROGRESSO/README ainda selecionavam WORK13 apesar dos lotes posteriores | Base aprovada do Lote 3 registrada; candidata .63 inicialmente pending e separada do último pacote aprovado |

O conteúdo exportado continua sendo o relatório completo obtido antes de abrir
o diálogo; o limite de 65.536 caracteres do registro em tempo real não corta
esse texto estático. Não altera eventos, autorizações, regras de instalação,
desinstalação ou exclusão, nem acrescenta upload ou dados identificadores.

## Testes que distinguem o defeito da correção

`TestesLayout --lote4` usa MainForm real sem serviços, 150 estados sintéticos
que deixam 128 eventos recentes, texto de comparação conhecido e identificador
privado fictício. Abre pelo botão real, verifica 520×360 e 770×530, texto
integral, somente leitura, quebra de linha, rolagem até o fim, ações acessíveis
e fechamento. Não abre o seletor de arquivo nem executa tarefas de manutenção.

A primeira execução, fonte `a2d8c413`,
[CI 40233614938](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40233614938),
falhou com **cenário não executado**: o teste não aguardava o feedback de
145 ms do ModernButton. Isso foi falha do teste, não comprovação do defeito
visual. O teste agora bombeia eventos até o diálogo abrir e aguarda seu
fechamento real. A correção do teste não remove nenhuma asserção visual.

`scripts/Testar-Regressao-Relatorio.ps1` cria checkout descartável do HEAD,
substitui apenas Program.cs pela implementação do Lote 3 e executa os mesmos
testes atuais. Exige a exceção específica da rolagem nativa após abertura do
diálogo. Compilação quebrada, modo não executado, timeout ou outro erro não
contam como defeito detectado. Um segundo controle restaura o diálogo atual e
usa DarkControls anterior: exige a recusa específica da sobreposição entre
editor e barra. O checkout é removido em finally.

A fixture Python de metadados também copiava exclusivamente o relatório WORK13,
falhando ao selecionar um relatório novo. Agora usa o relatório declarado pela
candidata e mantém um controle negativo que exige recusa se ele estiver ausente.

A marca final do cenário positivo e seu código de saída são obrigatórios
na CI. Regressões anteriores, 27 mutações, política, downloads oficiais,
drivers em Windows 2022/2025 e composição do pacote permanecem obrigatórios.
Os instaladores externos são baixados/verificados, nunca executados.

Na CI intermediária [40234097635](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40234097635),
fonte `7595c07c`, o mirror `mirrors.middlendian.com` retornou HTTP 403 após
redirecionamentos HTTPS do VLC. Download permaneceu reprovado e pacote
bloqueado. Nenhum fallback HTTP, exceção de assinatura ou origem foi adicionado.
A mesma revisão visual levou a testar explicitamente a separação entre editor
e barra, além de visibilidade e limites do viewport.

## Estado da candidata

**v5.13.11.63 TESTE aprovada tecnicamente.** [CI 40234268549](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40234268549),
tentativa 1, fonte `a893a051f353a1c9db3b5dd086539c6f252de93b`, build
`30e66ad2312047df02d6e00d70799c7624957cf6`. Todos os jobs obrigatórios
aprovados; consulta ampliada opt-in e atestação exclusiva da main ficaram
skipped. Regressões: 318 atualizadores, 655 resíduos/desinstalação, 27/27
mutações rejeitadas, 40 Python, 10 Node, 132 combinações resolução/aba e
os dois controles negativos Lote 4. Cenário positivo e diálogo de drivers
passaram com editor e barra sem interseção. Compilação/analisadores sem avisos/erros.

[Pacote 12614153157](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40234268549/artifacts/12614153157), 59.194.445 bytes; SHA-256:
`c2cad696979b20c02e9a1f85ef7735095d987f3be615a03cc5fd66fccf8154a3`.
Baixado e verificado fora do runner, **sem executar**: 282 arquivos/281 hashes,
151.893.431 bytes expandidos; AMD64, EXE/DLL FileVersion 5.13.11.63,
runtimes .NET/WindowsDesktop 10.0.13 self-contained, manifesto incorporado
`asInvoker` e origem/base/build exatas. O contrato rejeita binário extra,
fixture, dependência ou runtime divergente. ZIP +210 bytes e conteúdo +512
bytes ante a base Lote 3; sem nova dependência de produção.

[Capturas 12614960317](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40234268549/artifacts/12614960317): conferidos visualmente
histórico em 520×360/770×530, drivers em 500×340, Aplicativos em 1280×720 e
Reparo e rede em 1440×900. Barra agora visível ao lado do texto; botões
acessíveis, sem sobreposição. São viewports sintéticos. O runner Windows
Server conserva decoração nativa clara; DWM escuro depende de suporte do
Windows e não foi homologado fisicamente nos PCs.

[Evidência antivírus 12614457484](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40234268549/artifacts/12614457484), ZIP SHA-256
`b2e374bc23cefdb05dbf33e7f5034181d4e15bbeb19624f61a01acdcea72fdbc`:
hashes/tamanhos dos dois binários coincidem com o pacote. Status NotSigned,
sem signatário ou certificado embutido, scan_performed=false,
sample_submitted=false, avg_validation=pending. Isso não é scanner/atestado AVG.

[Registro durável dos jobs, artefatos, hashes, controles e limites](evidencias/lote4-ci-40234268549.json).
O registro aponta a fonte técnica testada. Commits posteriores de evidência
não alteram essa fonte/pacote selecionado. [Roteiro posterior](TESTE-CONSOLIDADO.md)
e documentação atualizados; nenhum checkpoint físico fechado.

Reconferência independente da base herdada, sem executar: ZIP Lote 3 final
`f423d602b1feee1f9602d02563f2d6e8ca94b59ed84c33c08035a3dabd9e753a`,
282 arquivos/281 hashes, 151.892.919 bytes expandidos, AMD64, runtime
.NET/WindowsDesktop 10.0.13, origem `8f8d1c8f`, build `6fb9c74f` e base
`6a020c86`. Essa conferência não aprova a candidata .63.

## Critérios e homologação separada

| Critério | Situação nesta missão |
|---|---|
| CI completa do código selecionado e política aprovadas | Aprovadas: fonte a893a051, CI 40234268549 |
| Dois controles negativos detectam os defeitos anteriores e positivo passa | Aprovados no Windows descartável |
| Capturas do relatório e matriz de layout revisadas | Cinco capturas inspecionadas e 132 combinações validadas |
| ZIP conferido fora do runner: origem, composição, runtime e hashes | Aprovado, sem executar o produto |
| Homologação física U8/R7/R4, I7, DPI real, bandeja/boot/UAC | Pendente; não executada |
| D2/G1/G2, casos residuais R6 e AVG P5/P6 | Critérios anteriores ainda abertos |

Nenhum checkpoint fechado: **27/42**. Runners e fixtures descartáveis não
homologam os PCs, cobertura universal, desempenho residente ou AVG.
GF continua sem assinatura; a evidência antivírus declara ausência de scanner
e envio de amostra. Nenhuma proteção foi desabilitada ou contornada.

[Roteiro físico posterior](TESTE-CONSOLIDADO.md): apenas operações já necessárias,
sem reinstalar aplicativos antigos, provocar reboot ou repetir limpezas para
produzir trabalho. Nenhum acesso/modificação nos PCs pessoais, merge, release
estável ou promoção da PR #40 nesta missão.
