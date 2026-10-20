# Gerenciador Final v5.12.6.1 TESTE — estabilidade e atualização assistida

Esta versão é incremental e preserva a interface principal da v5.12.6. É uma versão de testes, não uma confirmação de que a instalação automática esteja completa.

## Mudanças
- O RuckZuck continua sendo consultado apenas por HTTPS, sem WinGet ou API COM.
- Runtimes .NET de versões principais distintas (ex.: 9 para 10) passam a resultados **Incertos**, não a atualizações diretas; versões podem coexistir.
- Comparação de arquitetura x86/x64 para redistribuíveis Visual C++ e runtimes.
- Filtro **Incertos** separado da lista de **Com atualização**.
- Texto da pesquisa atualizado; remoção de nomes internos legados do WinGet.
- Quando reconhecido, o aplicativo permite abrir a página oficial do fabricante, com confirmação. Esse botão não baixa nem executa arquivos.
- **Atualizar todos** permanece desativado até que possamos verificar instaladores e segurança de cada provedor.

## Testes iniciais no Windows
1. No GitHub Actions, baixe o artefato Windows x64 da branch `teste-qualidade-v5.12.6.1`. Extraia **todos os arquivos** em pasta vazia.
2. Mantenha o AVG ativo; se houver bloqueio, interrompa o teste. Não crie exceções e não desative a proteção.
3. Confirme que o cadastro local e os painéis de manutenção carregam.
4. Clique em **Com atualização**, permita a consulta e confira os candidatos retornados.
5. Clique em **Incertos** e verifique a classificação do runtime .NET 9 versus 10.
6. Para testar a orientação assistida, clique **Site oficial** em um candidato reconhecido e confirme o domínio. Não instale nada neste teste.
7. Registre resultados e diagnósticos. Não execute desinstalação, limpeza, reparos nem atualizações em lote neste primeiro ciclo.

## Limites e segurança
O catálogo RuckZuck pode omitir programas e não autoriza execução de instaladores por si só. Uma versão disponível não garante que a atualização seja aplicável ao computador. Instalações automáticas só deverão ser habilitadas após validação de produto, versão, arquitetura, origem oficial, integridade criptográfica e assinatura Authenticode, consentimento e verificação pós-instalação. O GitHub Actions valida compilação e a busca de catálogo, não o comportamento do AVG local. O .NET Windows portátil distribui runtime com o executável; outros componentes do Windows podem deixar registros/caches normais.
