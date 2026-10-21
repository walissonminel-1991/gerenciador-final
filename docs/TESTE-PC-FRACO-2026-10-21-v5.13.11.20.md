# Terceira bateria real — v5.13.11.20 TESTE (21/10/2026)

Dados agregados provenientes de capturas e registro local fornecidos
voluntariamente para depuração. **Não** anexar arquivo bruto, capturas,
identificadores de usuários, aplicações pessoais ou hardware ao repositório.

## Filtros e resíduos

**Correção confirmada no PC fraco:** o painel direito acompanha o filtro de
origem. Para um grupo representativo com evidências em
`ProgramData`, `AppData` e Registro, a seleção por origem apresentou:
- Todas: 3 locais;
- Dados comuns: 1 local correspondente;
- Perfis do usuário: 1 local correspondente;
- Registro: 1 local correspondente.

Interseções sem correspondência apresentaram mensagens vazias apropriadas,
sem afirmar que não há resíduos. Busca por fabricante e categorias funcionaram.
A investigação permanece **ANÁLISE PARCIAL** e **somente leitura**, sem
autorização de remoção.

## Desempenho e resultados registrados (sessão única)

| Indicador | Resultado |
|---|---:|
| Inicialização | 622,01 ms |
| Primeira consulta do inventário | 314,00 ms |
| Primeira consulta de atualização | 2.759,57 ms |
| Descoberta de resíduos | 252,70 ms |
| Classificação (parte da descoberta) | 25,55 ms |
| Pico de memória de processo durante a sessão | 129,8 MiB |
| Entradas analisadas na descoberta | 2.000 |
| Fontes consultadas / inacessíveis | 12 / 2 |
| Possíveis atualizações catalogadas | 2 |
| Associações de drivers | 108 |
| Encerramento normal | Sim |

Os tempos não constituem comparação experimental controlada.

## Problema novo de confiabilidade do inventário

O registro desta execução mostra:
- `MSIX.LEITURA status=COMException(0x80040154)`;
- 0 pacotes MSIX enumerados; falha de leitura;
- Inventário total desceu de 74 (versão anterior, com 19 aplicativos MSIX)
  para 55 (somente ARP + bibliotecas).
- O ARP não relatou falhas de leitura; nenhuma desinstalação de 19 aplicativos
  está comprovada por esses números.
- A janela principal indicava inventário sem advertência persistente; a lógica
  de proteção de resíduos já recebia o sinal `Incomplete`.

**Correção da v5.13.11.21:** uma nova tentativa controlada somente para
`REGDB_E_CLASSNOTREG` (`0x80040154`), sem comandos externos, elevação,
PowerShell ou busca em outros usuários. Se falhar, o aviso `INVENTÁRIO PARCIAL`
permanece visível na lista, tooltip e registro, inclusive depois de consultar
fontes online. A ausência de pacotes não indica que foram desinstalados.
Testes sintéticos distinguem falha transitória, falha persistente, permissão
negada e propagação da parcialidade à proteção de resíduos.

## Próximo teste

Abrir o GF v5.13.11.21 no PC fraco em 1440×900, AVG ativo,
verificar no Registro do GF somente os campos `MSIX.LEITURA status`,
`repeticao` e `adicionados`. Confirmar se o inventário voltou a 74,
sem exigir exatamente 74 (o sistema pode mudar). Se ainda aparecer
`0x80040154`, confirmar o aviso persistente `INVENTÁRIO PARCIAL` e
repetir apenas o botão **Atualizar lista**. Não desinstalar, instalar,
limpar resíduos ou alterar drivers.

A solução de longo prazo para uma ativação WinRT permanentemente
indisponível exigirá diagnóstico isolado e sem dados pessoais; não presumir
que repetição sempre resolve a origem da falha.
