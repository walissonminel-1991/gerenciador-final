# Autoatualizador GF — Etapa 5: decisão dos oito gates operacionais

28/10/2026, America/Fortaleza. **Etapa 5 encerrada com evidências isoladas e
bloqueios explícitos. Zero dos oito gates operacionais foi fechado integralmente.
Não habilitar aplicação no GF; Etapa 6 NÃO INICIADA.**

PR #65 aberta/Draft, branch work-integracao-final-20261027. Entrada
`b10217552d372a9db55210fe71901b90abc3f159`, árvore
`2fb889b80570694414bb80a0fb5243ea33b7f657`. Checkpoint, contrato gf-self-update/1
e evidências 0–4 lidos; hashes/blobs dos insumos registrados no recibo.
Código dos ensaios: **64743bd45a83daa9bfba56ac06194c2c0b59d6da**.
Consulta adicional G5: **6712f3da6bda1c06fd693b625383cb7b23706d22**;
fontes dos dez ensaios iguais às do primeiro SHA. Fechamento posterior documental.

| Execução | Resultado e alcance |
| --- | --- |
| [Windows 40774544098](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40774544098) | **10/10**, número 1/tentativa 1/success; job 123120256513; SDK 10.0.402/runtime 10.0.14/Windows 20348 |
| [Consulta G5 40775734566](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40775734566) | success, número 2/tentativa 1; job 123124422982; consulta read-only em outro runner descartável. Suíte completa skipped, não repetida |
| Linux | **1 aprovado, 3 bloqueados, zero falhas finais**, quatro casos comuns. AF_UNIX negado, erro 13; sem fallback por mock |
| Isolamento | 142 itens Compile GF/zero protótipo; saídas externas; zero produto compilado/executado, zero testes físicos |
| Distribuição | Zero artefatos em ambas as runs; sem BETA nova, upload/Release/merge/deploy ou mudança de canal |

São dez casos de ensaio distintos, não onze somando o Linux. A consulta de
capacidade e as 274 tentativas de escrita são observações/subcasos, não novos
pontos do roadmap ou handshakes aprovados. Compilações finais sem avisos/erros.
Nenhuma suíte completa das etapas anteriores foi repetida. A primeira tentativa
Linux classificou três erros de transporte como falhas; sua evidência é
preservada. O harness final distingue bloqueio ambiental de teste aprovado.

[Resultados Windows](evidencias/autoatualizador-etapa5-windows.json) ·
[Linux](evidencias/autoatualizador-etapa5-linux.json) ·
[Contexto G5](evidencias/autoatualizador-etapa5-contexto.json) ·
[Histórico](evidencias/autoatualizador-etapa5-rodadas.json) ·
[Matriz estruturada](evidencias/autoatualizador-etapa5-gates.json) ·
[Recibo](evidencias/autoatualizador-etapa5.json) ·
[Protocolo/fontes](../prototipos/autoatualizador-seguro/stage5/README.md).

## Decisão por gate

