# L17 — contraexemplos do WORK: três ensaios adversariais, zero autorização

Continuação SOMENTE INERTE do Lote 16, preservando Stage11/Stage3 TUF assinado com chaves de TESTE e WindowsFileGuard.

Três cenários novos, e regressão dos 18 cenários L16:
1. Um processo recuperador mantém handle NTFS do seletor após observá-lo; o segundo processo tenta escrita/renomeação e recebe recusa. Outro processo substitui, por bytes iguais, o arquivo temporário fechado entre pré-check e File.Replace; a identidade divergente após a syscall causa UNKNOWN persistido. A janela de substituição continua fisicamente existente — o objetivo é falhar fechado, não declarar atomicidade ou impedir toda corrida.
2. O dono é morto após troca do seletor; um escritor local cria SOMENTE o recibo terminal calculável. Como não há testemunha independente, o recuperador retorna UnknownBlocked, inclusive quando o terminal foi escrito de verdade. Isso deliberadamente reduz disponibilidade e corrige qualquer inferência de conclusão confiável baseada em assinatura inexistente.
3. A espera do supervisor expira enquanto o proprietário ainda mantém lease; recuperadores observam BusyBlocked. O supervisor grava um marcador de incerteza local com CreateNew, WriteThrough e Flush(true); o dono termina tardiamente e escreve o terminal; recuperações posteriores retornam UnknownBlocked sem apagar o marcador.

Fronteira real de confiança: TUF test-only autentica EXCLUSIVAMENTE os bytes da fixture; os handles NTFS impedem mudanças comuns enquanto abertos; o próprio usuário é permitido pela ACL, portanto **qualquer marcador, arquivo, checksum ou segredo armazenado nesse mesmo domínio é falsificável ou apagável por ele**. O protocolo de produção exigiria autoridade distinta ou aceitação explícita de risco e investigação adicional. Não há READY, GF real, instalação, autorização operacional, witness externo, proteção universal contra TOCTOU nem resistência a power loss.

As correções são aplicadas somente ao protótipo m3-l16-signed-stage/L16Boundary.cs.txt e às expectativas de regressão de L16Tests.cs.txt; nenhum arquivo de código GF principal é compilado com essas fontes. G1–G8 continuam BLOCKED (0/8). PR #65 e BETA .66 intocadas, sem WORK nesta rodada.
