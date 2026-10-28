# POUF experimental GF — Etapa 1

Revisão 1, 28/10/2026. Perfil restrito de pesquisa para `gf-self-update/1`, baseado
em TUF 1.0.36. **Não é certificação de conformidade TUF nem política de produção.**
O contrato da Etapa 0 continua normativo; seus requisitos não são considerados
implementados por inteiro por estes testes.

## Protocolo e operações

O processo só oferece `--self-test <fixtures> <relatório>`. O validador aceita
bytes em memória, contexto local e uma cópia de cache de teste; retorna
`FixtureOnly=true`, `Installable=false`. Não há comando aplicar/recuperar,
processo privilegiado, importação de executáveis, gravação de estado do GF,
download, URL configurável ou transporte de rede.

O único ITufRepository é selado e serve arrays copiados. Nomes, contagens e
tamanhos de entrada/cache/bootstrap são limitados **antes** de clonar arrays.
A URI `.invalid` é
identificação obrigatória da API, não destino consultado. Repository nunca é
null; não se instancia HttpTufRepository ou FileSystemTufCache. Não se instala
exportador de métricas/traces. As fontes e os ZIPs de teste nunca são executados.

Fluxo: autorização/contexto → âncora incorporada → preflight limitado de JSON e
política → biblioteca TUF para encadeamento/assinaturas/versões → política GF
autenticada → hashes do contêiner/portátil → inspeção ZIP/CRC/manifesto em memória
→ resultado de fixture. O cache do chamador não é alterado. A proposta de cache
retornada não é persistida nem aprova uma instalação ou sequência.

O cliente TUF considera cache confiável. Por isso o adaptador recomeça a cadeia
de raízes na âncora fixa e exige que a raiz anteriormente recebida no cache
coincida com a cadeia revalidada. Não aceitar apenas uma raiz autoassinada do
cache. Versões anteriores de timestamp/snapshot continuam pisos de antirreplay.
Cache expirado pode ser renovado por metadados frescos; metadados expirados
recebidos como nova resposta são recusados.

## Uso, formato e limites

- Quatro papéis TUF, Ed25519 puro, raiz 2-de-3, demais papéis 1-de-1, seis
  chaves distintas entre papéis, IDs SHA-256 dos objetos de chave canônicos.
  Raízes sucessivas são validadas pela biblioteca com quórum antigo e novo.
- Sem delegações e `consistent_snapshot=false` neste perfil de fixture. Rotação
  limitada a 32, requisições de raiz limitadas e alvo único fixture-package.zip.
- Wire JSON UTF-8, sem comentários/trailing comma, profundidade 32. Preflight
  recusa chaves duplicadas, floats, expoentes, negativos e inteiros acima de
  2^53−1. Campos TUF/GF desse perfil são fechados. Canonicalização/assinaturas
  TUF são da biblioteca, verificadas contra fixtures assinadas em Python,
  incluindo Unicode/emoji, aspas, barra inversa e newline. Não é JCS/RFC8785.
- Metadata limits iguais ao contrato: timestamp 64 KiB, snapshot 256 KiB,
  root/targets 512 KiB; assinaturas até 16. Expiração e tetos por papel seguem
  o contrato; o adaptador captura horário inicial e verifica retrocesso frente
  ao estado local de teste. A biblioteca também consulta relógio de sistema;
  uniformidade do relógio em todos os componentes permanece gate de integração.
- `custom.gf` exige o conjunto de campos da Etapa 0, tupla numérica de quatro
  componentes, sequência crescente, origem em formato canônico, perfil local,
  confirmação obrigatória e repositório nominal fixo. Mesmo número com outro
  hash é conflito; versão/hash já instalada resulta em no-op sem instalação.
- O contêiner externo e o portátil possuem hashes próprios, autenticados nos
  metadados. O externo contém o portátil designado e os três recibos previstos;
  recibo não confere autoridade criptográfica. Nada permite URL/linha de comando.
- Limites de ZIP **menores que os de produção**: 64 KiB por ZIP, expansão total
  1 MiB, até 8 entradas no contêiner e 16 no portátil. Somente ZIP simples,
  sem multi-disco/ZIP64, extras, comentários, criptografia ou data descriptors;
  métodos store/deflate, cabeçalhos local/central coerentes, CRC32 verificado
  independentemente, sem regiões sobrepostas ou prefixos/trailers.
- Nome ASCII portável, sem traversal/ADS/UNC/dispositivo, nomes reservados,
  case collision, links e tipos especiais. Toda leitura/expansão é limitada e
  cancelável; orçamento do validador de dois minutos. Nenhuma extração em disco.
- O portátil contém apenas fixture-marker.txt, LICENSE.fixture.txt e
  SHA256SUMS.txt. Conteúdo e cobertura de hashes são fechados. **Não contém
  GerenciadorFinal.exe**, runtime real ou DLL; não substituir este marcador por
  aceitação de payload real para contornar os gates.

O teste RFC8032 §7.1 Test 1 valida o primitivo Ed25519 independentemente dos
metadados gerados. Seeds públicas de teste e chaves operacionais são conjuntos
completamente separados. O fingerprint de teste não é parâmetro de manifesto.

## Fronteiras ainda não implementadas

Persistência/durabilidade do cache e pisos, filesystem NTFS por handle/file ID,
TOCTOU de arquivos reais, seletor/diário, IPC por identidade do kernel,
fechamento de instâncias, bootstrapping de produção, assinatura Authenticode,
AVG, inspeção de PE/runtime/licenças reais, integração/UI e transporte privado
automatizado **não foram implementados ou homologados**. Nenhuma dessas
capacidades é inferida dos 66 testes. Nenhuma Etapa 2 iniciada.

Referências primárias: [TUF 1.0.36](https://theupdateframework.github.io/specification/v1.0.36/),
[cliente .NET fixado](https://github.com/mitchdenny/sigstore-dotnet/tree/c0f5ff9747aa9b00c45a964dca275be4066cd489/src/Tuf),
[RFC8032](https://www.rfc-editor.org/rfc/rfc8032.html#section-7.1).
