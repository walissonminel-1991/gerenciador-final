# GERENCIADOR FINAL — DRIVERS UX4 — TRIAGEM REAL CONSULTIVA PC FRACO

Data: 17/11/2026. PR #80 Draft; base UX3 previamente aprovada: `dc15544532cc1cef745909d7c8bd374301030ddc`.

## Escopo implementado
- A consulta WMI já existente de `Win32_PnPEntity` passa a incluir `PNPClass`. Nenhuma segunda consulta, download, registro do Windows, serviço, instalação ou privilégio novo. Classes vazias, inválidas ou contraditórias entre linhas da mesma instância são tratadas como desconhecidas; contradições causam `Partial=true`.
- A fotografia estável (`DriverInventorySnapshot`) compara classe PnP além de presença, INF, DeviceId e demais evidências. A inclusão de dispositivos com problema PnP sem `Win32_PnPSignedDriver` mantém o vínculo por DeviceId.
- `DriverCandidateTriage` classifica **somente como orientação**, não como direito de atualização: classe crítica excluída; MEDIA/HIDClass/Camera/AudioEndpoint somente consulta; Printer/Image/Ports grupo restrito para investigação, jamais automaticamente elegível.
- O relatório local `DriverReadOnlyReport` exibe a triagem para dispositivo selecionado: classe e presença relatadas pelo PnP, versão/fornecedor/assinatura relatados por WMI, INF, resultado Driver Store, contagem de INFs publicados sem dispositivo observado, duplicação de DeviceId no inventário integral, HardwareID compartilhado e estado de consulta WUA.
- WUA sem consulta, fonte parcial, fonte online não comprovada, zero ofertas, ofertas sem vínculo e oferta exata recebem rótulos distintos. A versão numérica do pacote ofertado, ranking e autenticidade do pacote remoto seguem **NÃO COMPROVADOS**.
- A lista virtualizada adiciona a classe PnP na descrição acessível e tooltip sem aumentar altura dos cartões. O resumo anônimo/copiar contagens **não** inclui identificadores, classes por dispositivo, nomes, fornecedor ou INF. O relatório detalhado é somente LOCAL.
- `DriverUpdatePilot.TryPrepare`, `AllowedClass`, WUA backend, journal e instalador permanecem **inalterados**. MEDIA/HIDClass permanecem bloqueadas para instalação.

## Laboratório futuro do PC FRACO — matriz de risco
| Classe/uso | Exemplo | Permissão UX4 | Instalação posterior |
| --- | --- | --- | --- |
| Printer | impressora USB dedicada | consulta WMI, INF, Store e WUA | somente com hardware presente, pacote oficial assinado, gates e restauração |
| Image | câmera USB com associação existente | consulta, verificar PnP/INF | candidata somente após validação separada |
| Ports | porta serial COM USB separada | consultar identidade e presença | candidata somente após validação separada |
| MEDIA | headset/interface áudio USB | apenas consulta e simulação | **bloqueada** no piloto atual |
| HIDClass | controle de jogo separado | apenas consulta e simulação | **bloqueada** no piloto atual |
| Camera | webcam que use classe PnP Camera | somente consulta até análise da classe WUA | não presumir Image ou compatibilidade |
| Impressora virtual | pacote de driver e serviço spooler | apenas consulta | requer análise de efeitos em spooler e plano de reversão |
| Firmware/System/Display/Net/Storage/USB essencial | UEFI, GPU, rede, disco etc. | diagnóstico consultivo | **excluídas** |

## Roteiro de homologação física posterior — NÃO EXECUTADO
1. Obter candidata portátil privada identificada por SHA, aprovada por segurança e integridade; testar **somente consulta** e interface no PC FRACO antes de selecionar pacote.
2. Preservar uma imagem completa do PC FRACO e comprovar recuperação **offline** a partir dessa imagem. Ponto de restauração não equivale a backup completo.
3. Escolher dispositivo físico externo não essencial (impressora, câmera USB ou serial), conferir PNPClass, HardwareID exclusivo, INF, classe WUA, fornecedor, assinatura real, versão e arquitetura.
4. Obter pacote oficial assinado compatível; **não forçar downgrade incompatível**. A instalação de pacote no Driver Store sem hardware não prova atualização.
5. Executar consulta WUA oficial e registrar resultado. Consulta sem ofertas não prova que o driver está atualizado. Oferta com hardware exato não prova DriverVer remoto superior.
6. Qualquer instalação/remoção/reinício requer **autorização específica posterior** para dispositivo/pacote/método e comprovação de recuperação.
7. Nunca converter simulação em prova de atualização real ou mudar as políticas para MEDIA/HIDClass apenas porque parecem simples.

## Testes de segurança a exigir no SHA final
- Windows Server 2022 e 2025: G1.9 24/24 em cada; inventário/testes de identidade, INF, PnP e WUA.
- Windows 11 ARM64 F4.2, política de segurança e privacidade, consentimento BETA sem rede, 36/36 mutações, regressões G1.10–G1.23, WinForms de 500×340, 790×510, 1440×900/2560×1440, DPI, compilação Windows x64 e integridade externa **completed/SUCCESS no mesmo SHA**.
- Testes adversariais: classe mista/duplicada, ID de dispositivo ambíguo, pacote sem hardware, PnP sem vínculo, WUA parcial/desconhecido/zero ofertas, HardwareID compartilhado, classe crítica com oferta WUA, nenhuma autoridade nova.
- Preservar UX3: botão Simular independente e 17 cenários fictícios.

## Preservações e bloqueios
UX4 é triagem, NÃO atualização de driver. Nunca criar release, merge ou alterar main, PR #79, G5, D1, Worker, Cron, Secrets, BETA, PC FORTE ou PC FRACO. Roadmap 27/42; G1–G5 continuam abertos. Instalação real permanece **NO-GO**. Nova etapa possível: preparar candidata portátil **privada** e inspecionar somente leitura do PC FRACO, após autorização específica.
