# Pesquisa técnica e novas fontes — v5.13.11.0 CANDIDATA

Data: 20/10/2026. **Nenhum código externo foi incorporado diretamente**: somente ideias de arquitetura e consultas a APIs oficiais. Compatibilidade de licença, origem e comportamento fazem parte da avaliação.

## Referências pesquisadas

- [UniGetUI](https://github.com/Devolutions/UniGetUI) — licença MIT conferida, inspiração para unificar provedores de gerenciamento sem acoplar interface a um pacote específico. Nenhuma biblioteca, executável ou serviço copiado.
- [Driver Store Explorer](https://github.com/lostindark/DriverStoreExplorer) — arquitetura alternativa de leitura nativa SetupAPI, DISM e pnputil. O GF usa hoje WMI e histórico próprio, não o inventário completo do Driver Store. Nenhuma operação de remoção, driver ou código de terceiros copiado.
- [WinGet list/upgrade — Microsoft Learn](https://learn.microsoft.com/windows/package-manager/winget/list) — referência de escopos e divergências entre registros do Windows e catálogos. A aplicação não depende de executar Winget para obter as quatro novas fontes.

## Endpoints oficiais integrados (somente GET HTTPS)

| Família | Origem | URL canônica informativa | Versão esperada da amostra de teste |
|---|---|---|---|
| ShareX | github.com/ShareX/ShareX/releases/latest | https://getsharex.com/ | v21.0.0 |
| HandBrake | github.com/HandBrake/HandBrake/releases/latest | https://handbrake.fr/ | 1.11.2 |
| Rufus | github.com/pbatard/rufus/releases/latest | https://rufus.ie/ | v4.15 |
| qBittorrent | github.com/qbittorrent/qBittorrent/releases/latest | https://www.qbittorrent.org/ | release-5.2.3 |

Observação: essas versões são fixtures para testes, **não** versões congeladas no aplicativo; em execução o aplicativo busca a versão estável atual no endpoint. O cliente exige formato numérico comparável, campo tag_name, rejeita prerelease/draft e liga produto+editor ao catálogo incorporado em tempo de compilação. Se alguma fonte mudar de formato, falha isoladamente; o programa não inventa candidatos.

## Controles e privacidade

- Identidade fixa do produto e lista explícita de editores: nome parecido ou editor desconhecido não aciona consulta.
- Não envia a lista completa de programas instalados nem identificadores do usuário; consulta URLs estáticas de catálogos públicos.
- Limite da resposta, timeout inclusive no corpo, HTTPS com hosts aprovados, cache de 10 minutos, concorrência baixa e cancelamento.
- Uma instalação só pode gerar uma linha no resultado consolidado. Fontes oficiais informam versão, não autorizam execução de instaladores não verificados.
- Não incorporar scripts, drivers, hooks, serviços ou binários de projetos externos.
- Licenças: UniGetUI MIT; código de projetos GPL como Mem Reduct/Windows Memory Cleaner não foi copiado nem vinculado. Verificar avisos de terceiros antes de futuras integrações.

## Portões para o lançamento estável

- [ ] Regressões Windows de dez grupos aprovadas.
- [ ] Build x64 self-contained, SHA-256 e identificação da versão/commit aprovados.
- [ ] Testes na máquina PC FRACO e no segundo computador; comparar com WinGet no mesmo horário.
- [ ] Incidente AVG avaliado e integridade do executável revisada.
- [ ] Assinatura digital e revisão de licença/segurança antes de Release estável.

A candidata pode estar no GitHub como código/PR de desenvolvimento sem ser disponibilizada como Release estável.
