# GERENCIADOR FINAL — Plano de execução: chat Alto + Work somente por marcos

Data: 29/10/2026. Linha de trabalho: PR #65 Draft, `work-integracao-final-20261027`.
Baseline conferido ao planejar: `a45f63ed4959f57d965a7de4c4e249db62626823`.
Documento operacional de coordenação; **não** é evidência de gate, homologação, versão ou avanço do roadmap.

## Estado inicial verificável

- Roadmap funcional: **27/42**; não misturar checkpoints do GF com os gates do autoatualizador.
- Autoatualizador: Etapas 0–13 concluídas em seus escopos experimentais; G1–G8 **blocked**, zero fechado.
- G2: dossiê para revisão independente pronto; sem parecer externo, manutenção formal aceita ou adoção.
- BETA `v5.13.11.66` e distribuição privada preservadas; envio remoto desativado.
- PR #65 aberta/Draft; sem merge, release, publicação ou integração de autoatualizador não homologado.

## Regra de uso de recursos

**Chat normal, esforço Alto: execução padrão.** Conferir código e HEAD, reduzir falha a teste, implementar patch pequeno e rastreável, consultar GitHub, revisar diff e usar verificações locais/disponíveis. Não presumir execução real no Windows ou aprovação de segurança sem evidências.

**Work: recurso de validação por marco, não por etapa numerada.** Acionar somente se há um conjunto coeso de mudanças a validar, critérios de entrada definidos e uma capacidade importante que o chat normal/CI existente não cobre. Antes de cada missão produzir prompt único com escopo, SHA, suíte direcionada, limites e parada obrigatória.

**Revisão independente: recurso humano separado.** Work ou outra sessão deste modelo não substituem revisão de terceiro qualificado nem aceite formal de manutenção. Preparação do dossiê G2 já concluída; não refazê-la.

## Sequência de trabalho e gates de decisão

| Marco | Responsável primeiro | Entrega mínima demonstrável | Quando acionar Work |
| --- | --- | --- | --- |
| M0 — Baseline e inventário dirigido | Chat Alto | Confirmar PR/HEAD, localizar entradas GF (GUI, COM, worker, bandeja), código dos probes e políticas de encerramento; escolher interfaces G5/G6 sem tocar no produto | **Não** |
| M1 — G5, IPC experimental | Chat Alto | Protótipo isolado de handshake autenticado, identidade de instalação/usuário/sessão, nonce, autorização, timeout; rejeição explícita de conexões cruzadas/ilegítimas; testes negativos direcionados | Somente se teste real com token não elevado/sessões diferentes precisar de laboratório que o chat não consiga executar |
| M2 — G6, ciclo experimental | Chat Alto | Adaptador isolado para descoberta de participantes, solicitação/ack de encerramento, espera de saída real, prontidão da nova versão e rollback em fixture; sem executar ou substituir GF | **Work #1**, após M1+M2 coesos, para validação Windows descartável de IPC entre contextos e ciclo real quando houver ambiente/autorização |
| M3 — G8/G1, recuperação difícil | Chat Alto | Revisar barreiras I/O pendentes e desenhar testes de crash durante recuperação, hang de Flush/ReplaceFile, frescor fora do domínio restaurado; implementar só subprovas possíveis e manter condições desconhecidas bloqueantes | **Work #2**, apenas com laboratório/hipóteses testáveis e resultados de M1/M2 disponíveis; não simular aprovação física |
| M4 — Confiança externa e candidato futuro | Mantenedor/revisor externo + Chat Alto | Obter revisão independente G2, aceite de manutenção, decisão de custódia/âncora G7 e perfil G3/G4; usar chaves de teste apenas no protótipo até autorização específica | Não utilizar Work para inventar revisão, chaves, custódia ou autorização |
| M5 — Integração e BETA futura | Chat Alto prepara; Work valida se necessário | Integrar só com gates aplicáveis satisfeitos; CI de pacote real futuro, rollback/IPC/segurança qualificados; primeiro teste autorizado em PC FORTE, depois piloto conectado sob consentimento e backend aprovados | Work de homologação **somente quando** houver candidata e ambiente realmente aptos |

O número de Work indicados acima é **planejamento de uso**, não garantia de que as validações caberão em duas missões. Se um marco já puder ser concluído por CI verificável e ferramentas normais, dispensar Work; se faltar autorização ou ambiente, marcar blocked e não gastar carga.

## Trilha paralela do GF funcional

Se dependências externas do autoatualizador impedirem progresso verificável, priorizar itens reais do roadmap do GF (por exemplo U8 atualização executável, R4 desinstalação/resíduos, I7 inventário físico), **um caso reproduzível por vez**. Primeira análise e correções no chat Alto; Work só para validar lote com mudança funcional. Não contar testes/protótipos como checkpoint sem os critérios físicos pertinentes.

## Critérios de qualidade e controle

1. Antes de qualquer edição, conferir HEAD e, para mudanças de código, preferir branch experimental exclusiva baseada no SHA vigente. Preservar PR #65 Draft como linha técnica; não criar concorrência com Work.
2. Cada alteração: problema, escopo, arquivo, patch, teste positivo/negativo, SHA, risco residual e justificativa para não repetir suites históricas.
3. Sem relaxar proteção para aprovar teste. Nunca usar fixtures ou contas com privilégio elevado como substituto de teste padrão entre usuários/sessões.
4. Não misturar TUF fixture com autoria operacional; não assinar ou distribuir retroativamente a .66. Não criar chaves operacionais nem exigir certificado comercial para beta-unsigned.
5. Nunca alterar `main`, canal privado, diagnósticos remotos/flags, AVG/Windows, drivers desativados, VLC somente inventário, WinGet consultivo, Worker/D1/DNS sem autorização explícita.
6. Cada checkpoint de Work deve responder: código mudou? que testes rodaram? que gates receberam subprova? falhas restantes? qual é a próxima **ação técnica**? Encerrar sem iniciar automaticamente outra missão.
7. Reavaliar prioridades por entrega funcional — não por quantidade de etapas, arquivos ou testes.

## Próxima ação imediata (chat normal)

**M0 → M1:** inspecionar Program.cs/worker/GUI/encerramento e os probes Windows anteriores; produzir mapa mínimo dos pontos de IPC e um contrato de handshake experimental; implementar primeiro teste negativo no protótipo isolado, se tecnicamente viável e aprovado quanto ao escopo. **Não convocar Work agora.**
