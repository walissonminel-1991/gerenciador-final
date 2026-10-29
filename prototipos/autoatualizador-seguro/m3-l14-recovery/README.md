# L14 — interrupções de processo e recuperação, somente texto inerte

Objetivo: testar em Windows/NTFS da CI uma mini-transação de laboratório com dois arquivos fixos old.fixture.txt e new.fixture.txt, um único active.fixture.json e recibos intent.fixture.json / terminal.fixture.json. Não é um autoatualizador, não é GF, nem um layout de instalação real. Só opera em subdiretórios diretos gf-u2-l14-UUID de TEMP, com marcador dedicado; sem chamadas HTTP, ferramentas externas, UAC, elevação, PowerShell ou lançamento de qualquer versão do GF.

- Cada cutpoint mantém um processo filho real com uma trava de arquivo exclusiva aberta, emite CUT:<ponto>, e o pai encerra o processo com Kill(entireProcessTree:true). Um SEGUNDO processo distinto reabre o diretório e reconcilia recibos persistidos. Os cortes: antes do staging, após stage flush, após intent flush, após selector scratch flush, após File.Replace(selector), após terminal flush.
- Se o terminal não existe, a recuperação preserva ou restaura seletor inerte old. Se existe e é consistente com os textos estáticos e bytes do seletor, retorna apenas NewInertObservedBlocked. Se dados ou recibos divergem, retorna UnknownBlocked; sem permissão para executar, nem autorizar update.
- O File.Replace exercita a troca de UM ÚNICO arquivo seletor, não a ativação atômica de múltiplos arquivos. Flush(true) é uma solicitação de sincronização, não demonstra resistência a perda de alimentação.
- Duas versões de textos são mantidas lado a lado; a antiga nunca é apagada. Os testes verificam também o bloqueio da trava quando um processo concorrente tenta recuperar, timeout de espera sem terminar processo, idempotência e replay de recibos de outra instalação simulada.
- Este modelo NÃO é um contrato de IPC real. A identidade de processos não está autenticada, o formato de recibo não é assinado, e o diretório TEMP da CI não simula ACLs de Program Files. Para token limitado, junction, link, ACL e TOCTOU existem limitações documentadas no L13; L14 não fecha os seus gates.
- Atenção: Recover pode retornar OLD após troca do seletor mas antes do terminal (rollback de fixture); após terminal pode OBSERVAR NEW, mas não realiza READY, instalação, confirmação, launch ou saúde real. Adulteração da própria versão OLD causa UNKNOWN e não repara silenciosamente os dados.
- O runner de testes é apenas um executável de laboratório gerado pela CI: não é o executável GF e não é distribuído a usuários.

Estado: protótipo inerte, CI Windows descartável, G1–G8 BLOCKED (0/8); main/PR #65/BETA .66/cloud/telemetria intocados; sem WORK.
