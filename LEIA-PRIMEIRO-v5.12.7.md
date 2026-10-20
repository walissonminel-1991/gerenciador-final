# Gerenciador Final v5.12.7 TESTE — primeiro atualizador integrado

Esta é uma versão experimental, construída sobre a v5.12.6.3, que preserva as consultas automáticas ao RuckZuck, Windows Update e histórico dos processos, além dos painéis existentes.

## Funcionamento da aba Aplicativos
- **Atualizar** (verde) está disponível somente quando há receita de atualização previamente conferida, para o pacote e versão exatos. O botão pede confirmação **antes de baixar ou executar**.
- **Site** (cinza) é a alternativa discreta; abre um endereço oficial definido no código, somente ao clicar. Nenhuma URL é executada a partir de metadados RuckZuck.
- **Desinstalar** mantém o comportamento anterior, com confirmação.
- Pacotes sem receita de atualização confiável mostram Site (se houver), mas não um botão Atualizar inoperante.

## Primeiras receitas de atualização real
- Microsoft Visual C++ 2013 Redistributable x64: 12.0.40664.0 via `https://aka.ms/highdpimfc2013x64enu`.
- Microsoft Visual C++ 2013 Redistributable x86: 12.0.40664.0 via `https://aka.ms/highdpimfc2013x86enu`.
- OBS Studio 32.2.2 x64: release `obsproject/obs-studio` no GitHub, instalador Windows x64. O SHA-256 fixado na receita é `c3a0b880adbe64dc4bcb68f93016916ab5b55ae43fd227115287bf80257d92dc`.

A aplicação só usa essas receitas se a comparação de versões exata, a identificação do pacote e o editor corresponderem aos critérios. **Não** baixa instaladores a partir de dados arbitrários do catálogo.

## Proteções
1. Download HTTPS a partir de domínio permitido, com redirecionamentos validados e limite de tamanho.
2. Hash SHA-256 validado para OBS; para ambos os fabricantes, o Windows confere Authenticode com a cadeia de certificados, e a identidade do editor também é conferida.
3. Confirmação antes de instalar e permissão UAC normal do Windows quando necessária.
4. Visual C++ utiliza `/install /quiet /norestart`; OBS conserva instalador interativo para escolhas de configuração.
5. Resultado só é declarado confirmado quando o Registro do Windows mostra a versão esperada (e não apenas quando o instalador encerra com código 0).
6. Arquivos de download criados numa pasta temporária específica e removidos ao terminar, sempre que o Windows permitir.
7. Não fecha programas nem reinicia o computador sem avisar; nenhuma instalação em lote é iniciada automaticamente.

## Limitações
- Compilação e testes do CI não são prova de instalação bem-sucedida no computador de destino.
- Não há receita de atualização automática neste teste para Adobe Acrobat e Plex; use Site para escolher o atualizador do fabricante.
- A consulta RuckZuck continua oferecendo **cobertura parcial**, diferente do WinGet.
- Atualizar pode exigir UAC; OBS pode abrir seu instalador e exigir interação.
- Falhas de certificado, hash, editor ou domínio **bloqueiam** o download/execução: não desative o AVG nem contorne validações.
- O Gerenciador é portátil, mas Windows e instaladores oficiais podem registrar eventos, caches e dados da instalação do próprio aplicativo.

## Primeiro teste seguro
1. Baixe e extraia o artefato Windows x64 em pasta vazia. Mantenha o AVG ligado.
2. Inicie o programa e aguarde as consultas automáticas. Confira a listagem.
3. Verifique se Visual C++ (x86 e x64) e OBS mostram Atualizar + Site; Adobe e Plex devem exibir apenas Site.
4. **Primeiro faça somente o teste da interface e dos links.** Não inicie instalações até conferir o comportamento do AVG.
5. Na etapa de teste de instalação, atualize **um único** Visual C++; confira resultado e versão no Registro. Faça o segundo só depois de confirmar sucesso. OBS deve ser testado separadamente, com OBS fechado.
6. Envie o log `%LOCALAPPDATA%\GerenciadorFinal\Diagnosticos\GerenciadorFinal-Diagnostico-v5.12.7.log` caso haja falhas.

Fontes das receitas: Microsoft Learn para VC++ 2013, lançamento oficial OBS no GitHub (tag 32.2.2), documentação Authenticode do Windows.
