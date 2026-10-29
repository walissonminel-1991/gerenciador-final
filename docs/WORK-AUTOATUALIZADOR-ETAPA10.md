# Autoatualizador GF — Etapa 10: qualificação de pacote real em laboratório

29/10/2026, America/Sao_Paulo. **Etapa 10 concluída no seu escopo técnico.
Zero gate operacional fechado; oito bloqueados. Etapa 11 NÃO INICIADA.**
PR #65 aberta/Draft, branch `work-integracao-final-20261027`.
Entrada obrigatória: `4c1810a9d49bdb5fa1c6914354b32e5cb4602af9`, confirmada
antes de qualquer edição e novamente antes do salvamento. Esta missão continua
o autoatualizador, sem reabrir a homologação física parcial.

Código de laboratório validado: `7a06b6c217c96f112eb3062be86d750683a58a3d`.
O SHA do encerramento documental posterior consta na descrição da PR e na
resposta de entrega. Contrato `gf-self-update/1`, fonte funcional da .66,
verificador aprovado e bibliotecas anteriores permanecem inalterados.

## Resultado concreto e implementação

O novo [controlador](../prototipos/autoatualizador-seguro/stage10/README.md)
compila o GF real no Windows Server 2022 descartável, por receita portátil
self-contained, perfil `local`, sem endpoint. Confere que os 142 itens Compile
do GF não incluem protótipos e que os insumos herdados da entrada não mudaram.
Não inicia EXE, DLL, GUI, worker ou método do produto. A leitura do assembly
usa PEReader/ECMA-335; não usa Assembly.Load ou LoadLibrary.

Foram implementados e demonstrados:

- Inspeção de identidade/versão/metadados BETA e três recursos incorporados;
  manifesto Win32 asInvoker/uiAccess=false e identificação de sistema suportado.
- Qualificação do conjunto exato por receita/hash local, ancestrais/identidades
  NTFS retidos, revalidação antes do uso e recusa de hardlink, stream alternativo,
  arquivo inserido e ticket descartado. Os 287 arquivos negaram abertura para escrita.
- Reutilização sem alterações de `scripts/verificar_pacote.py`, `BoundedPe`,
  `StreamPackage`, `OfflineAuthenticode`, guarda NTFS e componentes 2/8/9.
  ZIP real extraído por CreateNew, CRC, hashes e nova inspeção dos bytes no disco.
- `LabBinding`, que vincula qualificação retida a instalação, estado, seletor,
  journal, receita e manifesto. Estado antigo ou pendente é recusado. O binding
  não escreve seletor, não confirma GF real e mantém `Installable=false`.
- `EpochGuardedCache` no adaptador novo: após o cliente verificar raiz contígua
  e duplo threshold, recusa mudança de chave/threshold/material de timestamp,
  snapshot ou targets antes de esse cache avançar. A rotação somente da raiz
  passou. Isso não implementa reset normativo por época e não altera o adaptador
  histórico da Etapa 9 ou o produto.

## Pacote real e integridade

O [recibo Windows completo](evidencias/autoatualizador-etapa10-windows.json)
registra nome, tamanho, SHA-256 e identidade NTFS de cada arquivo.

| Verificação | Resultado do pacote qualificado na rodada 3 |
| --- | --- |
| Origem funcional | Entrada obrigatória 4c1810a9; checkout de laboratório 7a06b6c2; nenhuma alteração funcional GF |
| SDK / runtime do verificador | 10.0.402 / 10.0.14 |
| Runtimes incluídos no pacote | Microsoft.NETCore.App 10.0.13 e Microsoft.WindowsDesktop.App 10.0.13 |
| Arquivos / hashes do manifesto | 287 / 286; o próprio manifesto é coberto pela receita externa |
| Bytes expandidos / ZIP | 153.051.445 / 57.710.208 |
| SHA-256 ZIP | db823c42664da274b7df2ab030f325154436d03d59b42f144484529353916858 |
| SHA-256 manifesto | e00317b1fb9f934f1520b007dd0769e0c961bdc285d481c35202d2b8ea6a5dc2 |
| PEs | 273: 131 AMD64 e 142 assemblies IL AnyCPU compatíveis; EXE AMD64 |
| Tabelas de certificado inventariadas | 270; não equivale a validar publicador/revogação de todas as dependências |
| EXE e DLL GF | Sem tabela de certificado; WinVerifyTrust offline 800b0100 em ambos |
| Recursos/licenças/auxiliares | Conjunto exato, hashes e composição aprovados; três catálogos incorporados conferidos |
| Destino após ensaio | Árvore de laboratório apagada; zero upload e zero artefato de workflow |

