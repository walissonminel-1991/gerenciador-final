# Autoatualizador GF — Etapa 9: fluxo experimental integrado

28/10/2026, America/Sao_Paulo. **Etapa 9 do autoatualizador concluída no
protótipo isolado. Distinta da Etapa 9 de avaliação física, que permanece
parcial e intacta. Zero gate operacional fechado; oito bloqueados. Etapa 10 NÃO INICIADA.**
PR #65 aberta/Draft, branch work-integracao-final-20261027, entrada `eccdd16ddcfa247fd2a72df82bcd2a243ff49743`.
Contrato gf-self-update/1 preservado; sem integração ao executável principal.

## Fluxo demonstrado

[Protótipo integrado](../prototipos/autoatualizador-seguro/stage9/README.md):
reusa sem alterar os componentes de estado/diário/seletor/NTFS da Etapa 2 e o
patch TUF/ownership da Etapa 8. Novas fontes `.cs.txt`, Compile explícito,
SDK 10.0.402, BC 2.7.0 com lock/auditoria e saídas geradas externas.

1. Admissão de uma operação sob o lease NTFS da instalação de fixture; grava
   registro append-only de pendência antes dos efeitos seguintes.
2. UTC imutável por tentativa, cliente único, uma chamada GetTarget com cadeia
   TUF assinada/reancorada na raiz pública de teste preexistente. Cache persistido,
   rotação contígua de raiz, versões/seq e antirreplay são confrontados com o estado.
   Timestamp inalterado termina sem alvo, preparação ou nova transação.
3. Autoriza somente ZIP **inerte**, vinculado por assinatura a hash/tamanho,
   versão/seq, manifesto e entrada; confere conjunto de três textos/CRC/hashes.
   Descritor interno `gf-integrated-fixture/9`, nunca manifesto GF operacional.
4. Persiste cache/pisos de confiança sem aprovar release; prepara pasta nova,
   arquivos CreateNew/flush e identidade/hash por handle. Retém três entradas e
   o predecessor até encerrar a operação.
5. Usa o diário transacional original: intenção/piso de release antes do seletor,
   seleção, confirmação **sintética** ligada a tx/nonce/hash e registro de conclusão.
   Não abre EXE/DLL, GUI, worker ou aplicativo GF.
6. Após processo do próprio ensaio terminar em barreira comprovada, outro
   coordenador lê os diários/identidades do disco, recupera exclusivamente o
   predecessor ou reconhece confirmação já concluída. Outro processo inspeciona
   o resultado, sem executar pacote.

O novo journal `gf-operation-fixture/9` tem admitted/authorized/prepared/settled,
com recovering para reconciliação explícita, checksum/cadeia, limites e criação
sem overwrite. Estado parcial/corrompido/pendente bloqueia nova tentativa.
Restauração de apenas um dos diários é detectada pela vinculação do digest;
restauração **integral** continua sem testemunha independente, não foi resolvida.

O owner retém sessão/lease/pins antes de chamar a factory e compartilha prazo
com o fluxo. Guarda da Etapa 2 recebe o mesmo T0 para iniciar e depois confere
parede/monotônico/piso e cancelamento antes da seleção. Resultado desconhecido
não autoriza seleção, limpeza ou retry. Depois de observar o término original,
liberar recursos de runtime **não apaga nem resolve** o journal pendente.
As fixtures inconsistentes ficam para descarte do runner; não existe cleanup
de instalação pendente neste protótipo. Diretórios auxiliares do owner só são
descartados após o fim comprovado do seu trabalho.

## Execuções e falhas preservadas

| Fonte / execução | Evidência efetiva |
| --- | --- |
| `b2932f0b` / [run 40810376313](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40810376313) | Build falhou CS0029 antes de testes: `_` era parâmetro CancellationToken, não discard. Zero teste executado; diagnóstico preservado |
| `b7844566` / [run 40810646898](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40810646898) | **22/22**, integração TUF/estado/transação/recuperação e negativos |
| `32e8d906` / [run 40811610734](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40811610734) | **1/1**, só novo cancelamento antes de admissão; revisão identificou caminho de descarte sem operação registrada, corrigido sem repetir os 22 |

