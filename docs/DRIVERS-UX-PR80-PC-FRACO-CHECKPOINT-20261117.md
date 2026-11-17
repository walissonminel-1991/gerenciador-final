# GERENCIADOR FINAL — Drivers UX / PC FRACO — checkpoint

Data: 2026-11-17. PR #80 Draft; base PR #79 G1.23, roadmap 27/42; G1–G5 continuam abertos.

## Mudanças da etapa
- Criado filtro `Sem driver`: apenas registros com `WithoutSignedDriverAssociation=true`. Rótulo abreviado para leigos, mas descrição explica que ausência de linha WMI de driver assinado **não** significa falta de driver, necessidade de instalação ou pacote removível.
- Filtros existentes `Todos`, `Presentes`, `Problemas`, `Não confirmados` preservados; contagens são acessíveis por descrição e tooltip. `Problemas` usa apenas `HasPnpProblem`.
- Em painéis estreitos (<760 px) ou baixos (<230 px), seletor compacto apresenta os cinco filtros; botões de cabeçalho recolhidos, e a área da lista continua prioritária. O mesmo filtro acompanha o redimensionamento.
- Botão `Copiar resumo` disponível apenas após publicação válida. Copia para clipboard local **somente números e rótulos fixos**: total de linhas, parcialidade, falhas PnP, linhas WMI sem associação, presença não confirmada e assinatura WMI indeterminada. Não lê nem inclui Status WMI, nomes, IDs de instância/hardware, fornecedor, INF, caminhos ou pesquisa.
- Ao iniciar nova leitura, bloqueia cópia e investigação anteriores até novo resultado coerente; consulta cancelada/falha não reabilita cópia da fotografia revogada.
- Nenhum novo caminho de Download/Install, EULA, reboot, limpeza, mudança de driver ou envio de diagnóstico.

## Testes automatizados
- Fixtures WinForms com leitura injetada, botão de resumo inicial desabilitado, habilitação após publicação e parcialidade consultiva; filtro vazio não inventa driver ausente; opções compactas e variações 500x210, 540x320, 1200x720 e 1450x820.
- Teste do resumo sem identificadores injeta strings-sentinela em DeviceId, nome, fornecedor, INF, HardwareID e Status; nenhuma deve constar no resumo.
- Matriz visual existente percorre 1440x900 e demais resoluções, testes Windows 2022/2025, G1.9 24/24, G1.10–G1.23, F1–F4.2, mutações, consentimento, segurança, x64 e integridade exigidos **no SHA final**. Não afirmar aprovação antes dos runs concluídos.

## PC FRACO — roteiro de homologação posterior, sem instalação
1. Obter ZIP portátil identificado pelo SHA aprovado, validar SHA256SUMS e não instalar versão bloqueada pelo antivírus.
2. Fazer captura das abas em 1440×900 e janela até 60% da tela; conferir nome, botões e rolagem vertical em centenas de dispositivos.
3. Abrir Drivers, verificar que `Copiar resumo` inicia desabilitado e nenhum filtro dispara WUA.
4. Clicar `Consultar drivers`, conferir status, falhas e contagem total, sem travar a interface.
5. Testar os cinco filtros e pesquisa, inclusive `Sem driver` e alternância de janela larga/estreita.
6. Copiar resumo, conferir que **não tem IDs, nomes, fornecedores, INF, caminhos ou dados pessoais**; enviar somente esse texto voluntariamente.
7. Investigar um dispositivo sem driver associado e um dispositivo normal; nenhuma tela pode executar instalação/remoção.
8. Iniciar e cancelar consulta, observar bloqueio da cópia/investigação antiga e preservação da lista anterior. Não forçar encerramento do processo WMI em andamento.
9. Verificar que o piloto WUA na distribuição normal permanece com `Instalação bloqueada`.
10. Registrar capturas de tela, tempos aproximados, mensagens de erro e comportamento de acesso por teclado.

**Nenhum teste de instalação real está autorizado pelo roteiro.** A primeira instalação/desinstalação de driver, mesmo no PC FRACO, depende de backup/restauração comprovados, classe não crítica e autorização posterior distinta. Não converter simulação em homologação de driver real.

## Preservações e pendências
PR #79, main, G5, D1, Worker, Cron, Secrets, BETA, releases, PC FORTE e PC FRACO não alterados. Os bloqueios oficiais WUA e a ausência de prova de versão ofertada continuam. Não declarar G1–G5 concluídos pelo êxito desta etapa de UX.
