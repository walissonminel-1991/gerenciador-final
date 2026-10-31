# GERENCIADOR FINAL — D2.4.3 (somente observação, TUF/NTFS)

O coordenador C# da D2.4.2 mantém o papel de entrada de laboratório. O modo **`--d243 <fixture> <trust>`** cria a sessão e inicia um novo filho Python embutido como recurso no próprio projeto C#; esse filho **reutiliza** a função `observe` do laboratório D1.1, que é baseada em python-tuf e no parser D0 real. Nenhuma segunda biblioteca TUF ou implementação de downloader foi criada. O modo D2.4.3 **NÃO chama Stage**, não instala, não limpa nem promove nada: a observação autenticada tem status `inert_trust_observed_blocked`, e a história duvidosa resulta em `unknown_hold`.

O estado do TUF é persistido no mesmo diretório `gf-d243-trust-*` entre processos Windows independentes. Um mutex local derivado do caminho serializa tentativas *cooperativas*; concorrentes, estado parcial, lock abandonado e falhas de filho entram em HOLD, sem reparo automático. Isso **não** é mutex/ACL protegido contra o mesmo usuário e **não** fornece durabilidade em crash. O bootstrap de teste é lido e pinado no snapshot usado pelo cliente; chaves de teste são públicas.

A matriz exige: v1 → v2 autenticadas (papéis 1→2), replay de v1 recusado com histórico v2 intacto, e o **contraexemplo real do D1.2** reproduzido: restaurar cópia integral do histórico v1 permite aceitar v1 novamente. Logo H1 permanece NÃO comprovado e a CI exige os dois resultados distintos.

O modo `--d243-probe` é uma observação de arquivo INERTE sob `RUNNER_TEMP`. Verifica caminhos e ancestrais para reparse points, abre handle, recusa link count maior que um, compara file ID e hash inicial/final. Testes Windows reais exercitam junction ancestral, link simbólico de arquivo, hardlink, substituição concorrente do caminho enquanto o handle está aberto e alteração de conteúdo sem mudar o ID. Todos são **observações sem qualquer capacidade de instalação, exclusão ou recuperação**; divergência vira HOLD preservando os arquivos. Essas verificações e janelas de corrida **não** provam uma política NTFS/TOCTOU resistente a um adversário do mesmo usuário. Nenhum acesso a diretórios pessoais ocorre.

É obrigatório preservar D2.4.1 e D2.4.2. O modo `--d24` continua bloqueado. Nenhum resultado da D2.4.3 abre D2.4.4 ou D3 sem autorização posterior.

**Invariantes:** Opção B, Fase 1 ABERTA, READY=false, G1–G8 BLOCKED 0/8, zero recuperações automáticas, HOLD = contenção. Sem main, PR #65 Draft, BETA .66, Cloudflare, release, merge, deploy, downloads executáveis ou instalações.
