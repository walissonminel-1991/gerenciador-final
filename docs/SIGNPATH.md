# Preparação para SignPath Foundation (ainda sem aprovação)

A SignPath Foundation pode oferecer assinatura Authenticode **gratuita para projetos OSS selecionados**. **Não é uma assinatura individual em nome de CPF**, nem está garantida a aceitação de projetos novos.

## Critérios oficiais a conferir

- Código-fonte e componentes do projeto sob licenças de código aberto aprovadas pela OSI, sem conteúdo proprietário incompatível.
- Aplicativo documentado, já lançado na forma pretendida, mantido ativamente e sem recursos de malware, PUA ou de exploração de vulnerabilidades.
- Reputação pública verificável e identidade de mantenedores e equipe de assinatura definida.
- GitHub com MFA ativado e revisão de contribuições externas.
- Build automatizado em runner hospedado pelo GitHub, com histórico e artefatos de origem verificável; para OSS, o conector da SignPath exige origem confiável.
- Metadados de nome e versão consistentes; aprovação **manual** para cada assinatura.
- **Code signing policy** publicada na página inicial e páginas de downloads/releases, com crédito à SignPath **somente depois da aprovação**.
- Sem criação de exceções no antivírus, desativação de proteções ou manipulação insegura do sistema.

Os critérios completos e atuais são definidos pela própria fundação: https://signpath.org/terms.html

## Situação deste pacote

- Fonte C# e recursos gráficos incluídos, licença MIT **proposta para validação pelo titular**.
- Workflow GitHub Actions que publica uma build **não assinada**, permitindo estabelecer um histórico de compilação pública.
- O instalador Inno Setup ainda é gerado por um fluxo **local que exige certificado próprio** (`Criar-Instalador-Windows-x64.bat` e `Assinar-GF.cmd`). Ele **não está integrado à SignPath**.
- A política final de revisão, configuração de artefatos e assinatura SignPath deverá ser adicionada **após aprovação e recebimento dos identificadores reais do projeto**, sem colocar tokens no código.
- Os alertas anteriores do VirusTotal devem ser documentados e investigados; aprovação da SignPath não é mecanismo para ignorar detecções.

## Sequência recomendada

1. Publicar repositório GitHub, a documentação e o histórico de releases.
2. Compilar usando GitHub Actions e testar o artefato resultante num Windows isolado; validar funções administrativas, instalação e desinstalação quando houver instalador de teste.
3. Revisar avisos de terceiros e segurança, abrir canal de relatos privados, construir manutenção e reputação pública.
4. Abrir https://signpath.org/apply.html e enviar as informações do repositório. Usar `docs/MODELO-INSCRICAO-SIGNPATH.md` como rascunho.
5. Se aprovado: instalar aplicativo/conector oficial no GitHub, configurar projeto, artifact configuration, assinatura de binários próprios, approval policy, branch rules, token em GitHub Secrets e workflow de múltiplas etapas para assinar EXE/DLL **antes** de empacotar instalador e então assinar o instalador. Validar que o Inno Setup e a assinatura do desinstalador estão abrangidos pela política aprovada.
6. Registrar política de assinatura efetivamente aprovada no README e em cada Release.

**Não inclua o token SignPath em commits ou arquivos compartilhados.**

Fontes oficiais:
- https://signpath.org/
- https://signpath.org/apply.html
- https://signpath.org/terms.html
- https://docs.signpath.io/trusted-build-systems/github
- https://docs.signpath.io/projects
