# Etapa 5 — evidência real e bloqueios operacionais

Entrada b1021755, contrato `gf-self-update/1` intacto. Este projeto não é um
atualizador operacional: só compila o runner e a sonda WinForms inerte 0.0.0.1.
Não carrega/importa pacote GF, não compila/executa a BETA, não usa canal privado,
chaves operacionais, credenciais ou rede do aplicativo. Sem integração no produto.

`python run_tests.py --report <json>` exige SDK 10.0.402 e restore locked.
Fontes `.cs.txt`, saídas em temporários externos, IsPackable/IsPublishable=false
no runner; projeto principal só avaliado para conferir 142 itens Compile/zero
protótipo. Workflow restrito à branch de ensaio, contents:read, sem uploads,
secrets, cron ou retry automático. A sonda publicada em temporário pelo SDK
não é versão/publicação GF. Dependências Tuf 1.1.1 e BC 2.7.0 intactas.
O experimento de relógio não é recompilado/adotado nesta etapa; nenhuma suíte
completa das Etapas 0–4 é repetida.

## Provas novas, sem promover subgates a aprovação de produto

`PinnedFixtureBundle` retém um WindowsFileGuard/identidade por arquivo da receita
inteira da sonda, com ancestrais NOFOLLOW/IDs/volume/ACL read-only. Hashes em
FileStream assíncrono com buffer 64 KiB; só depois a inspeção PE/runtime/licenças
existente autoriza a capability de sonda. Todos os handles ficam abertos até
saída real do filho e encerramento do escopo. Testa-se abertura para escrita de
cada arquivo, além de delete/rename de DLL/licença antes não cobertos pelos
oito pins críticos. A abertura de controle após liberar handles distingue
compartilhamento de uma permissão de escrita ausente. Nenhuma ACL é alterada.

A enumeração própria é por nível, limitada a 4.096 nós, com nomes previstos e
guardas NTFS antes de descer. Não seguir diretório/junction desconhecido para
descobrir seu conteúdo. Essa enumeração não modifica o verificador estático
herdado. **Não há loader allow-list nem exclusão de criação de novos nomes**:
um arquivo extra criado pelo harness é recusado pela revalidação de conjunto.
Não se demonstra corrida de inserção durante cada carga tardia ou propriedade
de todos os participantes GF. Gate operacional de ciclo/TOCTOU continua aberto.

O ciclo ensaiado usa marcador de versão inerte ligado ao digest da receita,
diário da Etapa 2 e IPC da Etapa 4. Confirma enquanto o filho está vivo, com
handles retidos; expiração durante espera real sem READY impede confirmação e
recupera exatamente o antecessor, sem baixar piso. Não corresponde a pacote GF
assinado por TUF, launcher/helper GF ou seus workers/instâncias/manutenção.
Classificador operacional recusa elevação antes de spawn; harness de sonda
pode observar mecanismos na elevação herdada, sem autorizá-la para produto.

`StageDeadline` usa um prazo monotônico e um token compartilhado. Duas leituras
de pipe reais usam o mesmo prazo, cancelamento do usuário alcança leitura
assíncrona pendente e um arquivo real é lido/hashado. Windows inclui um ensaio
com o **prazo padrão real de 120 segundos**, sem relógio simulado. Prazos curtos
de fixture exercitam outras janelas; não são substitutos desse ensaio padrão.

Também se demonstra o limite real de WaitAsync: cancelar a espera não encerra
uma leitura de pipe iniciada sem o token. A leitura continua pendente, recebe
um byte depois do prazo e completa; Check continua recusando o resultado.
Caller retém handles/tarefa até terminar e observa a conclusão. Não abandonar
tarefas e liberar pins enquanto I/O ainda usa os arquivos. Cancelamento é
cooperativo; não se afirma cancelamento forçado de driver/dispositivo, travamento
de armazenamento, Flush/rename síncronos ou deadline global TUF→ZIP→PE→commit.
Por isso o gate operacional de prazo/I/O permanece bloqueado.

Linux neste ambiente nega criação de AF_UNIX ao processo .NET. Esses casos
recebem estado **blocked**, nunca passed, e não são substituídos por MemoryStream
ou mocks. A tentativa inicial classificada como falha de transporte é preservada
no histórico de resultados. Windows só conta casos realmente executados.

## Oito gates herdados: critérios que estes testes não substituem

| Gate | Evidência exigida para fechamento operacional |
| --- | --- |
| G1 — armazenamento e frescor após restauração integral | Referência de frescor autenticada fora do domínio restaurado, ou limitação de confiança expressamente aceita; ensaio de falha de energia/armazenamento com disco persistente e controle externo. Não reexecutar modelos de prefixos como se fossem corte de energia |
| G2 — relógio TUF | Revisão, responsabilidade de manutenção e adoção formal de implementação suportada com horário fixo por ciclo; o patch experimental não vira dependência de produto por passar fixtures |
| G3 — pacote GF autenticado | Âncora operacional e metadados gf-self-update/1 válidos autorizando bytes GF exatos, contêiner/portátil, recursos/perfil/versão/runtime/licenças/segredos e vínculo ao estado. Não assinar retrospectivamente nem reconstruir .66 por rotina |
| G4 — política PE/publicador | Classificação Authenticode do pacote GF autenticado no perfil assinado; se futuro modo obrigatório, publicador previamente incorporado e política de revogação/timestamp testada. Beta-unsigned continua permitido pelo contrato e não exige comprar/provisionar certificado por inferência |
| G5 — contexto IPC | Handshakes reais sem elevação e entre sessões/usuários distintos em contextos descartáveis autorizados. Token elevado ou enumeração WTS não são positivos; não criar conta, alterar token, ACL, UAC ou privilégio para fabricar aprovação |
| G6 — ciclo GF completo | Pacote autenticado e identidade/pins cobrindo o uso real, launcher/helper/instâncias/workers/manutenção e transação GF em ambiente descartável; sonda/marcador não homologam produto |
| G7 — bootstrap/custódia | Custódia, meios offline, quórum e recuperação/rotação demonstrados; fingerprint inicial entregue por registro autenticado independente. Slots vazios ou seeds públicas de fixture não são cerimônia |
| G8 — prazo global e I/O bloqueante | Deadline compartilhado em todas as fases reais, ausência de efeitos tardios e término/drenagem de I/O antes de liberar recursos, inclusive falhas de armazenamento; timeout de uma espera ou pipe cancelável não prova isso |

No ambiente Linux disponível não há /dev/kvm ou executável QEMU; não há controle
de energia/disco persistente exposto por esta sessão. Nenhum hypervisor, conta,
serviço, tarefa ou configuração Windows é instalado. Root/fingerprint/publicador
operacionais e custodiantes continuam ausentes do plano vigente da Etapa 4.
Nenhum novo serviço testemunha, chave, endpoint ou infraestrutura é criado.
Registrar no relatório cada bloqueio e a menor evidência que falta, sem
converter essa lista em autorização para outra etapa.

AVG/proteções, remoto bloqueado/flags no, privacidade, drivers inativos, VLC só
inventário e COM consultivo preservados. BETA .66/canal privado/roadmap 27/42
intactos. Não repetir testes físicos, publicar, integrar ou iniciar Etapa 6.

Fontes primárias: [cancelamento cooperativo .NET](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads),
[Task.WaitAsync](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.waitasync?view=net-10.0),
[FileStream.ReadAsync](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.readasync?view=net-10.0)
e contrato/protocolos/evidências fixados das Etapas 0–4. As conclusões sobre os
limites do ambiente vêm dos resultados observados, não de alegação dessas APIs.
