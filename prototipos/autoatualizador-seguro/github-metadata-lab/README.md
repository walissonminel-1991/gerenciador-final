# Verificador de versões GitHub — experimento mínimo

## Propósito
Isolar o primeiro passo de um autoatualizador simples: **consultar a versão publicada**
sem baixar, instalar, substituir, fazer rollback ou alterar o GF. Não é atualização operacional.

- Repositório fixo: `walissonminel-1991/gerenciador-final`.
- Endpoint único para versão estável: `GET https://api.github.com/repos/walissonminel-1991/gerenciador-final/releases/latest`.
- `404` é ausência de Release publicada; `403`, `429`, `5xx`, timeout e redirect bloqueiam a decisão.
- Candidata só tem valor consultivo. Não confiar em URLs, `assets`, scripts ou comandos retornados pelo JSON.
- `NewVersionMetadataOnly` **não** implica arquivo autêntico nem permissão para substituir EXE.
- `CanInstall=false`, `Ready=false` e `AuthorizeRecovery=false` em todos os resultados.
- HTTP: sem cookies, credenciais padrão, redirects ou envio de inventário, telemetria ou nomes de aplicativos.
- Comparação de versões estrita, aceitando tags de 3 ou 4 segmentos numéricos, sem pré-lançamento.

## Lógica de entrega simplificada (ainda não implementada)
1. Manter no GitHub **Release oficial** com tag estável e artefato para Windows x64.
2. Exigir manifesto/metadata **autenticado** com autoridade independente do JSON da Release; chaves privadas não ficam no repositório.
3. Após verificar assinatura, alvo, versão e bytes completos, preparar em pasta separada e protegida. Nunca executar binário que venha de conteúdo não autenticado.
4. Encerrar a versão atual, verificar estado e efetuar troca controlada por helper separado. Em dúvida, preservar OLD/NEW e bloquear; nenhuma limpeza ou rollback agressivo.
5. Só depois de teste Windows real e revisão independente autorizar integração de produto. O projeto atual não contém esse helper.

**Situação real em 30/10/2026:** a consulta ao GitHub retornou zero Releases publicadas. Não há atualização que este canal possa oferecer.

A linha experimental B+A, TUF/NTFS/TOCTOU, G1–G8 e as provas de contenção existentes permanecem intactas. Este protótipo não redefine os gates operacionais e não conclui a Fase 1.
