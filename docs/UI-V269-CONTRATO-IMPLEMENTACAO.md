# GERENCIADOR FINAL — Contrato de implementação da interface V2.6.9

**Data de aprovação visual:** 23/11/2026  
**Status:** referência visual aprovada; implementação nativa WinForms **não iniciada nesta PR**.  
**Origem:** protótipo interativo V2.6.9, 8 abas, 3 temas, operações simuladas e offline.  
**HTML congelado:** [prototipos/ui-v269/GF_Prototipo_Interativo_V2_6_9_2026-11-23.html](../prototipos/ui-v269/GF_Prototipo_Interativo_V2_6_9_2026-11-23.html)  
**SHA-256 do HTML aprovado:** `9942b8ed29335cb526825b7658d0f8ab11f2c7241b46a6a99b959941b31dca44`  
**Base de preparação:** PR #101, commit `72669bf1494315d90c1192fb2465b0287544d68e`.  
**Branch exclusiva:** `ui/v269-referencia-aprovada-20261123`.

> **Regra central:** o protótipo fixa a aparência, a disposição, a interação e os estados visuais. Os dados e as operações de exemplo NÃO são requisitos de comportamento real. Implementar a interface com os motores atuais do GF, sem portar JavaScript como backend e sem fingir sucesso de manutenção.

## 1. Contrato de escopo

- Preservar o **Azul GF** como tema padrão; disponibilizar **Dark neutro de preto/grafite** e **Claro azul suave**, todos fiéis à V2.6.9.
- Navegação lateral compacta com **8 abas**: Início, Aplicativos, Drivers, Limpeza e Desempenho, Reparo e Rede, Sistema, Inicialização, Configurações.
- Cada aba deve usar área principal e **painel direito persistente** para detalhes, revisão, confirmação, progresso, resultado e histórico. Evitar diálogos flutuantes para estes estados. Exceções impostas pelo próprio Windows (por exemplo UAC) não são substituídas por confirmação falsa.
- Preservar alinhamento de tabelas, larguras relativas, linhas compactas, seleções visíveis, busca, ordenação e ícones nas linhas. Não aumentar indiscriminadamente os botões; não remover colunas.
- Os controles de ações apresentam somente o resultado prático **pode / não pode**. Não acrescentar categorias ou etiquetas "não verificado", "sem oferta" e "bloqueado" junto aos ícones. O sistema interno pode manter estados técnicos para decisões seguras; esses estados não se convertem em permissões fictícias.
- Botão atualizar: **verde somente se houver atualização realmente elegível**; caso contrário desabilitado com cor neutra (**cinza no Dark**, **azul muito claro no Claro e Azul GF**).
- Botão desinstalar/remover: **vermelho vivo somente quando a ação for elegível**; caso contrário desabilitado e visualmente neutro (**cinza no Dark**, neutro discreto nos outros temas).
- Ícones devem ser nítidos, reconhecíveis e de saturação suficiente nos três temas, sem ofuscamento nos textos do Dark. Os temas não devem compartilhar um tom azul dominante.
- Início apresenta inventário e sensores do hardware em cartões informativos, sem inventar medidas indisponíveis nem abrir ações de manutenção nessa página.
- Configurações: seletores de tema, barras de **tamanho da interface 90–110% (passo 1%)** e **tamanho do texto 90–115% (passo 1%)**, animações discretas aplicadas globalmente, opções gerais e consentimento.
- Nada de dicas flutuantes ao passar o mouse (*tooltips*). Manter descrições acessíveis para teclado/leitores de tela.
- Respeitar preferência do sistema para movimento reduzido; nunca relacionar animações à velocidade de operações reais.
- Evitar scrollbars duplas, clipping, perda de foco, alteração de seleção durante execução e reconstrução que apague resultados.
- A homologação final de dimensões/DPI e resolução **fica na última etapa**, após estabilização funcional, sem dispensar verificações básicas de ausência de clipping durante o desenvolvimento.

## 2. Fonte de verdade e limites

O HTML de referência é **uma demonstração estática/offline com estado local fictício**. Não executar esse HTML como o produto, não integrar seus registros ilustrativos ao inventário real, não transportar credenciais, caminhos, números de dispositivos, versões ou resultados simulados para a aplicação.

### Código existente a preservar e adaptar com parcimônia

| Componente existente na base da PR #101 | Possível integração nativa | Limite |
|---|---|---|
| `Program.cs` / `MainForm` | navegação, composição, painel lateral, estados de ação | não reescrever ou substituir backends |
| `DarkControls.cs` / `DarkProgramList` / `DarkTabHost` | tabela e interação de Aplicativos, foco e rolagem | não quebrar virtualização e DPI |
| `DarkDriverList.cs` | linhas compactas e comandos por dispositivo/driver | preservar identidade PnP e autorização WUA |
| `IntegratedSystemUi.cs` | cards de Sistema, Windows Update, energia, inicialização | preservar revalidação e proteções |
| `ResponsiveLayout.cs` / `AppActionLayout.cs` | geometria, escalas, anchoring, monitor/DPI | não assumir pixels fixos do HTML |
| `TestesLayout` e testes de domínio existentes | contratos de layout, foco, estados, regressão | não alterar testes para mascarar falhas |

Esses caminhos foram inspecionados no commit-base; a listagem é um mapa de intervenção, não autorização para editar todos os arquivos simultaneamente.

## 3. Separação de camadas recomendada