| Gate herdado | Solução comprovada ou bloqueio explícito | Menor evidência/ação necessária |
| --- | --- | --- |
| G1 — durabilidade e antirreplay após restauração integral | **BLOQUEADO.** Contraexemplo real de restauração do diário da Etapa 4 permanece válido. Não existe fonte de frescor autenticada fora do domínio restaurado no fluxo fornecido; flush/checksum não a criam. Nenhum corte de energia/erro de dispositivo foi executado. Ambiente Linux sem /dev/kvm/QEMU; sessão sem controle externo de energia e disco persistente | Definir e provar referência independente de frescor, ou revisar explicitamente a garantia pretendida; dispor de VM descartável com armazenamento persistente e injeção externa de falha. Não instalar infraestrutura, criar testemunha ou reduzir garantia por inferência |
| G2 — adoção segura do relógio TUF | **BLOQUEADO.** Tuf 1.1.1 original continua fixado; a mudança da Etapa 3 é experimental, sem revisão/manutenção/adoção operacional demonstradas. Nesta etapa não foi recompilada nem promovida ao produto | Revisão específica do diff/fluxo TUF, responsabilidade de suporte e decisão verificável de adoção de implementação com horário fixo por ciclo; fixtures anteriores não substituem isso |
| G3 — validação autenticada do pacote GF | **BLOQUEADO.** Não foram fornecidos âncora operacional e conjunto de metadados gf-self-update/1 autorizando pacote GF futuro exato. Sonda, receita de build e marcadores não são pacote GF autenticado; .66 não foi importada/reassinada | Disponibilizar em escopo autorizado pacote GF isolado e metadados autenticados pela âncora aprovada, para validar cadeia→contêiner→ZIP→PE/perfil/runtime/licenças/segredos→estado, sem publicar ou converter .66 |
| G4 — política PE/publicador/revogação quando exigidos | **BLOQUEADO para classificação operacional de GF.** Evidências WinVerifyTrust da Etapa 4 não atestam GF. Beta-unsigned permanece permitido; publicador obrigatório, catálogo/revogação online não são exigências novas impostas à primeira BETA | Classificar o PE de pacote GF autenticado conforme modo declarado nos metadados autenticados. Só se outro modo Authenticode obrigatório for autorizado, incorporar e provar política de publicador/revogação/timestamp; não comprar/provisionar certificado por inferência |
| G5 — IPC sem elevação e entre sessões/usuários | **BLOQUEADO.** Ensaio funcional ocorreu elevado e foi recusado operacionalmente. Consulta separada observou elevado=true, TokenElevationTypeDefault=1, GetTokenInformation(TokenLinkedToken) falhou com **1312**; nenhum token padrão vinculado ao token consultado. Positivo sem elevação e par entre sessões/usuários não executados | Fornecer contextos descartáveis padrão e pares de sessões/usuários já autorizados. Usar permissões existentes; não habilitar UAC, criar contas, alterar/duplicar tokens ou privilégios para fabricar aprovação. Consulta de capacidade não conta como handshake |
| G6 — ciclo de vida transacional GF | **BLOQUEADO, com subgate de retenção completo na sonda.** **274/274** arquivos/identidades retidos, 274 write-opens recusados, pins até saída real; liberação posterior permite write-open sem mudar ACL. READY confirma filho vivo; deadline impede confirmação e recupera antecessor mantendo piso 2. Ainda é sonda/marcador, sem launcher/helper/instâncias/workers GF reais. Criação de nomes novos é possível, embora revalidação recuse extra | Ensaiar os participantes GF e pacote autenticado em contexto suportado, incluindo carga tardia/inserção e recuperação, sem integrar componentes não homologados ao principal. Não inferir fechamento pelo sucesso da sonda |
| G7 — bootstrap/custódia | **BLOQUEADO.** Plano vigente continua com root/fingerprint/publicador e slots de custódia operacionais vazios; nenhuma cerimônia/backup/restauração/rotação real foi executada. Não gerar privadas no job para simular custódia | Custódia offline e meios definidos, quórum/recuperação demonstrados e fingerprint previamente autenticado por canal independente; bootstrap futuro manual aprovado em pasta nova. Sem tokens GitHub no EXE |
| G8 — deadline global e I/O bloqueante | **BLOQUEADO, com subprovas reais.** Prazo padrão de 120 s expirou em **120,005 s** contra read Windows pendente e cancelou a tarefa; fases compartilham prazo. Porém WaitAsync cancelado deixou read sem token pendente, que completou depois. Sem pipeline GF/TUF/ZIP/PE/commit completo nem falha de armazenamento real, não há prova de término de todo I/O/ausência de efeitos tardios | Propagar deadline às fases reais, drenar/terminar I/O antes de soltar recursos e provar comportamento em falha de armazenamento. Espera cancelada, token ou read de pipe não são garantia de abortar driver/Flush/rename síncrono |

**Nenhuma garantia do contrato foi relaxada.** Não foi criada uma dependência de
rede para o transporte offline, nem removido um gate por não poder ensaiá-lo.
Os limites de comprometimento do próprio usuário/administrador do contrato
continuam distintos da durabilidade a demonstrar. Contraprovas anteriores não
foram reexecutadas ou substituídas por modelos novos nesta sessão.

## Preservação e parada

BETA **v5.13.11.66**, fonte 6df95fdd, run privada 40716548530/tentativa 1/
artefato 12828262039 e ZIP SHA-256
`8e11f2dda5b7e2df2f85c61b57cc6a31a9596628523d2be7ad155dfdd5dda7f2`
intactos, sem download/recompilação/requalificação. Canal privado, retenção,
acessos, workflows de produto, main, históricos e demais PRs preservados.
Branch de evidência work-autoatualizador-etapa5-validacao-20261028 mantida.

AVG/proteções, consentimento e dados preservados; nenhum novo veredicto AVG ou
homologação física. VLC exclusivamente inventário/zero ações/consultas/testes
específicos; WinGet COM opcional/consultivo; drivers instalar/remover/reverter
desativados. Remoto bloqueado, PILOT_ENABLED=no e GITHUB_EXPORT_ENABLED=no.
Zero Worker/D1/DNS/infraestrutura, ACL/token/UAC/privilégios/trust store alterados,
chaves operacionais, bootstrap instalado ou instalação silenciosa. Roadmap
**27/42**, mesmos 42 IDs/done; avaliação física anterior parcial e intacta.

O resultado desta etapa é a evidência e a matriz de bloqueios, não permissão de
atualizar. Próxima ação é resolver os pré-requisitos específicos registrados,
em sessão autorizada. **Encerrar após salvar/conferir no GitHub; Etapa 6 não
iniciada, sem integração no executável principal ou nova publicação.**
