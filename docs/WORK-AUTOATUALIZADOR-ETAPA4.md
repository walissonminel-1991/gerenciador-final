# Autoatualizador GF — Etapa 4: ensaios e limites dos gates

28/10/2026, America/Fortaleza. **Etapa 4 concluída no escopo isolado;
aplicação operacional bloqueada pelos gates abaixo. Etapa 5 NÃO INICIADA.**
PR #65 aberta/Draft, work-integracao-final-20261027. Entrada
`9dd892259af9297172818d9c3112f3df0721b328`, árvore
`e3776aeee15ac145612b546c0376e4cb8a99278a`.
Checkpoint, contrato gf-self-update/1, protocolo e evidências da Etapa 3 lidos.
Contrato, bibliotecas, receita do produto e provas anteriores intactos.

Código validado: **dc7ee0a1aa65be20c660c1e0e7527d4e1c05cec4**, árvore
`85d06f55a6f2b6781b508a0f163ba0b5ebfc16a1`. Fechamento posterior só documental.

| Execução | Resultado |
| --- | --- |
| [Windows 40769700594](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40769700594) | **46/46**, número 2/tentativa 1/success; job 123103247820; SDK 10.0.402, runner .NET 10.0.14/Windows 20348 |
| Linux | **38/38**, SDK 10.0.402/.NET 10.0.13; subconjunto dos 46, não somar plataformas |
| Isolamento | 142 itens Compile GF, zero protótipo, ambos OS; zero build/execução do GF; saídas externas |
| Publicação | Zero artefatos nas duas runs; sem upload, Release, merge, deploy ou nova BETA |

Windows: 27 casos .NET + 19 modelos Python. Linux: 19 .NET + os mesmos 19
modelos. Contraexemplo reproduzido significa **delimitação comprovada**, não
aprovação do gate. Builds finais sem avisos/erros. Não repetir suítes físicas
ou as antigas 66/61/47 verificações completas por rotina.

[Windows](evidencias/autoatualizador-etapa4-windows.json) ·
[Linux](evidencias/autoatualizador-etapa4-linux.json) ·
[Rodada anterior](evidencias/autoatualizador-etapa4-rodadas.json) ·
[Recibo](evidencias/autoatualizador-etapa4.json) ·
[Protocolo, decisões e fontes primárias](../prototipos/autoatualizador-seguro/stage4/README.md).