São **23 casos distintos em evidência composta (22 + 1)**, não bateria completa
de 23 no SHA final. O ajuste final só trata encerramento sem operação registrada,
seu teste e filtro; caminhos com operação, TUF/journals/recuperação permanecem
iguais aos 22 aprovados. Nenhuma execução anterior das Etapas 1–8 repetida.
Cada run usa tentativa 1; sem reexecução automática, upload ou artefato.
Windows descartável, runtime .NET 10.0.14; avaliação Compile GF: 142 fontes,
zero protótipo, sem build/execução do produto. Dez fontes reutilizadas conferidas
idênticas após normalizar EOL; patch experimental TUF original permanece intacto.

Os seis pontos com recuperação supervisionada observada foram op-admitted,
cache-checkpointed, intent-persisted, selector-replaced, selected-persisted e
confirmed-persisted. Piso aprovado permanece 1 antes da intenção ou 2 depois;
seleção fica no predecessor 1, exceto confirmação já concluída em 2. Foram
recusados registro de estado parcial, journal truncado, prova de outro escopo,
replay/versão de papel inferior, cache sem intermediária, pacote misturado e
identidade divergente. Escrita de texto deliberadamente atrasada após cancelar
a espera foi observada: piso de release continua 1 e nova tentativa é bloqueada.
Cancelamento após intenção mantém piso 2 e seletor anterior, com journal pendente.
Isso é evidência de processo/arquivo/controlador de teste, **não energia real**.

## Gates e limites de aceitação

Todos G1–G8 continuam **blocked** na [matriz](evidencias/autoatualizador-etapa9-gates.json).
As novas subprovas não homologam produto, storage ou a biblioteca experimental.

- G1: nenhum teste de energia/dispositivo travado ou testemunha externa de frescor.
  Os dois diários pertencem ao mesmo domínio restaurável; não repetir o contraexemplo
  antigo nem declarar antirreplay integral por hash/encadeamento.
- G2: falta aceite de manutenção/revisão independente e conformance interoperável.
  Testou-se rotação de **raiz**; mudança das chaves dos demais papéis/reset de
  versões por época não foi executada. O checkpoint de cache ocorre após validar
  o alvo inteiro: persistência normativa de cada raiz/papel intermediário durante
  falhas não está qualificada. Não promover o patch a biblioteca operacional.
- G3/G4: ZIP de três textos não é pacote GF autenticado/PE/runtime/licenças GF.
  Beta-unsigned permanece permitido, sem exigência nova de certificado.
- G5/G6: objetos Process/saída de barreira e FixtureReadiness são controle do
  harness, não IPC autenticado entre contextos padrão/sessões, prontidão ou ciclo
  GF real. Falta qualificação de todos os participantes/carga tardia/inserção.
- G7: nenhuma chave operacional, cerimônia/custódia ou bootstrap instalado.
- G8: recuperação só com processo criado pelo harness, término observado e ponto
  concluído da whitelist. Não deduz término de driver por PID/ausência de lock.
  Crash durante a nova recuperação, falha de disposal, hang/Flush/ReplaceFile ou
  queda física não executados; casos desconhecidos/inconsistentes ficam bloqueados.

## Continuidade e preservação

[Windows](evidencias/autoatualizador-etapa9-windows.json) ·
[Admissão dirigida](evidencias/autoatualizador-etapa9-admissao.json) ·
[Build inicial](evidencias/autoatualizador-etapa9-build-inicial.json) ·
[Rodadas](evidencias/autoatualizador-etapa9-rodadas.json) ·
[Recibo](evidencias/autoatualizador-etapa9.json) ·
[Checkpoint](WORK-AUTOATUALIZADOR-CHECKPOINT.md).

BETA v5.13.11.66, fonte 6df95fdd776644cd4b248b5cea61108974ca9ce9,
run privada 40716548530/tentativa 1/artefato 12828262039 e ZIP SHA-256
8e11f2dda5b7e2df2f85c61b57cc6a31a9596628523d2be7ad155dfdd5dda7f2
preservados, sem download/rebuild/conversão em alvo gerenciado. Canal privado,
workflows existentes, acessos/retenção, avaliação física parcial e **27/42**
intactos; mesmos IDs/done. AVG/Windows, consentimento, remoto bloqueado/flags no,
drivers inativos, VLC só inventário e COM consultivo preservados. Zero físico,
manutenção PC FORTE, credenciais/permissões, infraestrutura/Worker/D1/DNS,
integração GF, merge/Release/deploy/publicação. **Encerrar antes da Etapa 10.**
