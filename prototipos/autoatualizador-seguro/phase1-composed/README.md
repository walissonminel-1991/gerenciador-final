# Fase 1 do autoatualizador — GF real em Windows descartável

Missão única em quatro fases. Este diretório é apenas uma ponte de medição entre os protótipos TUF/NTFS L1–L22 e os BYTES de pacotes reais do GF. Não modifica o código operacional do GF, nem cria um atualizador distribuível.

A CI compila o projeto GerenciadorAplicativos.csproj duas vezes com GfBetaProfile=local, variando apenas o metadado Version no MSBuild efêmero. Compilação nenhuma é publicada. O ensaio fica EXCLUSIVAMENTE sob RUNNER_TEMP em GitHub Actions Windows 2022.

O manifesto de todos os EXE/DLL/runtime do build NEW é assinado com as chaves públicas/determinísticas de TESTE já existentes em Stage11; python-tuf 7.0.1 verifica as assinaturas TUF, a referência temporal e os hashes de metadados. Essas chaves existem no repositório e NÃO autorizam atualizações de produção.

O ensaio rejeita modificação in-place de GerenciadorFinal.dll, revalida os bytes completos OLD/NEW e inicia a aplicação GF real com --tray na sessão descartável.

O observador Win32 **externo ao produto** consulta a identidade do executável associada ao PID por QueryFullProcessImageNameW, compara caminho e SHA256 da imagem de disco com o manifesto autenticado e procura a janela principal de título Gerenciador Final pertencente ao PID. Se encontrada, testa a resposta da thread UI por SendMessageTimeout(WM_NULL) e tenta encerramento gracioso por PostMessage(WM_CLOSE), aguardando a saída com limite temporal. Como o `MainForm.Shown` inicia o inventário assíncrono e o `FormClosing` pode cancelar fechamento durante `_busy`, o observador aguarda uma janela limitada de 10 segundos após encontrar o HWND e admite até dois pedidos `WM_CLOSE` separados por espera limitada. Isso não ignora o cancelamento do GF: se não houver saída normal após os pedidos, registra o bloqueio e mata exclusivamente a árvore criada no runner como **limpeza forçada, não como prova de shutdown**.

**NÃO declarar READY**: HWND, WM_NULL, PID e retorno 0 demonstram no máximo existência de janela/thread de mensagens e encerramento, não saúde dos motores do GF nem consentimento real em desktop de usuário. Identidade do executável observada via caminho+hash no disco não é atestação dos bytes carregados em memória e tem TOCTOU. GitHub Actions pode rodar na sessão 0 sem acesso ao desktop interativo. A CI deve explicitar session ID, acesso a desktop, presença de HWND, resposta WM_NULL, solicitação WM_CLOSE, saída graciosa e uso de fallback. Mesmo com qualquer sinal positivo, **a seleção retorna obrigatoriamente a OLD**.

Limites intencionais: ainda não há contrato operacional autenticado de encerramento completo/READY independente, recuperação de I/O pendente, confiança de chaves de produção ou restauração após falha física. Nenhuma modificação foi feita no Program.cs ou MainForm, nem na BETA distribuída, main, PR #65, backend, credenciais ou PCs pessoais. G1–G8 permanecem BLOCKED (0/8).

Critério para encerrar a Fase 1: demonstrar, no MESMO marco, saída/identidade/saúde verificável do GF real, autenticação do pacote autorizada para laboratório e recuperação conservadora, sem usar recibo local como autoridade suficiente. Não avançar para Fase 2 até que a evidência exista ou um bloqueio objetivo seja registrado.
