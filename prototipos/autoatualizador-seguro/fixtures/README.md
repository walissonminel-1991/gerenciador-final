# Fixtures públicas, sem autoridade operacional

bootstrap.test-only.json é a raiz pública determinística de teste cujo SHA-256
está fixado em FixtureValidator.cs.txt. Sua chave privada é derivável do gerador
público, por definição: **nunca usar para produção**.

O gerador produz os demais metadados/ZIPs em diretório temporário externo. Datas
de timestamp/snapshot/targets são relativas à execução para permitir reproduzir
os testes; a raiz inicial é fixa e pode estar expirada, pois somente autentica
a rotação para a raiz nova válida. Esse caminho também é validado pela biblioteca.

As fixtures assinadas de texto não são versões do GF nem aprovação para download,
instalação, piloto conectado ou publicação. Não versionar ZIPs, DLLs ou EXEs gerados.
