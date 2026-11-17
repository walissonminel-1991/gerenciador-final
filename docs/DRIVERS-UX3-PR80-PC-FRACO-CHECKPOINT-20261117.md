# GERENCIADOR FINAL — DRIVERS UX3 — LABORATÓRIO MULTICLASSES PC FRACO
Data: 2026-11-17. Base aprovada UX2 `df3bd7aceb9efc5ec052f1378891e8f6d7eddc51`. PR #80 Draft, sem merge.

## Entrega
- Botão **Simular** disponível diretamente na aba Drivers antes de qualquer inventário, dispositivo real ou oferta WUA.
- Janela independente com **17 cenários inteiramente fictícios**, apenas transformação de dados fixos em texto. Não recebe inventário, WUA, COM, PnP, SetupAPI, backend, journal ou rede.
- Famílias Printer, Image, Ports, MEDIA, HIDClass. Cenários de INF no Store sem dispositivo, dispositivo sem vínculo WMI, consulta WUA não realizada/sem ofertas/parcial, DeviceId ambíguo, EULA, revalidação, consentimento, download, cancelamento, journal, reinício pendente e verificação inconclusiva.
- Cada etapa está rotulada **SIMULADO** ou **NÃO EXECUTADO**; um desfecho favorável é **VERIFICADO SOMENTE NA FICÇÃO**. Não se escreve histórico ou journal, não há download e não ocorre qualquer alteração.
- O botão continua acessível no layout compacto de 500×210; `Copiar resumo` permanece oculto nas telas estreitas; clique repetido não cria segunda janela.
- Consulta WUA: distinguir consulta parcial, zero ofertas observadas e ofertas sem correspondência, sem inferir atualização mais recente ou compatibilidade.
- A política `DriverUpdatePilot.TryPrepare` permanece inalterada, inclusive a restrição real Printer, Image e Ports; MEDIA/HIDClass só para diagnóstico.

## Matriz futura de homologação real (PC FRACO)
| Classe | Exemplo | Pacote Driver Store | Dispositivo e atualização real |
|---|---|---|---|
| Printer | Impressora USB dedicada | Pacote sem hardware é apenas evidência de armazenamento | Exige impressora real, identidade PnP exclusiva, assinatura, oferta WUA e restauração |
| Image | Webcam USB externa | Pacote não comprova associação | Candidata apenas com equipamento real e gates existentes |
| Ports | Adaptador serial USB externo | Pacote não implica COM ativa | Candidata com dispositivo real e gates |
| MEDIA | Headset/interface de áudio USB | Somente diagnóstico de pacote | Não autorizada para instalação pelo piloto |
| HIDClass | Controle de jogos USB separado | Somente diagnóstico de pacote | Não autorizada para instalação pelo piloto |
| Impressora virtual | Driver de impressão virtual | Pode interferir no spooler mesmo sem periférico | Somente leitura no primeiro ciclo |
| System/Firmware/Net/Display/Storage/USB essencial | Hardware crítico | Não utilizar para provocar atualizações | Excluídos |

## Pré-condições obrigatórias antes de testes físicos
1. Inventariar PC FRACO por consultas somente leitura e preservar apenas dados sanitizados em evidência compartilhável; não expor IDs de hardware ou caminhos de usuário.
2. Criar **imagem de disco completa com restauração comprovada**, meio de recuperação offline e plano de retorno. Ponto de restauração e botão Reverter Driver são auxílios, não garantias.
3. Selecionar dispositivo NÃO crítico, separado das funções de vídeo, rede, armazenamento, áudio integrado, teclado/mouse/HID essenciais e firmware.
4. Confirmar fabricante, HardwareID, INF oficial assinado, versão/arquitetura do Windows e integridade do pacote. Não forçar versões antigas incompatíveis ou obtidas de fontes duvidosas.
5. Diferenciar preparar driver no Driver Store de instalar associação ativa. A primeira ação não demonstra oferta real WUA. Instalar versão antiga não garante oferta mais recente.
6. Autorizar separadamente dispositivo, pacote e mudança real somente após prova de recuperação, sem automatizar reboot.
7. Nunca chamar simulação de homologação real. Sucesso sintético demonstra somente comportamento do modelo de UI.

## Testes e gates
Testes de casos fictícios não dependem de hardware. Testes WinForms abrem Simular antes de consultar, verificam que não incrementa a leitura e que o status não muda, impedem janela duplicada, verificam contraste com consulta real e largura compacta. Exigir CIs Windows Server 2022/2025, G1.9 24/24 em cada runner, G1.10–G1.23, F1–F4.2 ARM64, 36/36 mutações, testes de layout 1440×900/2560×1440, política, consentimento, x64 e integridade **no mesmo SHA**.

## Preservações
NÃO autoriza instalar, remover ou reiniciar drivers, não usa Azure nem computadores pessoais, não modifica main, PR #79, G5, D1, Worker, BETA, Cron, Secrets, releases. Roadmap 27/42; G1–G5 abertos.
