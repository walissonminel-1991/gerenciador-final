# Observações reais da v5.13.11.8 — computador de testes de 20/10/2026

**Origem:** seis capturas de tela e `GerenciadorFinal-Diagnostico-v5.13.11.8.log` fornecidos pelo testador. São observações no hardware real; não certificam instalação, desinstalação, reparo, ausência de malware ou compatibilidade universal.

## Constatações confirmadas

- A interface de aplicativos, drivers, limpeza, reparo, energia e RAM foi exibida e consultável. A função de inventário terminou sem apontar erro e a janela foi ocultada/restaurada pela bandeja duas vezes sem falha reportada.
- O estado na interface indica **diagnóstico local voluntário ativo; nenhum dado enviado pela internet**. Não há prova por captura de exclusão pós-revogação, persistência após reinício, ou upload remoto (o envio segue inexistente).
- Inventário ARP: `171` registros, `289` examinados, `98` ocultos, `12` duplicados reconciliados, `0` erros reportados. MSIX: `158` enumerados e `27` incluídos. Steam: `42` entradas, `5` adicionadas. Não somar indiscriminadamente os números: representam etapas e classes diferentes.
- Atualizações: `1` candidato consultivo, apresentado como Outlook pela Microsoft Store; `107` sem correspondência de catálogo, `31` somente inventário, `3` correspondências ambíguas, `1` versão principal divergente, `2` registros antigos com versão nova instalada. Uma fonte oficial exibiu `TaskCanceledException` para consulta Proton Pass; não assumir erro da aplicação nem recomendar atualização automática sem verificar.
- Windows Update: consulta retornou `0` atualizações. Isso não prova que o sistema esteja atualizado em todas as categorias ou políticas.
- Drivers: `184` associações, `65` INFs, `20` INFs compartilhados, `3` sem INF válido, nenhuma divergência de versões/assinaturas reportada nesse agrupamento. **Não** tratar 184 como pacotes individuais ou as 101 instâncias PnP sem confirmação como drivers órfãos.
- Diagnóstico geral: `1` desinstalador ausente, `101` instâncias PnP sem confirmação e `0` problemas de dispositivo reportados. A captura cita um registro histórico de desinstalação relacionado a `Most Wanted Ultimate Edition`; apenas revisão, sem exclusão do Registro.
- Captura de limpeza: temporários do usuário ~169,1 MiB (214 itens), Windows ~4,5 MiB (37 itens), shaders ~6,5 MiB (21 itens), **estimativas parciais**. Nenhuma limpeza efetiva foi comprovada.
- Unidades na captura: C ~70% usada, D ~95% usada, E ~79% usada. Aviso educativo sobre D é aceitável, mas nenhuma limpeza automática nem modificação de arquivos só com base na porcentagem.
- Capturas da RAM mostram dados/linha de tendência. Monitoramento de uma sessão curta não comprova precisão de longo prazo nem efeito da otimização.

## Problemas de interface visíveis

1. **v5.13.11.8:** janela de consentimento unia parágrafos no campo de visualização. Corrigido na candidata v5.13.11.9 (normalização CRLF, testes Windows); falta confirmação visual posterior em PC real.
2. **v5.13.11.8:** a tabela de drivers ficava muito estreita e deixava grande área lateral ociosa; nome do dispositivo, fornecedor e versão eram truncados. Na v5.13.11.10, seis colunas passam a usar `AutoSizeColumnsMode.Fill`, pesos e mínimos, com teste de layout.
3. Nas telas Reparo e Rede e Planos de Energia, há ampla região vazia por não haver diagnóstico ativo ou lista adicional. Não é falha funcional por si só. Refinamentos opcionais devem ser compactos, sem adicionar botões perigosos.
4. Histórico dos drivers começou na consulta atual: não apresentar como reconstrução das instalações anteriores.

## Próximos gates reais

- [x] Abrir programa e navegar em abas, sem alerta AVG de abertura reportado pelo testador.
- [x] Demonstrar recusa de coleta e posterior estado local ativo em interface.
- [x] Consultar drivers, fontes, inventário, limpeza e diagnóstico geral sem confirmar alteração do Windows.
- [ ] Conferir apresentação de parágrafos corrigidos v5.13.11.9+ em PC real.
- [ ] Confirmar largura/legibilidade da tabela de drivers v5.13.11.10 em PC real.
- [ ] Verificar persistência da escolha após encerrar e reabrir e revogação apagando a pendência local; não enviar logs brutos.
- [ ] Validar atualizações com comparação independente; testar instalação **individual e segura** de aplicativo não crítico após backup.
- [ ] Verificar desinstalação de aplicativo de teste, análise de resíduos somente leitura, confirmação explícita e possibilidade de recuperação.
- [ ] Inspecionar reparo de rede/SFC/DISM primeiro como diagnóstico; executar alterações somente quando justificadas e autorizadas.
- [ ] Validar o AVG durante uso prolongado e no PC FRACO em outro momento.

**Nenhum arquivo, driver, chave do Registro, programa ou configuração foi alterado pelo trabalho de análise das capturas.**
