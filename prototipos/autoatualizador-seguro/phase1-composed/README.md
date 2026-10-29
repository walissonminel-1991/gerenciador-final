# Fase 1 do autoatualizador — GF real em Windows descartável

Missão única em quatro fases. Este diretório é apenas uma ponte de medição entre os protótipos TUF/NTFS L1–L22 e os BYTES de pacotes reais do GF. Não modifica o código operacional do GF, nem cria um atualizador distribuível.

A CI compila o projeto GerenciadorAplicativos.csproj duas vezes com GfBetaProfile=local, variando apenas o metadado Version no MSBuild efêmero. Compilação nenhuma é publicada. O ensaio fica EXCLUSIVAMENTE sob RUNNER_TEMP em GitHub Actions Windows 2022.

O manifesto de todos os EXE/DLL/runtime do build NEW é assinado com as chaves públicas/determinísticas de TESTE já existentes em Stage11; python-tuf 7.0.1 verifica as assinaturas TUF, a referência temporal e os hashes de metadados. Essas chaves existem no repositório e NÃO autorizam atualizações de produção.

O ensaio rejeita modificação in-place de GerenciadorFinal.dll, revalida bytes completos OLD/NEW e tenta executar de modo limitado a aplicação GF com --tray na sessão descartável, depois encerra os PIDs criados no runner. ATENÇÃO: PID não equivale a saúde do WinForms. Por isso o seletor retorna a OLD obrigatoriamente e não há alegação de READY nem de instalação autorizada.

Limites intencionais: não existe ainda contrato autenticado de encerramento completo, READY independente, recuperação real de I/O pendente, confiança de chaves de produção ou restauração após falha física. O processo pode não abrir a UI em sessão de serviço Windows. Os testes não alteram BETA distribuída, main, PR #65, infra, credenciais ou PCs pessoais. G1–G8 permanecem BLOCKED (0/8).

Critério para encerrar a Fase 1: demonstrar, no MESMO marco, saída/identidade/saúde verificável do GF real, autenticação do pacote autorizada para laboratório e recuperação conservadora, sem usar recibo local como autoridade suficiente. Não avançar para Fase 2 até que a evidência exista ou um bloqueio objetivo seja registrado.
