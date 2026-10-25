# Desenvolvimento contínuo com AVG ativo — plano de trabalho

Atualizado: 2026-10-25. Referência histórica: v5.13.10.24; investigação atual no Lote 3, PR #56.
Problema observado: alerta comportamental AVG `IDP.Generic` seguido de quarentena ao restaurar da bandeja; em seguida um ZIP passou a falhar na extração com "Acesso negado". A coincidência temporal e a repetição das ocorrências justificam análise, mas **não provam** a causa do bloqueio do WinRAR nem que o EXE seja seguro.

## Estratégia para NÃO travar o desenvolvimento

1. **Editar código, executar testes e compilar no GitHub Actions:** múltiplas mudanças de código por rodada, sem necessidade de baixar cada versão para os PCs. A bateria de layout, MSIX e resíduos continua independente da máquina do usuário.
2. **Pacote auditável:** cada build Windows inclui `BUILD-INFO.txt` (versão, commit e execução) e `SHA256SUMS.txt` com hashes de todos os arquivos publicados. O pipeline verifica os hashes contra os arquivos gerados.
3. **Atestado de origem:** somente em contexto confiável da main pública o pipeline pode gerar atestado Sigstore da procedência do EXE; PRs não recebem permissão de atestação. Ele **não** é assinatura Authenticode e **não** significa aprovação pelo AVG.
4. **Menos pacotes intermediários:** usar branches/PRs e testes automatizados para iterar; publicar ZIPs para pessoas somente após qualidade e avaliação de segurança. Evitar renomear/ofuscar/empacotar agressivamente código para evitar detecção.
5. **Código com intenção transparente:** aplicativo inicia sem privilégios administrativos (`asInvoker`). Verificações consultivas e operações que alteram dados devem ter consentimento explícito; acesso a Registro, serviços, desinstaladores e comandos privilegiados somente quando necessário. Não interferir com antivírus, firewall ou políticas de segurança.
6. **Assinatura:** preparar adesão à SignPath Foundation após satisfazer critérios OSS (versão publicamente lançada, licença aprovada, código público verificável, política no README, papéis de revisão, MFA). Manter a identidade de assinatura estável ao longo das versões, se aprovado.

## Envio do caso ao AVG (sem exceções)

Formulário oficial de falso positivo:
https://www.avg.com/pt-br/submit-a-sample

Guia oficial em português:
https://support.avg.com/SupportArticleView?l=pt&supportType=home&urlName=avg-submit-file-url

1. Preservar registro de detecção **IDP.Generic**, módulo **Comportamento**, e ID do alerta. Não divulgar caminhos pessoais da máquina.
2. Enviar **a amostra detectada** pela Quarentena do AVG ou usar o formulário oficial. Conferir formato e limites atuais na interface oficial; a investigação do Lote 3 não enviou amostras.
3. Informar que o programa é um gerenciador de aplicativos/atualizações Windows, código aberto, compilado por GitHub Actions, com consulta de drivers/registro e fluxo de minimizar/restaurar. Incluir link ao código, commit e hash.
4. Pedir análise tanto do evento comportamental quanto de eventuais bloqueios na extração do arquivo. **Não afirmar falso positivo como comprovado**.
5. O FAQ oficial descreve inscrição no programa, revisão e possível aprovação por arquivo ou assinatura. A aprovação fica a critério do fornecedor e não foi obtida por esta missão.
6. Só retomar testes locais após revisão do incidente e validação de integridade, mantendo AVG habilitado. Uma confirmação de build no CI não substitui teste real no Windows.

## Como verificar o pacote sem executar o EXE

- Baixar apenas o artefato associado ao workflow verificado.
- Na distribuição, ler `BUILD-INFO.txt` e comparar o commit com o código/revisão do GitHub.
- No PowerShell, a partir da pasta extraída (somente leitura):

```powershell
Get-FileHash .\GerenciadorFinal.exe -Algorithm SHA256
Get-Content .\SHA256SUMS.txt | Select-String GerenciadorFinal.exe
```

- Os hashes precisam coincidir. Isto detecta alteração de arquivo, mas não assegura que o código original seja confiável ou livre de bugs.
- Não substituir arquivos em pastas antigas. A falha de extração deve ser registrada sem desabilitar a proteção. Se o AVG bloquear, preservar o evento e interromper o teste, sem adicionar exclusões.

## Portões de qualidade

- [ ] GitHub Actions build/manifesto e SHA256SUMS passam.
- [ ] Regressões Windows: layout, RAM, MSIX e resíduos passam.
- [ ] Origem e hash da amostra do AVG correspondem aos registros do GitHub.
- [ ] Pendente avaliação do alerta `IDP.Generic` e integridade do sistema.
- [ ] Avaliação de segurança antes de disponibilizar para usuários finais.
- [ ] Testes reais com AVG ativo e sem bloqueios, especialmente minimizar/restaurar, desinstalação e diagnósticos.

## Limitações

- Nenhum fluxo pode garantir ausência de falso positivo: reputação e heurística dependem do fornecedor.
- **Aprovação por hash** pode precisar ser refeita para cada binário novo; **assinatura por editor**, quando suportada e aprovada, melhora a continuidade, sem ser garantia.
- Caso o AVG confirme ameaça real, a causa precisa ser removida/corrigida antes de qualquer distribuição.

## Investigação Lote 3 — somente evidência técnica

[Relatório do Lote 3](MISSAO-LOTE3.md) e [PR #56](https://github.com/walissonminel-1991/gerenciador-final/pull/56).
Erros Windows 225/226 passam a explicar bloqueio antivírus sem atribuir fabricante;
erros 5/32 continuam separados. A CI registra origem, hash e assinatura do
produto em JSON separado, explicitamente marcado sem varredura e AVG pendente.
Não desativa proteção, cria exceção, restaura quarentena ou envia arquivos ao AVG.
P6 permanece aberto: causa, resposta do fornecedor e estabilidade real não comprovadas.

Fontes conferidas em 25/10/2026:
[FAQ oficial](https://support.avg.com/SupportArticleView?l=en&urlName=avg-threat-lab-file-whitelist-faqs),
[diretrizes](https://support.avg.com/SupportArticleView?l=en&urlname=avg-threat-lab-clean-guideline)
e [quarentena](https://support.avg.com/SupportArticleView?l=pt&urlName=Use-AVG-Antivirus-Quarantine).
