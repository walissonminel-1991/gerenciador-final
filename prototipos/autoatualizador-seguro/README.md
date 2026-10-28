# GF — protótipo isolado de validação (Etapa 1)

**Somente fixtures inertes. Não instala, não baixa atualizações, não executa o
GF e não é um atualizador distribuível.** Requer SDK .NET 10.0.402 em ambiente
de desenvolvimento descartável e Python com `cryptography`. Não instalar essas
ferramentas no PC FORTE por consequência deste protótipo.

Bibliotecas fixadas: `Tuf` **1.1.1** e `BouncyCastle.Cryptography` **2.7.0**,
com `packages.lock.json`, restauração locked e fonte/mapeamento NuGet limitados.
Projeto independente, sem ProjectReference ao GF. IsPackable/IsPublishable=false.

```sh
python run_tests.py --report /caminho/temporario/gf-u1-results.json
```

`dotnet` deve estar no PATH ou em `GF_DOTNET`. O runner desativa telemetria do
SDK, usa diretório temporário externo para build/fixtures, testa e verifica a
lista Compile do produto **sem compilar o produto**. Nenhum artefato entra no
canal privado. Não usar `dotnet publish` para este projeto.

Os fontes `.cs.txt` são compilados explicitamente por Prototype.csproj. Essa
extensão impede inclusão pelo glob `**/*.cs` do GF. Directory.Build.props local
também move os fontes C# gerados de obj/bin para fora do repositório, inclusive
em build direto, e mantém a política NuGetAudit herdada. Não remover essas duas
barreiras. O runner recusa sobras antigas de obj/bin dentro desta pasta.

O gerador Python usa Ed25519 de uma implementação independente da biblioteca
.NET. Produz 42 cenários assinados, ZIPs de texto e metadados temporários, com
raízes 2-de-3 e rotação que exige quórum antigo e novo. Os resultados finais
foram **66/66 testes**, sem somar mutações internas ou execuções de correção.

Todas as seeds derivadas no gerador são **públicas, determinísticas e exclusivas
de teste**. O bootstrap de fixture tem fingerprint incorporado no test runner;
não é raiz operacional, não deve ser reutilizado em qualquer build GF e não
autoriza conteúdo de produção. Os SHAs `aaaa...`/`bbbb...` e versões nos cenários
são dados inventados, não commits/candidatas publicados.

[POUF e limites](POUF.md) · [Relatório da Etapa 1](../../docs/WORK-AUTOATUALIZADOR-ETAPA1.md) ·
[Contrato gf-self-update/1 preservado](../../docs/WORK-AUTOATUALIZADOR-ETAPA0.md).
