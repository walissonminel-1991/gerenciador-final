# Auditoria do GERENCIADOR FINAL — v5.13.11.4 TESTE

Data: 20/10/2026. Base revisada: ec8c1ce7957adeebe89a635dfb501b432693613b (PR #40); main na candidata v5.13.11.0. A auditoria combina inspeção estática das áreas abaixo, conferências estruturais e regressões no Windows. Não é prova de ausência de bugs, pentest nem validação nos computadores do usuário.

## Achados corrigidos

| Achado | Impacto | Correção e evidência |
| --- | --- | --- |
| Comandos Windows chamados apenas pelo nome | Procura por executáveis homônimos no diretório de execução/PATH, inclusive na tarefa elevada | WindowsSystemTools vincula nove ferramentas permitidas à pasta de sistema; AppServices e acesso ao Gerenciador de Tarefas usam esse caminho; TestesSeguranca valida caminhos sem executar comandos administrativos |
| Reconsulta da limpeza ampliava alvos depois do diálogo | Nova pasta/chave de uma política podia ser incluída sem aparecer na confirmação original | Interseção entre alvos aceitos e os revalidados, ainda sujeitos às políticas e à verificação de instalação; TestesResiduos cobre alvos novos e desaparecidos |
| Relatório expirado podia ser usado se a exclusão falhasse | Retenção não era uma condição independente da revisão | Validade conferida antes de ler para compartilhar; teste Windows mantém arquivo aberto sem permissão de exclusão e exige bloqueio da revisão |
| Sessão sem eventos sobrescrevia a pendência anterior | Perda do resumo ainda útil da sessão passada | Sem eventos novos, preservar a pendência válida; TestesTelemetria verifica conteúdo anterior |
| Reaceitar consentimento ativo apagava contagens | Abrir privacidade e aceitar novamente perdia ocorrências da sessão | Confirmação idempotente preserva eventos; regressão específica |
| Consentimento aceitava campos extras ou duplicados | Estado ambíguo podia habilitar coleta | Esquema fechado de dois campos numéricos; testes com duplicatas, tipos errados, overflow e esquema diferente |
| Expressão de versão aceitava quebra de linha final | Validação de versão não era exata | Âncora de fim absoluto e regra única compartilhada entre serialização e formulário; teste específico |
| Resumo do roadmap 30/45 divergente da lista | Indicador de progresso inexato | Contagem atual: 27/42 (64,3%); 30/45 mantido apenas como referência histórica; não é perda de funcionalidades |
| Evidência do roadmap apontava tabela de processos excluída da compilação | Documento descrevia implementação antiga | Evidências passam a citar amostragem de RAM e controles atuais; uso do Gerenciador de Tarefas fica explícito |

## Áreas verificadas

- Estado remoto da main e PR, histórico recente e resultados dos dois workflows atuais.
- Gatilhos de Actions, compilação/publicação, manifesto e identidade da versão.
- Referências locais de projetos e recursos, sintaxe dos catálogos JSON e links relativos de README/PRIVACY/docs.
- Ciclo de consentimento, coleta agregada, retomada, expiração, aviso público e geração da URL GitHub.
- Fluxos de desinstalação e revalidação de inventário; políticas de limpeza e proteção de links/junções.
- Lista de comandos administrativos; identidade, hash, limites de download e assinatura nos atualizadores verificados.
- Inventário MSIX, amostragem de RAM, consulta/histórico de drivers e correspondência com as afirmações do roadmap.
- Busca textual por credenciais óbvias no código ativo; não substitui análise completa de segredos no histórico git.

## Conferências e testes

As conferências locais não encontraram referências de projetos/recursos ou links relativos ausentes; catálogos JSON foram analisados e projeto/manifesto sincronizados na v5.13.11.4. As suítes TestesSeguranca, TestesResiduos e TestesTelemetria incluem os novos cenários. A validação remota executa toda a bateria consolidada e publica um pacote cuja identidade e ativação SxS são verificadas no Windows. O resultado e links da revisão final ficam registrados no PR #40; a auditoria não usa resultados de uma revisão antiga como prova do novo código.

## Limitações e pendências

- AVG/IDP.Generic, fluidez, DPI, cobertura e alterações reais nos dois PCs continuam pendentes. A CI não reproduz esses ambientes.
- Instalações/desinstalações reais, assinaturas atuais de todos os pacotes e comparação ao vivo com WinGet não foram executadas nesta auditoria. Receitas são limitadas a versões auditadas; novas fontes consultivas não autorizam instalação automática.
- O timeout da consulta WMI é observado entre etapas/linhas; uma chamada COM bloqueada pode ultrapassar esse prazo. Isso exige análise específica de cancelamento/isolamento e teste de falha WMI.
- O programa de teste ainda exige conseguir iniciar o log local para abrir; a falha é exibida e interrompe a inicialização, conforme a instrumentação atual.
- Consentimento/diagnóstico são locais. Ainda não existe envio automático, análise por IA ou descarte garantido de Issues públicas. URL do formulário transmite resumo ao GitHub e pode permanecer no histórico do navegador.
- Gestão de drivers (instalar/reverter) continua não implementada. Assinatura WMI não é validação criptográfica independente de todos os pacotes.
- Instalador legado Inno Setup conserva versão antiga e exige certificado próprio; é descrito como legado no README e não foi usado para o pacote portátil desta revisão.
- Há PRs históricos abertos. Esta auditoria não integra nem encerra automaticamente essas branches. Correções do PR #40 só passam à main após integração.
