# GF — U6 — reconciliação WinGet por evidência existente (20/11/2026)

**Meta do roadmap:** U6 — explicar divergências de cobertura com WinGet.  
**Situação:** EM ABERTO; nenhuma promoção de `27/42` para `28/42` nesta etapa.  
**Branch isolada:** `u6-winget-reconciliacao-20261120`, criada exclusivamente a partir do commit `0325c5617b10e25c36a6d7396fceab9ee9282eeb` da PR #78.  
**Limites:** nenhuma modificação de `main`, PRs #79/#80, linha G3, executáveis, atualização/instalação, WinGet COM, endpoints, Worker, D1, G5, recursos de privacidade ou computadores pessoais.

## 1. Evidência física anterior (não confundir versões)

O registro histórico do PC FORTE, v5.13.11.32 (21/10/2026), informa **174 programas, 27 MSIX, três candidatas GF e nove candidatas `winget upgrade`**, observadas naquele computador. Era uma comparação consultiva do inventário daquela sessão, **não** uma comprovação de nove instalações elegíveis para execução silenciosa.

* Correspondências: **7-Zip** e **Outlook for Windows**.
* Divergência de versão: **Proton Pass**, com GF **1.43.0** e WinGet **1.42.0**, ligada a canais/fontes diferentes na evidência da época; a hipótese de manifesto defasado não está comprovada como causa única.
* Candidatas WinGet não oferecidas pelo GF daquela versão: **EA app, FanControl, Jellyfin Server, K-Lite Mega Codec Pack, Microsoft GameInput e Plex Media Server**.

Fonte histórica declarada pelo mantenedor no documento `PROMPT-WORK-5-WINGET-AVG-REVISAO-SEGURA-GERENCIADOR-FINAL.md` (material de projeto previamente fornecido; sem reproduzir caminhos, dados privados ou logs brutos). A ausência aqui dos horários exatos e da listagem bruta impede considerar o pareamento auditado com precisão de minutos.

## 2. Reconciliação com implementação posterior (PR #78)

A candidata técnica contém fontes de consulta no arquivo `OfficialReleaseSources.cs` para os itens EA app, FanControl, Jellyfin Server, K-Lite Mega, Plex, Proton Pass, Microsoft GameInput e Outlook. Isso comprova **existência de receitas de consulta e verificações de identidade no código**, não disponibilidade atual da fonte, elegibilidade silenciosa ou oferta real na máquina.

| Caso | Explicação sustentada / situação pendente |
| --- | --- |
| 7-Zip | Correspondência histórica GF–WinGet; não há divergência desse item a resolver na fotografia antiga. |
| Outlook for Windows | Correspondência histórica de oferta; a implantação é tratada como Store/MSIX, não como instalador Win32 genérico (WORK18). |
| Proton Pass | Diferença histórica entre versão do feed oficial e versão apontada pelo catálogo WinGet. Fonte por canal distinguida no código; falta comparar os metadados exatos daquela sessão para atribuir causa definitiva. |
| EA app | Fonte posterior adicionada; WORK18 identifica Burn e `uninstallPrevious`, motivo para não autorizar execução silenciosa genérica. |
| FanControl | Fonte oficial GitHub posterior adicionada. Falta evidência individual de instalação, identidade e resultado real da fonte no mesmo snapshot para explicar toda a diferença. |
| Jellyfin Server | Fonte oficial posterior adicionada; descoberta da tag não comprova instalador Windows, identidade ou segurança de manutenção de servidor (WORK18). |
| K-Lite Mega Codec Pack | Fonte posterior adicionada; pacote composto e arquitetura/contexto local não corroborados para instalação automática (WORK18). |
| Microsoft GameInput | Fonte WinGet posterior adicionada; componente Microsoft do Windows requer proteção contra ações genéricas; ausência de botão de atualização não equivale à ausência de consulta. |
| Plex Media Server | Fonte posterior adicionada; WORK18 distingue serviço e dados de servidor, sem contrato de manutenção genérica homologado. |

Documentação de implementação: `OfficialReleaseSources.cs` e `docs/MISSAO-WORK18.md` no commit-base da PR #78. O método `OfficialReleaseSources.CoverageStage` já discrimina etapas 1–12: fonte, elegibilidade, identidade/canal, execução da consulta, resultado, conflito, revisão e comparação de versões. **Não criar outro motor nem novos contadores** antes de usar estes motivos existentes.

## 3. O que NÃO é equivalente

* WinGet mostrar uma versão mais recente **não** autoriza baixar, instalar, forçar atualização ou declarar compatibilidade; a política do GF exige identidade, arquitetura, escopo, tipo de instalador e autorização.
* O backend WinGet COM de produção lê inventário instalado. Não é a mesma operação que `winget upgrade` para consultar ofertas. Falha COM tampouco torna inválidas as outras fontes HTTPS.
* A avaliação posterior em 28/10 da .66 encontrou **196 entradas e 14 candidatas GF**, com fontes parciais e WinGet COM indisponível. Não há uma lista `winget upgrade` pareada para esta versão. **Não comparar 3 vs. 9 de 21/10 com 14 de 28/10 como se fosse um experimento controlado.**
* Testes simulados, CI, estimativas de cobertura e capturas isoladas não comprovam classificação individual no PC FRACO/PC FORTE.

## 4. Critério de encerramento mínimo para U6

1. Reaproveitar a comparação histórica e a matriz anterior como casos conhecidos, **sem repetir** uma bateria completa ou realizar instalações.
2. Na próxima candidata portátil já autorizada e verificável, obter **uma** comparação consultiva no mesmo equipamento, inventário e janela temporal (preferencialmente PC FRACO, preservando o PC FORTE usado pela G3): versão/SHA GF; contagem e motivos locais; lista WinGet com versões e canal quando puder ser consultada, sem reparo/elevação; horário e status de fontes.
3. Para cada diferença efetiva, classificar: ausência de fonte; fonte indisponível; identidade/canal/arquitetura/escopo; versão não comparável; revisão/conflito; gerenciamento externo; oferta compatível. Usar `CoverageStage` e evidência por instalação, sem deduzir nomes a partir de contagens anônimas.
4. Caso uma diferença permaneça inexplicada, registrar somente esse bloqueio e o teste direcionado correspondente. Não repetir as outras verificações nem ampliar mecanismos do WinGet.
5. Só marcar U6 como **concluída** quando a lista de divergências da comparação tiver explicações verificáveis e rastreáveis. Atualizar roadmap/JSON apenas no commit que satisfizer o critério.

## 5. Fronteiras das outras linhas

**R7:** código integrado e CI reutilizável; o checkpoint separado `GF_R7_CHECKPOINT_20261120.md` informa que faltam pacote portátil verificável e autorização específica da desinstalação para homologação física. Não executar remoção nem criar experimentos para gerar demanda.

**R6:** revisão de casos históricos ambíguos continua dependente de evidência de proveniência/uso e pode aproveitar somente uma desinstalação R7 legitimamente autorizada; não criar limpeza separada.

**G3:** intocada. Nenhum merge, push, edição, consulta operacional nem rebase da branch `experimento-atualizador-drivers-wua-20261114` ou da PR #80.

## 6. Resultado desta etapa

**Entregue:** separação explícita dos nove casos históricos, provas de cobertura de consulta no código posterior, justificativa da não equivalência entre oferta e execução e critério de aceitação direcionado.  
**Bloqueio restante:** ausência de captura pareada para uma candidata atual com motivos individuais correlacionados.  
**Roadmap:** permanece **27/42**; sem alegação de U6 encerrada.  
**Mudança de produto:** nenhuma; documento somente em branch isolada.