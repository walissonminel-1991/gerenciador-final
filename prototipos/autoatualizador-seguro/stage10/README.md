# Etapa 10 — laboratório de pacote GF real

Este controlador compila a fonte funcional da entrada obrigatória
`4c1810a9d49bdb5fa1c6914354b32e5cb4602af9` em Windows descartável,
sem alterar versão, código, perfil ou canal da BETA .66. O SHA do checkout
experimental é registrado separadamente. Não inicia o GF, não assina os seus
bytes com fixtures, não gera entrega, não publica e não retorna autorização
para instalar. O diretório temporário e todos os binários são eliminados.

`python prototipos/autoatualizador-seguro/stage10/run_lab.py --report <fora-do-repositorio>/recibo.json`

Requer Windows/NTFS e SDK 10.0.402. O diretório de dados é criado pela API
normal Directory.CreateDirectory, com ACL herdada; nenhuma ACL é editada.
O controlador compara os descritores do diretório Python e do diretório .NET
somente por leitura, para diagnosticar recusas da guarda NTFS.

Linux recebe `blocked`, nunca aprovação.
O workflow separado tem `contents: read`, ações fixadas por SHA, nenhuma etapa
de upload, nenhum segredo e nenhum retry. Executa somente a nova qualificação
e os deltas abaixo, sem suites anteriores. Saídas C# ficam fora dos globs GF.

## Componentes

- `LabMetadata`: metadados ECMA-335 via PEReader, sem carregar assembly do GF;
  recursos Win32 via BoundedPe, identidade, versão, asInvoker e compatibilidade.
  Reutiliza apenas a política textual de consentimento, sem chamar o produto.
- `LabPackage`: conjunto exato de arquivos, SHA-256, guarda NTFS da Etapa 2,
  ancestrais e identidades retidos, streams alternativos, PE AMD64 ou IL AnyCPU
  compatível e Authenticode dos dois arquivos do GF. beta-unsigned exige
  ausência real de tabela de certificado **e** TRUST_E_NOSIGNATURE; uma
  assinatura existente com falha nunca é convertida em unsigned.
- `StreamPackage`: verificador/extrator aprovado da Etapa 4, inalterado. Confere
  headers, CRC, hash do ZIP, receita exata e bytes gravados; nenhuma execução.
- `LabBinding`: liga a qualificação ainda retida ao digest do estado, seletor,
  instalação e journal da Etapa 9. Recusa ticket fechado, cancelamento,
  pendência, inserção de arquivo e estado antigo. Somente relatório em memória;
  não oferece API para selecionar ou confirmar GF real.
- `EpochGuardedCache`: fronteira nova para o cache de teste do cliente TUF
  experimental da Etapa 8. Após validação de raiz contígua/duplo threshold pelo
  cliente, recusa mudança de chaves/threshold/material de timestamp, snapshot
  ou targets antes de avançar esse cache. Raiz sozinha pode rotacionar. Isso
  **não** implementa reset normativo de pisos por época ou persistência durável
  de cada raiz/papel; esses caminhos continuam bloqueados. Não é usado pela
  Etapa 9 histórica nem pela BETA. O cliente/biblioteca aprovados não mudaram.

## Limites

A receita/hash do laboratório fornece integridade e rastreabilidade local,
sem autoria operacional. A cadeia TUF usa exclusivamente pacotes de texto
inertes e seeds públicas preexistentes; não autoriza o pacote GF compilado.

Inspeção estática e retenção de arquivos não provam carga tardia, prontidão,
encerramento seguro, controle de todas as instâncias/workers ou rollback do GF.
O ensaio de leitura pendente usa bytes GF reais sob ownership/journal de
laboratório, com interrupção controlada da espera. Não representa driver
travado, energia perdida, IPC de produto ou ciclo operacional.

Assinaturas das dependências são inventariadas pela tabela PE; a qualificação
Authenticode explícita cobre EXE e DLL do GF. Os hashes de todas as dependências
são conferidos, mas não se alega revisão de publicador/revogação de cada DLL.
A busca de segredos cobre padrões de tokens GitHub e blocos de chave privada,
sem exibir correspondências, e não prova ausência de qualquer tipo de segredo.
G1–G8 só podem ser fechados mediante os seus critérios próprios.
