# Etapa 8 — deltas isolados G2/G8

Protótipo não instalável, sem integração ao GF. Somente fixtures públicas e
texto inerte em diretórios temporários `gf-u8-inert-*`. Nunca .66, PC FORTE,
canal privado, chaves operacionais, contas/UAC/ACL, IPC ou publicação.

SDK 10.0.402; BouncyCastle.Cryptography 2.7.0 com lock e auditoria preservados.
Fonte Tuf 1.1.1 no SHA já fixado, com patch de três arquivos, compilada apenas
no assembly de ensaio. Original NuGet e protótipos anteriores intactos. Arquivos
C# versionados são `.cs.txt`; fontes geradas e saídas ficam fora do repositório.

`python prototipos/autoatualizador-seguro/stage8/run_tests.py --report <fora-do-repo>/resultado.json`

UTC escalar readonly por cliente/tentativa, uso único concorrente e resultado
Unchanged que termina o ciclo e impede acesso ao alvo. Facade chama GetTarget
uma vez, sem refresh prévio; cache em memória é reancorado e limitado.

Coordenador reserva ownership antes de iniciar factory, observa tarefa original,
retém recursos/lease, compartilha prazo monotônico padrão de 120 s e bloqueia
continuação/liberação quando resultado é desconhecido. Teste curto do orçamento
usa 1 s; não repete a prova antiga de 120 s. Factory síncrona e tarefa que ignora
token são controladas por sinais, distinguindo espera cancelada e término real.
Há escrita de texto real deliberadamente atrasada, sem alegar falha de dispositivo.

Reconciliação é exclusivamente da fixture em memória. Não há barreira durável,
diário do produto, garantia de abort de driver/Flush/ReplaceFile, ciclo GF,
pacote GF autenticado, conformance/interoperabilidade TUF ou custódia operacional.
G1/G3–G7 permanecem bloqueados; resultados destes deltas não fecham G2/G8.

Workflow restrito à branch de validação da Etapa 8, readonly, sem upload/retry.
Falhas/limites devem permanecer nas evidências. Encerrar antes da Etapa 9.
