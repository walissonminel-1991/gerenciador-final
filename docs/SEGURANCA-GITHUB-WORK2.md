# Segurança GitHub — Missão Work 2 (20/10/2026)

## Estado observado

- `main`: `9ac3dc167b9e67ac50628749d5ad33c563c09376`, `protected: false`, proteção desativada, checks obrigatórios vazios (GET branches/main).
- Rulesets do repositório: `[]` (GET rulesets).
- `allow_auto_merge: false`; PR #40 aberta/Draft.
- Endpoint detalhado de proteção retorna 403 `Resource not accessible by integration`. A conta tem papel de administrador, mas essa integração não possui acesso administrativo suficiente. Nenhuma configuração administrativa foi alterada.
- CODEOWNERS cobre toda árvore, workflows e scripts com o proprietário. Sem proteção de branch/revisão obrigatória, o arquivo não impede merge e o autor não pode aprovar a própria PR para cumprir revisão independente.

## Correções na branch de desenvolvimento

Auditados os 43 workflows. Os 38 históricos ainda usavam tags móveis `@v4` (38 checkouts, 38 setup-dotnet e 55 uploads); foram fixados nos mesmos commits oficiais já usados pelos workflows atuais. Checkout deixa `persist-credentials: false` explícito em todos. SHAs confirmados em refs oficiais das Actions: checkout v7.0.1 `3d3c42e5aac5ba805825da76410c181273ba90b1`, setup-dotnet v6.0.0 `a98b56852c35b8e3190ac28c8c2271da59106c68`, upload-artifact v7.0.2 `cf430e030ddbb5b0abf93d22962f4752f3646cd9`. Os fluxos históricos continuam limitados às respectivas branches; não foram disparados artificialmente nem declarados executados.

Todos os workflows solicitam `contents: read`; não há permissão de escrita de código, Issues ou PRs. OIDC/attestations:write restritos ao job de atestação fora de PR e na main pública. Sem `pull_request_target`, eventos de Issues ou comandos interpolados a partir de entrada de participante. PR executa código/testes em runner hospedado sem credenciais persistentes; proprietário ainda precisa conferir política da plataforma de Actions.

Jobs atuais recebem nomes únicos para checks exigíveis: **Compilação Windows x64**, **Regressões Windows**, **Política de segurança e privacidade**. Guardrail Python valida todas as Actions fixadas e ausência de credenciais persistentes, inclusive nos workflows históricos.

Nenhum PackageReference de terceiros nos projetos auditados. Auditoria NuGet já habilitada para todas as severidades, com falha se a fonte de auditoria estiver indisponível; Dependabot semanal para NuGet/Actions preservado. Isso não comprova ausência de vulnerabilidades no runtime/Windows ou em instaladores externos.

## Configuração administrativa pendente para o proprietário

Em Settings → Rules → Rulesets, criar ruleset ativo para `main` (ou Branch protection equivalente):

1. Exigir Pull Request; bloquear pushes diretos e bypass, inclusive de administradores, conforme suporte/plano.
2. Exigir os três checks acima, selecionando origem GitHub Actions; exigir branch atualizada ou merge queue quando disponível.
3. Bloquear force push e exclusão da branch.
4. Exigir resolução de conversas, descartar aprovações obsoletas e exigir revisão do último push.
5. Exigir revisão de Code Owners e pelo menos uma aprovação independente. Em projeto mantido por uma pessoa, cadastrar antes outro revisor de confiança: aprovação do próprio autor não satisfaz essa regra.
6. Manter merge automático desativado. Não habilitar manutenção/merge automático por IA.

Em Settings → Actions → General: permissões padrão somente leitura; desativar capacidade de Actions criar/aprovar PRs; restringir Actions às necessárias e manter revisão obrigatória de workflows de contribuidores externos. O estado global dessas opções não pôde ser lido pela integração; o YAML restritivo não substitui essa configuração. Nenhum segredo/token foi enumerado ou exposto.

Essas regras só passarão a valer após configuração pelo proprietário. Esta missão não fez merge na main, não habilitou proteção administrativa nem publicou release.