YamlDotNet 18.1.0 e ComInterop 1.29.380 permaneceram iguais; hashes NuGet
resolvidos constam no recibo. O lockfile do verificador preserva BC 2.7.0;
20 arquivos da fonte TUF fixada e o patch original da Etapa 8 foram conferidos.
O pacote contém versão .66 para inspecionar a receita existente: não recebeu
nova versão pública/BETA e não é a reprodução binária da entrega privada.

**Qualificação local de bytes não é autoria editorial.** Nenhum metadado TUF
assinou ou autorizou este pacote GF; as seeds públicas preexistentes assinaram
somente fixtures inertes/raízes de teste. G3 continua bloqueado pela âncora e
metadados operacionais ausentes. beta-unsigned permanece permitido; a regra
exige ausência real de certificado e TRUST_E_NOSIGNATURE. Não se converte falha
de assinatura existente em ausência e não se exige certificado comercial.

A busca limitada de tokens GitHub e blocos de chave privada não encontrou
correspondências na primeira compilação. Não é prova de ausência de todo segredo;
não foi repetida nas rodadas dirigidas de fonte funcional igual. As assinaturas
de todas as DLLs de terceiros não tiveram revisão de confiança/revogação.

## Rodadas, falhas e correções

[Registro composto](evidencias/autoatualizador-etapa10-rodadas.json).
Todas as execuções usaram tentativa 1, sem retry automático. Os oito checks
Python e os quatro casos Windows já aprovados não foram repetidos.

| Run / código | Resultado e limite |
| --- | --- |
| [40852886229](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40852886229) / 32021adf | Build GF/ferramenta e 8/8 checks Python aprovados; C# não executado, porque o marcador LF virou CRLF. Falha preservada |
| [40853270760](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40853270760) / 695cf6a3 | 4/19 aprovados, 15 falhos: ACL do diretório Python recusada e dependentes sem qualificação; novas fixtures com sétima chave indevida recusadas antes da fronteira de época |
| [40853787836](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40853787836) / 7a06b6c2 | **15/15** casos afetados aprovados; novo pacote passou preflight obrigatório de integridade; quatro aprovados anteriores não repetidos |

São **27 casos distintos com evidência composta: 8 + 4 + 15**. Não é uma
bateria completa de 27 no SHA final. A primeira e a segunda falhas permanecem
nos JSONs; uma recusa ambiental não foi contada como aprovação.

A comparação somente de leitura dos descritores mostrou ACE OWNER_RIGHTS no
diretório Python: `O:BAD:P(A;OICI;FA;;;OW)(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)`.
A guarda aprovada não o admite. Os dados passaram a usar criação normal .NET,
com ACL herdada: `O:BAD:(A;OICIID;FA;;;SY)(A;OICIID;FA;;;BA)(A;OICIID;FA;;;LA)`.
Nenhuma ACL, whitelist, permissão Windows ou proteção foi editada para aprovar.
Também foram corrigidos o nome Win32 do stream padrão (`::$DATA`), o encerramento
do lease antes da inspeção por novo coordenador e a fixture que agora substitui
a chave antiga, mantendo exatamente seis chaves. A restrição de seis é do
perfil experimental; não é alegação de conformance TUF geral.

