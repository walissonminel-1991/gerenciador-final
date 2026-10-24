# Compilação e distribuição

## Ambiente de desenvolvimento

Usar Windows x64 e o SDK indicado em `global.json` (10.0.402; correções da mesma
faixa permitidas). `Compilar-OSS-Windows-x64.bat` gera a publicação de
**desenvolvimento não assinada** em Distribuicao. `Publicar-Portatil-Windows-x64.bat`
limpa saídas locais e cria o ZIP portátil. Ambos precisam do SDK somente na
máquina que compila; o usuário final recebe o runtime completo.

A publicação mantém EXE, DLLs e runtime lado a lado: self-contained, sem
single-file, trimming, packer ou extração do aplicativo em TEMP. O projeto inclui
guia portátil, licença GF, privacidade e avisos dos packs efetivamente resolvidos.
Não remover DLLs do runtime manualmente porque seus nomes parecem dispensáveis.

## Candidata TESTE pelo GitHub Actions

`build-windows.yml` exige **regressões Windows e política/privacidade aprovadas**
da mesma revisão antes do job de publicação. O pipeline confere versões dos
scripts, projeto/manifesto, ativação Win32 do EXE, arquitetura AMD64, runtime,
composição, origem e SHA-256 de todos os arquivos. Os controles negativos do
verificador usam somente fixtures descartáveis, sem instalar programas.

O nome do artefato usa versão do projeto e SHA curto da **fonte**. BUILD-INFO.txt
registra também base da PR, merge sintético efetivamente testado e run. O ZIP de
Actions contém o EXE na raiz; o ZIP criado pelo script local pode ter a pasta
Distribuicao. Não misturar arquivos de versões diferentes.

Artefatos ficam sete dias, não são Releases e não têm Authenticode. Atestação de
procedência ocorre somente na main, com OIDC restrito; PR não recebe essas
permissões. Atestação e SHA-256 não substituem assinatura nem homologação AVG.
O relatório da missão registra o digest do ZIP baixado e a conferência independente.

O verificador pode ser usado numa pasta extraída sem executar o EXE:

```powershell
python scripts/verificar_pacote.py PASTA --version VERSAO --source SHA_FONTE --build SHA_MERGE --base SHA_BASE
```

Os valores esperados devem vir da PR/run confiável, além de conferir BUILD-INFO.
Os ZIPs locais de desenvolvimento não têm os metadados de proveniência da CI;
não apresentá-los como candidata aprovada apenas porque o publish terminou.

## Instalador e release pública

`Criar-Instalador-Windows-x64.bat`, `Assinar-GF.cmd` e o script Inno Setup
preservam o fluxo opcional de instalador com certificado Authenticode local.
Não existe aprovação SignPath nem um fluxo gratuito de assinatura configurado.

Antes de uma release pública, revisar direitos dos recursos e termos de todos
os componentes incorporados, incluindo os [avisos Microsoft](THIRD-PARTY-NOTICES.md),
validar Windows 10/11 e antivírus em ambiente isolado, resolver alertas de
fornecedores e registrar testes reais de instalação/desinstalação/preservação.
O código continua MIT e gratuito. Não desativar proteções ou criar exclusões para
contornar um alerta. A missão WORK9 não publica release estável nem faz merge.