1. **Tokens de tema e tipografia:** uma fonte nativa compartilhada para cores, espaçamentos, estados de ação e escala, com `Color`/fontes WinForms; leitura e validação centralizadas.
2. **Shell visual:** sidebar, título, conteúdo, separador ajustável, painel direito e estados visuais, sem regras de negócio.
3. **View models por aba:** projeções de dados observados e capacidades efetivas; modelos tipados, eventos e cancelamento, não flags deduzidas de texto/cores.
4. **Controladores de operações:** máquina de estados (revisão → confirmação explícita → executando → conclusão/cancelamento/erro); preservação do histórico e do foco ao navegar.
5. **Adaptadores de motores existentes:** serviços atuais de Aplicativos, Drivers, Windows Update, Limpeza, Reparo, Rede, Inicialização, sensores e energia; cada motor conserva políticas de autorização e evidência independentes.
6. **Acessibilidade e testes:** comandos por teclado, leitura de controles, foco restaurável, contraste, scroll e DPI.

Nenhum botão colorido autoriza uma ação. **Elegibilidade é calculada pelo backend**, revalidada no momento de executar, e deve falhar de forma segura quando houver mudança de inventário, autorização, assinatura, dependência, origem ou confiança.

## 4. Regras específicas de ações

### Atualização

- Verde **apenas** para uma oferta atual efetivamente elegível pelo motor correspondente. Não significa que a instalação foi concluída.
- Neutro/desabilitado em qualquer caso restante. O usuário quer uma decisão visual binária, sem uma terceira legenda. Informações técnicas detalhadas podem ficar no painel direito quando úteis para diagnóstico, sem confundir a permissão operacional.
- Ações disponíveis exigem consentimento e confirmação por item/lote, revalidação e acompanhamento real, com resultado baseado em evidências, não na saída textual presumida.
- Drivers de vídeo e dispositivos críticos permanecem sujeitos às proteções existentes; **nenhuma elegibilidade pode ser inferida somente da identidade do hardware**.

### Desinstalação

- Vermelho vivo **apenas** quando o motor atual permite desinstalar/remover o item no contexto corrente.
- Neutro/desabilitado para dispositivos, componentes protegidos ou dependências não elegíveis.
- Abrir painel direito para revisar alvo, impacto, operação e consentimento antes de executar; preservar confirmação adicional quando o domínio exigir.
- Não permitir remoção de dispositivos/proteções do Windows porque o protótipo ilustrativo exibia um ícone de lixeira.

### Painel e histórico

- Painel direito é a única superfície visual da aplicação para revisar/confirmar/progredir/concluir e consultar histórico.
- Manter estado de execução ao trocar aba; não afirmar sucesso após cancelamento ou falha.
- Não ocultar indevidamente resultados não dispensados; restaurar o foco para uma ação ou linha válida ao fechar.
- Execuções simultâneas só quando suportadas pelo motor real; nunca abrir execuções concorrentes conflitantes.

## 5. Ordem de implementação — incrementos isolados

**Etapa A — fundação visual:** tokens dos três temas, escala de texto/interface, shell e painel, sem substituir nenhuma tela funcional e sem conectar ações reais. Testes de render e teclado.

**Etapa B — Início e Configurações:** cartões de hardware associados a leituras nativas, sensores indisponíveis tratados honestamente; preferências locais e consentimento real intactos.

**Etapa C — Aplicativos e Drivers:** tabelas compactas e botões binários com elegibilidade real, busca/ordenação, confirmação/painel, controles de identidade. Integrar separadamente as PRs funcionais que já evoluem essas frentes.

**Etapa D — Limpeza, Reparo e Rede, Sistema, Inicialização:** apresentar motores já existentes na nova organização, sem refazer políticas de segurança.

**Etapa E — integração e homologação:** mesclar somente mediante revisão coordenada da ancestralidade das PRs #101, #103, #105, #106, #107, #108 e outras ativas; CI no SHA exato; validação real em Windows; por último, DPI/dimensionamento em PC FORTE e PC FRACO.

Cada etapa exige uma PR/commit independente ou sequência linear de commits revisáveis, nunca substituição integral de `MainForm` de uma vez.

## 6. Gates de aceite

- [ ] Nenhuma regressão funcional comprovada nos motores existentes.
- [ ] As 8 abas e o painel direito reproduzem a V2.6.9, com paridade entre os 3 temas.
- [ ] Ícones de atualizar/desinstalar indicam corretamente **pode / não pode**, em Aplicativos e Drivers.
- [ ] Controles desabilitados não recebem foco de ativação nem disparam serviços.
- [ ] Toda operação administrativa exige autorização real e explícita; nada de credenciais embutidas.
- [ ] Nenhuma atualização/desinstalação/limpeza/reparo ocorre pelo simples ato de navegar ou consultar.
- [ ] Sem *tooltips* de hover, modais desnecessários ou toasts sobrepondo a interface.
- [ ] Foco, teclado, painel, histórico e resultados persistem corretamente.
- [ ] CI Windows + integridade + testes direcionados aprovados no SHA de cada etapa.
- [ ] Homologação física e teste definitivo de DPI/dimensionamento apenas ao final.

**Não alterar nesta preparação:** `main`, releases, autoatualizador G5, GF_BETA, Worker/Cron/Secrets, workflows, lista de segurança, drivers reais ou o Windows dos computadores do proprietário.

## 7. Estado deste lote

Esta PR **prepara o GitHub** e preserva a referência visual, o contrato de implementação e a matriz de aceite. Ela **não implementa o novo layout no EXE**, não substitui telas WinForms e não realiza operações reais. A execução das etapas A–E depende de novos commits de integração, revisão de conflitos e testes Windows.