| Gate | Evidência obtida | Bloqueio e menor evidência futura necessária |
| --- | --- | --- |
| Durabilidade/antirreplay | Recuperação exata mantém piso 2. Restauração integral de diário/seletor volta a piso 1. Modelo recusa piso/digest/challenge/instalação divergentes | Nenhuma testemunha autenticada implementada; o modelo pressupõe evidência externa. Hash/flush/cópia local não distinguem restauração integral. Exige decidir frescor independente ou manter limitação; energia/disco real exige VM descartável com controle de falha e armazenamento persistente, sem PC FORTE |
| Relógio TUF | Mesmo experimento fixado/20 hashes da Etapa 3; horário congelado seguido de prazo monotônico >2 min e retrocesso >5 min bloqueia commit. Relógio+estado restaurados reproduzem limite local | Tuf 1.1.1 original intacto; adoção exige revisão independente, manutenção/suporte e decisão formal. Nenhum contato upstream ou troca de pacote |
| ZIP | Bundle real da sonda: **63.940.273 bytes**, 274 arquivos/**123.062.971 bytes** expandidos; preflight, hashes, CRC, CreateNew/NTFS, inspeção PE/runtime/licenças | Limites 256/768 MiB e 2.048 arquivos implementados, pontos representativos testados; extremos completos não executados. ZIP restrito sem descriptors/ZIP64/extras/diretórios explícitos. Orçamento único da extração não prova deadline global TUF/PE nem interrupção de I/O bloqueante |
| Pacote GF | Verificador aprovado intacto, modelos JSON fechados de dependências, deriva YAML/WinGet, self-contained e perfil local | Não foi criado/importado EXE/DLL GF nem executado verify() integral sobre pacote GF. Faltam recursos/perfil/segredos reais e vínculo TUF→ZIP→estado. Exige pacote futuro autenticado em escopo próprio; não reconstruir/importar .66 por rotina |
| Authenticode | WinVerifyTrust real: unsigned 800b0100, SDK assinado 00000000, cópia adulterada 80096010. Certificado/falha não permitem downgrade unsigned; zero Windows não autoriza publicador GF ausente | Cache-only/offline: revogação atual, catálogo, múltiplas assinaturas e publicador GF não aprovados. Se assinatura obrigatória for adotada, precisa política incorporada por bootstrap; SDK não comprova autoria GF e beta-unsigned não foi promovido |
| IPC/contexto | Token real elevado recusado antes de spawn operacional; WTS read-only enumerou quatro sessões | Quatro sessões enumeradas não são handshakes. Positivo sem elevação e pares entre sessões/usuários distintos **não executados**; precisam contextos descartáveis previamente autorizados, sem UAC/token/ACL alterados nesta sessão |
| Ciclo de vida | Sonda anterior sai antes da intenção; canal vinculado a ID/nonce do diário e digest de marcador/receita. READY confirma filho vivo; nonce errado/saída antecipada recuperam anterior e retêm piso; adiar não fecha/grava intenção | Harness funciona na elevação herdada enquanto classificador operacional a recusa. Marcador/sonda não são bundle GF. Faltam launcher/helper, instâncias/workers/manutenção/saúde GF reais; pipeline operacional não integrado |
| Bootstrap/custódia | Raiz pública de fixture: fingerprint fixo, 2 IDs Ed25519 distintos, papéis separados; TOFU/substituição/signer duplicado/reuso recusados e modo produção bloqueado | Plano só contém slots vazios. Falta cerimônia autorizada de custódia/backup/restauração/rotação/fingerprint independente e launcher/helper aprovados. Nenhuma raiz/chave operacional; primeiro bootstrap manual em pasta nova, sem converter .66 |

O ZIP da sonda tinha SHA-256
`fdf138d75d8bac135f95c1844d5bd92dcd486c4b010681ec3c953c22f755d560`.
Era temporário, inerte e eliminado com a sandbox; não há artefato/link de entrega.
Somente os arquivos críticos da capability ficam presos durante spawn/execução,
como na Etapa 3; não se prova retenção por handle dos 274 arquivos até cada carga
tardia. Modelos de custódia/antirreplay nunca retornam permissão de instalar.
Nenhum teste de energia, AVG ou homologação funcional GF foi executado.

Primeira run **40769018174**, SHA 4663f9fc, job 123100835609/tentativa 1:
**39/40**, U4_ZIP_FORMAT no bundle completo; quatro casos de ciclo de vida não
executados. Diagnóstico isolado reproduziu método 8/flags 6/versão 20/extra e
comment 0: hints de velocidade Deflate definidos em APPNOTE §4.4.4. Corrigido
apenas o leitor novo para permitir bits 1/2 nesse método, mantendo CRC/igualdade
local-central e recusa de criptografia/descriptors/extensões. Dois novos testes
cobrem aceitação Deflate e recusa de criptografia. Etapas 1–3 intactas.
Segunda run passou 46/46 incluindo os quatro casos antes bloqueados. Duas runs
distintas, ambas tentativa 1, zero retries automáticos; falha anterior preservada.
Hashes raw do verificador diferem entre OS por CRLF do checkout Windows;
blob Git idêntico, sem edição ou afrouxamento do verificador aprovado.

BETA **v5.13.11.66**, fonte 6df95fdd, run privada 40716548530/tentativa 1/
artefato 12828262039 e ZIP SHA-256
`8e11f2dda5b7e2df2f85c61b57cc6a31a9596628523d2be7ad155dfdd5dda7f2`
preservados sem download/rebuild/execução/requalificação. Canal privado,
retenção, acessos, workflows de produto, main, históricos e outras PRs intactos.
Workflow novo somente na branch work-autoatualizador-etapa4-validacao-20261028,
contents:read, sem secrets/uploads/cron/retries; branch de evidência mantida.

AVG/proteções, consentimento e dados intactos. VLC só inventário/zero ações ou
consultas/testes específicos; WinGet COM opcional/consultivo; instalação/remoção/
reversão de drivers desativadas. Remoto bloqueado; PILOT_ENABLED=no e
GITHUB_EXPORT_ENABLED=no. Zero Worker/D1/DNS, infraestrutura, certificados ou
chaves operacionais instalados, credenciais permanentes, ACL/token/privilégio
alterado ou instalação silenciosa. Roadmap **27/42**, mesmos 42 IDs/done;
avaliação física parcial preservada, zero repetição.

Continuidade atualizada por acréscimo de histórico. **Parar após salvar/conferir
a PR #65 Draft. Sem integração no executável principal, publicação ou Etapa 5.**
