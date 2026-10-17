# Builds, instalador e releases

## Compilação local de desenvolvimento

Executar `Compilar-OSS-Windows-x64.bat` em Windows x64 com SDK .NET 10. Isto gera `Distribuicao`, **sem assinatura**, e sem adicionar exceções de segurança. Confirme a versão no C# e o conteúdo compilado antes de divulgar qualquer arquivo.

## GitHub Actions

O workflow `Compilar Windows (sem assinatura)` restaura, compila, publica para win-x64 self-contained e registra os hashes SHA-256 de EXE/DLL no log. Ele envia `Distribuicao` como **artefato de teste do workflow**, não como Release. Antes de qualquer download público, verifique os alertas de segurança e os resultados de teste.

## Instalação

O script histórico `Criar-Instalador-Windows-x64.bat` utiliza `Assinar-GF.cmd` e exige um certificado Authenticode local. Não use esse fluxo esperando uma assinatura grátis da SignPath.

Para instalar em uma máquina de teste, o mantenedor pode compilar um instalador **não assinado somente para avaliação interna** após revisar o script Inno Setup, sem remover verificações do fluxo de produção. O projeto não contém ainda um workflow SignPath de installer-signing. Após aprovação, criar o fluxo oficial de assinatura do EXE/DLL e instalação em GitHub Actions conforme a configuração autorizada pela fundação.

## Antes de uma Release pública

- Revisar licença e direitos de cada imagem, código e componente distribuído.
- Compilar no runner GitHub Actions, conferir hashes e arquitetura.
- Executar testes num Windows isolado, inclusive UAC, antivírus, instalação e desinstalação.
- Investigar os 3/52 alertas do instalador anterior, se persistirem no artefato corrente.
- Publicar notas de versão e fonte; fornecer SHA-256 e informação clara sobre a ausência ou presença real de Authenticode.
- **Nunca** instruir usuários a desativar antivírus ou ignorar bloqueios.
