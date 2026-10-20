# Gerenciador Final v5.12.9 TESTE — motor multifontes seguro

Versão experimental derivada da v5.12.8, mantendo a interface escura e o sistema de atualização supervisionada.

## Três motores trabalhando juntos

1. **RuckZuck** — consulta HTTPS GET do catálogo público, comparação conservadora, 10 minutos de cache exclusivamente em memória.
2. **GitHub Releases oficial** — apenas repositórios previamente definidos em código; consulta `/releases/latest` para OBS Studio, Notepad++, Microsoft PowerToys e PowerShell 7. Cada identificação verifica nome e fabricante antes de qualquer consulta.
3. **Mozilla Product Details** — consulta `firefox_versions.json` para Firefox Stable (não ESR, Beta ou Nightly). Inclui apenas versões numéricas estáveis.

A consulta oficial do **Windows Update** permanece separada e continua executada na inicialização.

Todos os motores HTTPS são **somente leitura**: não abrem terminal, não executam scripts remotos, instaladores nem COM. As URLs consultadas são estáticas. A lista de programas do computador nunca é enviada aos provedores; estes recebem apenas os dados normais da conexão HTTPS, como endereço IP.

## Sem resultados conflitantes

- Cada programa instalado tem uma única linha e no máximo um candidato de atualização.
- Fontes oficiais reconhecidas por nome e fabricante têm precedência sobre a versão genérica RuckZuck para o mesmo programa.
- A versão estável oficial pode também **anular** um falso positivo de um catálogo desatualizado.
- Se uma fonte falha, os resultados das outras continuam válidos. Erros são anotados de modo isolado, sem nomes de programas nem dados pessoais.
- As versões antigas do Visual C++ 2013 x64/x86 continuam tratadas por arquitetura; registros antigos são exibidos sem reinstalação repetitiva.
- Fonte aparece no cartão do programa: RuckZuck, GitHub oficial ou Mozilla.
- Somente o botão **Verificar atualizações** solicita dados novos; as consultas automáticas podem usar cache em memória de dez minutos.
- GitHub é limitado a duas conexões simultâneas e os resultados ficam em cache para reduzir chances de HTTP 403/429 e tráfego excessivo.

## Atualizar programas

Adicionar novos motores **não** autoriza executá-los. O botão **Atualizar** só utiliza receitas previamente assinadas e validadas, existentes para Visual C++ 2013 e OBS Studio 32.2.2. As novas fontes não geram downloads ou comandos arbitrários; para novos aplicativos reconhecidos existe apenas o botão **Site** com endereço oficial fixo. UAC e confirmações legítimas do Windows permanecem ativos.

## Testes automatizados no Windows GitHub Actions

- **19/19** testes novos: fusão de versões divergentes, ausência de duplicatas, identificação por fornecedor, exclusão ESR, fontes indisponíveis, rejeição de prereleases, domínios bloqueados, cancelamento, TTL e cache com 12 consultas simultâneas.
- **14/14** testes do catálogo RuckZuck: cache e desempenho (142 instalados e 631 entradas), concorrência, JSON inválido, HTTP 429 e cancelamento.
- **18/18** testes históricos de matching e regressão Visual C++.
- **12/12** testes de receitas de instaladores oficiais.
- Consultas **reais, somente GET**: Mozilla, OBS, Notepad++, PowerToys e PowerShell retornaram status `ok` no CI, com durações entre 140 e 474 ms. RuckZuck retornou 631 entradas.
- Download sem execução: assinaturas Microsoft Visual C++ x86/x64 verificadas; SHA-256 e assinatura do OBS verificados.
- Compilação e ZIP portátil Windows x64 aprovados.

Os tempos observados são **exclusivos do Windows do GitHub**; não são previsão do tempo de consulta no seu computador.

## Como testar sem modificar programas

1. Baixe o ZIP do artefato no GitHub Actions e extraia em uma pasta nova, com AVG ligado.
2. Abra o programa e não clique imediatamente em Verificar: aguarde as buscas automáticas.
3. Confira a contagem de candidatos e observe o texto de origem em cada cartão.
4. Clique uma única vez em **Verificar atualizações**; confira no registro o status e a duração por fonte.
5. Alternar entre **Todos** e **Com atualização** não deve duplicar programas.
6. Não instale nem desinstale nada neste primeiro teste.
7. Se uma fonte falhar, confirme que as demais ainda aparecem; envie o diagnóstico local `%LOCALAPPDATA%\GerenciadorFinal\Diagnosticos\GerenciadorFinal-Diagnostico-v5.12.9.log`.

**Limitações:** o catálogo dos três motores não abrange todos os programas do Windows, nem todos os fabricantes publicam versões na mesma fonte. A ausência de um aplicativo na lista de candidatos não garante que ele esteja atualizado. O AVG e o SmartScreen podem avaliar o executável portátil não assinado independentemente do funcionamento dos motores; não desative proteções para testar.
