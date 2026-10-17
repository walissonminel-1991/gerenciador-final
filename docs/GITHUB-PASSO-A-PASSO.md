# Publicar o projeto no GitHub: passo a passo

## Antes de tornar público

1. Revise `LICENSE` e confirme que você pode licenciar **todo o código e recursos gráficos**. Esta versão propõe MIT e atribui copyright a “Gerenciador Final contributors”; altere se necessário antes de publicar.
2. Leia `SECURITY.md`, `PRIVACY.md` e `README.md`; não publique arquivos contendo informações confidenciais, CPF, e-mails privados, senhas ou credenciais.
3. Confira que `bin/`, `obj/`, `Distribuicao/`, `Instalador/`, `.cache/` e certificados (`*.pfx`, `*.p12`, `*.key`) não estão incluídos. Um `.gitignore` não remove segredos que já tenham entrado no histórico do Git.
4. Ative autenticação de dois fatores (2FA/MFA) em sua conta GitHub.

## Criar o repositório

1. Acesse https://github.com/new e escolha nome `gerenciador-final` (sugestão).
2. Selecione **Public**, não marque criação automática de README/licença/gitignore (o pacote já traz esses arquivos).
3. Instale Git for Windows, se necessário, ou use o GitHub Desktop para publicar a pasta.
4. Na pasta descompactada, em um terminal:

```powershell
git init -b main
git add .
git status
git commit -m "Publica Gerenciador Final v5.12.3 em codigo aberto"
git remote add origin https://github.com/SEU-USUARIO/gerenciador-final.git
git push -u origin main
```

Substitua `SEU-USUARIO` pelo nome real do perfil no GitHub. Git solicitará autenticação no próprio fluxo do provedor; **nunca inclua tokens na URL do repositório**.

## Depois de publicar

1. Ative 2FA, **secret scanning** e **Private vulnerability reporting**, quando disponíveis, nas configurações do GitHub.
2. Vá a **Actions** e confira o workflow `Compilar Windows (sem assinatura)`; ele só gera artefato de testes e não cria release.
3. Abra uma Issue com a lista de verificações de segurança e registre os resultados de execução no Windows.
4. Após revisão do projeto, faça uma versão oficial em **Releases** com fonte, notas de versão e hashes SHA-256. Se decidir oferecer instalador não assinado inicialmente, explique claramente o risco de alertas e **não instrua o público a ignorar o antivírus**.
5. Publique capturas reais, instruções para leigos, correções documentadas e mantenha o histórico de mudanças. Isso é relevante para a avaliação de reputação pela SignPath.
6. Só solicite a SignPath quando cumprir os critérios detalhados em `docs/SIGNPATH.md`.

## Links úteis

- Criar repositório: https://github.com/new
- Guia oficial de publicação: https://docs.github.com/en/migrations/importing-source-code/using-the-command-line-to-import-source-code/adding-locally-hosted-code-to-github
- Documentação de segurança: https://docs.github.com/en/code-security/getting-started/quickstart-for-securing-your-repository
