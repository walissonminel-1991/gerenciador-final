# Etapa 9 — fluxo integrado experimental, sem produto

Distinta da Etapa 9 física do GF. Fixture-only, não instalável; não interpreta
manifesto operacional. Descritor interno `gf-integrated-fixture/9`, ZIP de três
textos, versões `0.0.0.N`; nunca BETA .66 como alvo. Raiz pública de testes
preexistente e signer Python independente; zero chave operacional criada.

Reusa sem alterar a Etapa 2 (estado/diário/seletor, lease e NTFS) e a Etapa 8
(UTC imutável, cliente único, timestamp inalterado e ownership/prazo). O novo
adaptador faz uma chamada TUF, vincula ZIP/manifesto/entrada/seq/versão/cache e
BundleRef/identidade ao diário. Guarda de commit combina T0 com parede,
monotônico e piso persistido, sem alterar relógio do Windows.

Novo side journal `gf-operation-fixture/9`: admitted → authorized → prepared →
settled; recuperação explícita passa por recovering. Registros append-only
limitados, checksum/cadeia, CreateNew/WriteThrough/Flush(true)/promoção sem
overwrite. Pendência, corrupção ou divergência entre os diários bloqueiam
tentativas. Journal não é um contador externo nem prova de durabilidade física.

Um owner da Etapa 8 retém a sessão/lease real da Etapa 2 e os pins. Cancelamento
da espera não fecha o trabalho original nem libera recursos. Depois do término
original, a liberação de recursos de runtime não apaga/resolve a pendência
persistente. Não há método de limpar a instalação experimental pendente.

Recuperação supervisionada exige o objeto Process de filho criado pelo harness,
término observado e barreira concluída específica/operação/instalação. Confere
cache/identidades/estado antes de recuperar o predecessor exato ou reconhecer
confirmação já concluída. Não descobre término de driver por PID, não autentica
IPC de produto e não transforma um recibo local em prova de frescor. Recusa
registros parciais e fronteiras não comprovadas. Após recuperação outro processo
inspeciona o estado sem executar qualquer pacote.

`python prototipos/autoatualizador-seguro/stage9/run_tests.py --report <fora-do-repo>/resultado.json`

SDK 10.0.402; BC 2.7.0/lock/auditoria anteriores. Fontes `.cs.txt`, Compile
explícito, source TUF/saídas externos. Suítes antigas não são executadas.
Windows NTFS necessário; Linux é blocked, sem simular identidade Windows.
Diretórios inconsistentes ficam preservados para descarte do runner, sem cleanup
autorizado por resultado desconhecido. Só filhos deste ensaio são interrompidos.

Limites: não cobre energia/dispositivo real, restauração de todo estado local,
crash durante recuperação desta nova composição, key epochs/reset de versões
dos papéis, pacote GF/PE/runtime, IPC padrão, prontidão real, custódia ou produto.
Todos os gates operacionais continuam bloqueados até evidência integral.
