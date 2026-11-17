# GERENCIADOR FINAL — F4 — Harness de homologacao WUA em VM

Data 14/11/2026. PR #80 Draft, base F3 SHA 0af26af9a380c8c8e366fb8d6b83f26e9df1e2cd, Windows 43660312215 e integridade 43660312142 completed/SUCCESS.

## Entrega de codigo
- Motor piloto recusa HardwareID repetido em mais de uma instancia PnP presente. A oferta WUA unica nao prova alvo fisico unico.
- Backend WUA exige DeploymentAction=Installation, CanRequireSource=false, somente uma WindowsDriverUpdateEntry e correspondencia exata de HardwareID, Provider e DriverClass. Recusa sem metadata, EULA nao aceita, pacote com bundle, classe critica, instalada/oculta/beta.
- Executor TestesDrivers/DriverWuaF4VmHarness.cs COMPILA SOMENTE NO PROJETO DE TESTES, nao no GerenciadorFinal.exe. Modos '--f4-vm-probe' (consulta sem download/Install) e '--f4-vm-install' (somente VM reconhecida por BIOS, admin e console interativo).
- Antes de consultar no modo install, exigir atestado manual de snapshot externo; depois escolha manual da oferta e confirmacao do GUID/revisao/hash curto do DeviceId e frase especifica de instalacao. Isso nao substitui comprovacao externa de snapshot/isolamento.
- Fluxo real usa DriverUpdatePilot.Execute, WUA backend e journal separado de historico de leitura v1. Reboot apenas sinalizado; falhas nao autorizam repeticao/rollback automatico. Evidencias locais resumidas em JSON sem DeviceId original nem titulos de aplicativos. Nenhum envio remoto.
- Fixtures TestesDrivers/DriverWuaF4VmHarnessTests.cs e novos controles no DriverUpdatePilotTests sao estritamente sinteticos, sem WMI, Windows Update ou instalacao.

## Restricoes de homologacao
Nenhuma VM com snapshot comprovado e oferta real disponivel nesta sessao. NAO foi feita instalacao/download nativos. Mesmo com CI verde, estado fisico e NO-GO ate teste real controlado. SMBIOS pode ser falsificado e nao garante isolamento. Pode nao haver ofertas oficiais Image, Printer ou Ports; ausencia nao deve motivar instalar drivers alternativos.

## Procedimento da primeira VM
1. Operador confirma VM Windows descartavel, sem passthrough de hardware pessoal, restauracao de snapshot externo testada, conta administradora, Windows Update disponivel e logs locais sem dados privados.
2. No terminal DA VM, obter o SHA F4 especifico desta PR e executar: dotnet run --project TestesDrivers/TestesDrivers.csproj -c Release -- --f4-vm-probe . Se VM nao reconhecida ou nao houver oferta elegivel, NO-GO.
3. Restaurar snapshot independente e preparar repeticao controlada; depois, somente dentro da VM, executar: dotnet run --project TestesDrivers/TestesDrivers.csproj -c Release -- --f4-vm-install . Digitar frase de snapshot, numero, UpdateID, revisao, hash do DeviceId e frase de consentimento.
4. Verificar journal e evidencia local, consultar Win32_PnPSignedDriver antes/depois, conferir hardware e estado, testar falha/download, reinicio pendente, retorno parcial, reentrada, reversao via snapshot externo; nao repetir automaticamente.
5. Se comprovado, anexar evidencias sanitizadas ao checkpoint e avaliar release pessoal separadamente. Nao afirmar versao numerica remota maior apenas por titulo/data.

## Gate
CI Windows 2022/2025 24/24 G1.9 por runner, G1.10-G1.23, F1-F4, 36/36 mutacoes, seguranca, privacidade, consentimento, WinForms, x64 e integridade mesmo SHA. Nenhuma fixture chama modo VM. Checkpoint antes da CI, comentario final somente depois de completed/SUCCESS em ambos.

Proibicoes: main, PR #79, G5 autoatualizador, D1, Worker, Cron, Secrets, BETA, releases, PC FORTE/FRACO. Draft sem merge. Roadmap 27/42 G1-G5 abertos.