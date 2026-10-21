# Segunda bateria no PC fraco — v5.13.11.19 TESTE (21/10/2026)

Documento de evidências **agregadas**, extraídas de capturas e do arquivo de
etapas de teste fornecidos voluntariamente. Não anexar log bruto, contas,
caminhos pessoais, imagens com dados pessoais ou identificadores de hardware.

## Resultado em 1440×900

- Janela aberta, filtros, pesquisa, classificação, inspeção sob demanda e
  encerramento normal. Nenhuma exclusão de resíduos, desinstalação ou
  alteração de drivers.
- Nomes de pastas genéricas `Config`, `Logs`, `Updates`, `Scripts` e
  `Providers` deixaram de ser exibidos como produtos independentes.
- Revisão combinada: **12 grupos**, **41 locais**. Bateria anterior: 17 grupos
  e 57 locais; a diferença é de agrupamento/classificação, não limpeza.
- Descoberta geral: 12 origens, **2.000 entradas examinadas**, 11 candidatos,
  3 instalações a conferir, 2 fontes inacessíveis, `parcial=True`.
  Não confundir grupos da revisão combinada com candidatos da descoberta.
- Seleção `Possível resíduo — revisar` não encontrou grupos: enquanto parcial,
  isso **não** significa que os dados sejam removíveis ou que nada exista.
- **Bug novo reproduzido pela captura**: ao escolher origem `Perfis do usuário`
  ou `Dados comuns`, a seleção de grupo era filtrada, mas a lista direita
  ainda exibia locais de outras origens. Ex.: grupo com ProgramData,
  AppData e Registro exibido por inteiro após selecionar uma origem.
- Outros achados seguem corretamente conservadores: Driver Booster, IObit,
  GOG.com, McAfee, HP e aplicativos de terceiros mantidos como dados ou
  instalações não confirmadas, sem licença para exclusão.

## Métricas observadas da sessão (não são benchmark controlado)

| Etapa | Resultado |
| --- | ---: |
| Inicialização | 708,86 ms |
| Inventário inicial | 1.290,51 ms |
| Busca inicial de atualizações | 2.941,40 ms |
| Descoberta de resíduos | 294,66 ms |
| Classificação de resíduos | 29,34 ms (dentro da descoberta) |
| Primeiro filtro | 7,13 ms |
| Inspeções sob demanda | 8,77–22,56 ms |
| Pico de memória de processo | 134,2 MiB |
| Aplicativos inventariados | 74 |
| Possíveis atualizações | 2 (nenhuma instalada) |
| Associações de driver / pacotes INF | 108 / 50 |
| Alterações no histórico de drivers | 0 adicionados, 0 não observados, 0 alterados |

Um teste real em um computador não prova ausência de riscos em outras máquinas.

## Correções planejadas na v5.13.11.20 TESTE

1. O filtro de **origem** e de **classificação** passa a selecionar também os
   locais individuais apresentados, não apenas os grupos.
2. Busca por termos presentes num local exige que o mesmo local corresponda
   à origem selecionada; os resultados não cruzam locais distintos para
   gerar falsos positivos.
3. Contadores de locais refletem os locais correspondentes aos filtros.
   Fotografia original e dados preservados.
4. Estado vazio tem mensagem distinta para filtros sem resultados e análise
   parcial sem candidato revisável, sem dizer que tudo está limpo.
5. Adicionar testes sintéticos e WinForms de interseção entre origem,
   classificação, pesquisa, contagem e navegação sem exclusão real.

## Teste seguinte

Na próxima versão, repetir exclusivamente a análise somente leitura, selecionar
`McAfee` e alternar `Dados comuns`, `Perfis do usuário` e `Registro`:
somente os locais correspondentes devem aparecer à direita. Conferir também
interseção com `Compartilhado — preservar`, pesquisa, retorno a `Todas as
origens`, contagens e encerramento. Manter a análise parcial explicitada.