Os negativos cobrem DLL adulterada, dependência ausente inclusive após regerar
manifesto, arquivo inesperado, manifesto inconsistente, versão/arquitetura
incompatíveis, contêiner com hash incorreto, hardlink, ADS, inserção posterior,
ticket fechado, estado antigo, journal pendente, cancelamento e três mudanças
de época. O controle de espera retém os pins do pacote real até término da
tarefa original e mantém o journal pendente para o coordenador seguinte.
A pausa acontece antes da revalidação: não representa I/O de driver travado.

## Revisão TUF e preparação do ciclo GF

[Revisão dirigida](evidencias/autoatualizador-etapa10-revisao.json).
Foram relidos UTC imutável, cliente único, timestamp inalterado, reancoragem do
cache, pisos persistentes, rotação, recuperação e guarda pré-commit. As provas
anteriores não foram reexecutadas. A fronteira de época nova passou com raízes
assinadas válidas, conservando o pacote original e o patch anterior.

Continuam pendentes persistência normativa de cada raiz/papel durante falhas,
reset/recuperação dos pisos por época, vetores interoperáveis, aceite de
manutenção e revisão independente. Esta revisão do implementador não substitui
revisão independente. Referência normativa: [TUF 1.0.36](https://theupdateframework.github.io/specification/v1.0.36/).
O cache de laboratório que recusa transições não satisfaz essas exigências.

A revisão de Program.cs identificou GUI, leitor COM e `--worker` como entradas
que precisarão participar do futuro protocolo. FormClosing pode cancelar só a
espera e adiar fechamento; instalador/desinstalador pode continuar. O coordenador
BETA cuida da janela/envio, sem provar término de participantes, prontidão ou
rollback. Não há prova nova de IPC padrão/entre sessões. O binding novo reduz
a distância de pacote→estado, sem criar aplicação operacional ou encerramento
sintético que pudesse fechar G6/G8.

## Gates, preservação e próxima etapa

[Matriz atualizada](evidencias/autoatualizador-etapa10-gates.json): G2, G3, G4,
G6 e G8 receberam novas subprovas; **nenhum dos oito gates foi fechado**.
G1/G5/G7 mantêm os bloqueios anteriores. Independência de frescor, energia e
armazenamento real, IPC/ciclo GF, custódia/âncora e conformidade TUF permanecem
necessários antes da integração.

BETA privada v5.13.11.66, fonte funcional 6df95fdd, run 40716548530,
artefato 12828262039 e ZIP SHA-256
8e11f2dda5b7e2df2f85c61b57cc6a31a9596628523d2be7ad155dfdd5dda7f2
preservados. Não houve acesso/conversão desse ZIP, alteração do canal privado,
fonte funcional, dez workflows preexistentes ou main. Três compilações de
laboratório tiveram origem/hashes próprios e foram apagadas. Zero execução GF,
artefato enviado, manutenção nos PCs pessoais ou repetição de suítes antigas.

Roadmap **27/42**, mesmos IDs/done; avaliação física parcial preservada.
AVG/Windows, consentimento/privacidade, remoto desativado, PILOT_ENABLED=no,
GITHUB_EXPORT_ENABLED=no, drivers inativos, VLC só inventário e COM consultivo
preservados. Sem chaves operacionais, trust store/permissões/infraestrutura,
Worker/D1/DNS, merge, Release, deploy ou publicação.

O menor próximo escopo útil é **Etapa 11 isolada de G2**: persistência por
raiz/papel, pisos/reset por época, recuperação com interrupções/antirreplay e
vetores de interoperabilidade. Exige missão nova; para eventual aceite G2,
designar manutenção e revisor independente. Esse escopo não exige criar chaves
de produção, mudar a .66, integrar ao GF ou alterar o canal. A futura integração
continuará exigindo os demais gates e autorizações específicas. **Etapa 11 não
iniciada; encerramento após salvar e verificar a PR #65 Draft.**

[Recibo](evidencias/autoatualizador-etapa10.json) ·
[Checkpoint](WORK-AUTOATUALIZADOR-CHECKPOINT.md).
