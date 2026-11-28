# Auditoria cromática V2.6.9 — 25/11/2026

## Autoridades
- HTML PR #109: 825f24a53808204642ed11971d66f2c9e30c3259, `prototipos/ui-v269/GF_Prototipo_Interativo_V2_6_9_2026-11-23.html`. Ler a última regra aplicável da cascata, não somente :root.
- Monitor PR #114: 0f10023975cb966fb5d7761828fa6b320eccfd30, `UiV269DynamicMeter.cs` e contrato de integração. Preservadas escalas, estados, limites, dados e regras de animação.
- Referência marcada do proprietário (25/11): pixels opacos RGB (0,162,232), (237,28,36), (34,177,76). Amarelo #FFDF00 é proposta de implementação, não pixel medido na referência.
- PR #113: candidata anterior 1e4432771e8d1ca29a4010ec22c9e02cb578d35c preservada; recuperação 2407d42dbdba711932bf2bad8d63d292a5aa1695.

## Causas confirmadas
1. A paleta central Dark conserva valores da cascata anterior (fundo #14181d, sidebar #1c252d, conteúdo #202a33, cartão #242c34). Última cascata: #0e0f11, #1b1e21, #202327, #2b2e33. Corrigidos os tokens de fundo, menu, conteúdo, cartão, painel, borda, leitura, seleção e destaque.
2. Monitor usava frame.Color tanto em texto quanto preenchimento. Contraste de fonte condicionava a saturação da barra. Separados MeterFillColors e MeterInkColors, com trajetória compartilhada; RGB opaco uniforme em preenchimentos.
3. Tokens duplicados em shell, inventário, detalhe, ferramentas, disco, sistema/inicialização, toolbar, cartões e configurações. Centralizados grupos de cores e gradientes em UiV269Theme.cs; valores específicos já existentes mantidos (não reduzir todos os cartões a uma única cor).
4. Transparências da caixa de ícone e fundo decorativo não são equivalentes à opacidade do glifo ou da fonte. Preservadas as transparências CSS intencionais. Não aplicar alpha ao preenchimento semântico nem ao texto de navegação.

## Evidências matemáticas (não substituem CI/pixels)
Amostragem das quatro transições de tinta RGB em passos de 0,01%, sobre cartão efetivo: Azul GF mínimo 4,949:1; Dark 4,834:1; Claro 4,818:1. Texto Dark azul ligeiramente recalibrado para manter 4,5:1 na superfície corrigida.
Inventário: texto normal, alternado, seleção em ambos extremos do gradiente e cabeçalho têm contraste calculado >=4,5:1 nos três temas.
Conflito real no HTML aprovado: texto da navegação ativa Dark #e3e6e9 sobre #666d74 atinge 4,19:1; Claro branco sobre #6f97bc atinge 3,08:1 (outro extremo 4,30:1); Azul GF branco mínimo 5,60:1. Preservados os gradientes e textos aprovados, sem substituir silenciosamente a referência. O gate V269-COLOR-AUDIT explicita REFERENCE_CONFLICT. A correção de contraste desses estados exige aceitar uma alteração visual pontual.
Preenchimento vivo e fundo de trilha podem ficar abaixo de 3:1, especialmente Claro. Não confundir contraste de texto com contraste da barra; revisar separação gráfica/contorno em capturas, sem dessaturar as âncoras marcadas. A informação numérica e acessível continua presente.

## Gates
- Testes originais de interpolação, âncoras, evidência térmica, estados e acessibilidade mantidos.
- Teste de pixels distingue valor textual e preenchimento; exige RGB interior das quatro âncoras em todos os temas; registra contraste das superfícies e estados.
- Homologação Windows, capturas reais nos três temas, 1440x900 e 2560x1440, transições, escala/DPI e sessão contínua pendentes para a candidata cromática. Não afirmar conclusão pela análise estática.
- Lote 4: falha comprovada em CI 45697735231 (CS0246) porque a mutação histórica de DarkLogView removeu os novos viewports colocados entre os marcadores. Isolados em UiV269ScrollHosts.cs; nenhum teste ou workflow alterado. Drivers Windows 2025 apresentou TaskCanceledException na fase Published, não alterar o motor ou o prazo nesta frente.
