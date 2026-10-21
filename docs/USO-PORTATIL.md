# Como usar a versão portátil

O Gerenciador Final é destinado a Windows 10/11 x64. No computador de uso,
**não é necessário instalar o SDK .NET**: a publicação do aplicativo inclui
seu runtime. A pasta `Distribuicao` deve ser mantida inteira, pois o executável
precisa dos arquivos publicados ao seu lado.

## Executar

1. Extraia o pacote portátil completo em uma pasta de sua preferência.
2. Abra `GerenciadorFinal.exe` dentro da pasta `Distribuicao`.
3. Utilize as consultas e diagnósticos antes de iniciar qualquer reparo.
4. Leia as confirmações. Algumas tarefas pedem autorização UAC; não são
   executadas automaticamente pela simples abertura do aplicativo.

## Funções e limitações

- O inventário usa o Registro, MSIX/Store e manifestos de jogos. A busca de
  versões usa RuckZuck e fontes oficiais HTTPS, sem exigir a CLI WinGet.
  Falhas de fontes tornam a consulta parcial; não comprovam desinstalação.
- O diagnóstico de rede é apenas consultivo. `Reparar rede` só oferece correções
  específicas quando há resultado aplicável e pede confirmação.
- `Redefinir rede`, ajustes de energia, atualizações, desinstalação, limpeza e
  reparos do Windows podem alterar configurações, exigir permissões e interromper
  o trabalho em andamento. Salve os documentos antes de utilizá-los.
- A criação de ponto de restauração requer a Proteção do Sistema configurada;
  não substitui o backup dos dados.
- A ferramenta não promete ganho de desempenho e não substitui antivírus.

## Segurança e distribuição

Baixe o software somente de canais oficiais do projeto. Confira hashes SHA-256
quando publicados. Se houver bloqueio por antivírus, **não desative a proteção
nem adicione exceções**; interrompa a execução e reporte ao mantenedor.

Para compilar a partir do código-fonte, leia `README.md` e
`docs/PUBLICACAO.md` no repositório do projeto.
