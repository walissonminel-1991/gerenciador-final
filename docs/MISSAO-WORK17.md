# WORK17 — cobertura MSI e preservação de resultados

Candidata 5.13.11.62 TESTE. Verificações Windows e pacote pendentes. Base: PR #52, `680ba3134b5191628ebc9ed83f163fa33b85dda4`. [Auditoria anterior à implementação](WORK17-AUDITORIA.md).

## Implementação

- Pipeline consultivo comum para lookup, manifesto e decisões, sem executor. Falhas individuais preservam evidências anteriores e posteriores; timeout e limite retornam resultados parciais. Cancelamento explícito propaga e não sela um scan. O selo privado só pode ser emitido pelo caminho que abriu o catálogo Microsoft autenticado.
- Identidade MSI adicional para nome ARP localizado: ProductCode exato ou ID WinGet observado → catálogo assinado → cache MSI na instância/contexto correto → produto/fabricante/versão/UpgradeCode/arquitetura/escopo → PE exclusivo assinado pelo fabricante. Edições/canais conhecidos divergentes e família compartilhada permanecem bloqueados. Evidência é relida antes da execução.
- O pacote continua sujeito a SHA-256, assinatura confiável igual à instalação, conteúdo MSI e fechamento de processos. Na nova rota, mudança de ProductCode exige Upgrade Table com faixa/idioma de substituição, ActionProperty segura e sequência de execução comprovada. Sem essa prova, não há execução.
- Confirmação admite o nome canônico depois da atualização, mas exige unicidade, mesmo caminho, arquitetura/escopo, ProductCode alvo e família preservada.
- Diagnóstico por motivos fixos e contagens; não inclui nomes ou caminhos do inventário. A interface conserva os contadores existentes de consulta, planos e operações; consulta parcial continua identificada e evidência histórica fica sob revisão.

## Medição e validação

Execute no Windows: `dotnet run --project TestesAtualizador/TestesAtualizador.csproj -c Release -- --work17` e `python scripts/measure_work17.py`. A comparação usa os mesmos 22 cenários contra WORK16. Resultados ainda não apurados nesta versão do relatório. São planos elegíveis antes da validação do payload, não compatibilidade física ou operações finais autorizadas.

Leitor MSI nativo também lê bancos `.msi` sintéticos reais com e sem contrato de major upgrade, sem instalar. Catálogo e downloads reais permanecem cobertos pelos jobs existentes da CI. O limite do runner WinGet elevado continua documentado no WORK16.

## Roteiro físico PC FORTE e PC FRACO

1. Usar a candidata apenas depois da CI aprovada e da conferência do ZIP publicado. Registrar versão/hash, Windows, arquitetura, usuário comum, WinGet e duração; comparar com .61 no mesmo inventário, sem alterações entre snapshots.
2. Consultar com internet e repetir sem rede. Registrar inventariados, identificados, versões, planos silenciosos/supervisionados, desinstalações, gerenciamento externo, ambíguos e bloqueados. Denominadores devem ser idênticos.
3. Em usuário comum, testar COM nativo somente leitura. Registrar sucesso ou motivo real sem reparar/instalar WinGet ou elevar o leitor. Verificar cancelamento e indisponibilidade independente de outra fonte.
4. Em VM descartável antes dos PCs, usar um MSI de fabricante confiável com ARP localizado, assinatura válida, UpgradeCode consistente e root dedicado. Conferir diagnóstico e revisão do payload. Variantes x86/x64, instalação dupla, assinatura divergente e cache antigo devem bloquear.
5. Qualquer atualização/desinstalação real requer consentimento específico do usuário. Conferir versão, assinatura, root e ausência da cópia anterior. Retorno do instalador sem evidência suficiente permanece pendente/indeterminado.
6. Não executar limpeza residual nos PCs para comprovar fixtures. Homologação HKLM, propriedade e releitura física permanece em R4/R7; registrar separadamente quando houver autorização e ambiente adequado.

## Impedimentos preservados

Nenhum aplicativo de fornecedor novo está homologado fisicamente. Amplia-se a família MSI/WiX em instalações com nomes ARP divergentes e provas completas. EXE com alias, Upgrade Table complexa/condicional, serviços/drivers, MSI com cache inacessível ou família compartilhada continuam bloqueados. Não há novo executor WinGet/Scoop/Chocolatey, Burn, Squirrel update ou remoção MSIX genérica. Sem operações de terceiros nos PCs pessoais, sem scripts remotos. Roadmap permanece 27/42; I7/U8/R7/R4 físicos abertos.

## Documentação oficial consultada

- [MsiGetProductInfoEx — instância/contexto, State e LocalPackage](https://learn.microsoft.com/en-us/windows/win32/api/msi/nf-msi-msigetproductinfoexw).
- [Upgrade Table — códigos, limites, idiomas e atributos](https://learn.microsoft.com/en-us/windows/win32/msi/upgrade-table).
- [RemoveExistingProducts — sequência e substituição](https://learn.microsoft.com/en-us/windows/win32/msi/removeexistingproducts-action).
