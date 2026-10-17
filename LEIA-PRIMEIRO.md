# Gerenciador Final — Laboratório de Provedores v0.1

Este pacote é um **experimento independente, somente leitura**. NÃO é uma atualização da versão principal do Gerenciador Final. Não substitua a instalação atual por este teste.

## Objetivo

Avaliar uma alternativa ao WinGet e à API COM que apresentou `InvalidCastException`. O laboratório:

1. Lê nomes, versões e fabricantes do Registro do Windows localmente (sem executar programas).
2. Após confirmação explícita, faz dois GETs HTTPS no serviço RuckZuck: resolução do servidor e obtenção do catálogo.
3. Compara os dados **no próprio computador**. Nenhum nome da lista de programas é enviado nas requisições; o serviço remoto recebe dados normais de conexão, como endereço IP e agente do cliente.
4. Exibe **possíveis atualizações**, **sem cobertura** ou **versão não comparável**, sem classificar correspondências vagas como atualizações confirmadas.
5. Mantém diagnóstico apenas na memória; há botão de cópia para área de transferência. Nada é escrito automaticamente em AppData ou Temp por este código.

**Não instala, não atualiza, não desinstala, não executa scripts de terceiros, não abre PowerShell e não chama `winget.exe`.** A consulta não inclui verificações via outros gerenciadores (Scoop/Chocolatey). Resultados podem ser incompletos e não são equivalentes aos seis resultados conhecidos do WinGet.

## Fontes e restrições

- UniGetUI: arquitetura modular como referência conceitual, código disponível sob MIT: https://github.com/Devolutions/UniGetUI
- RuckZuck: documentação da API pública (geturl + getcatalog): https://github.com/rzander/ruckzuck/wiki/REST-API
- O catálogo RuckZuck depende de serviço externo, disponibilidade e limites. `getsoftwares` exige chave: o laboratório não utiliza esse endpoint.
- Não copiamos bibliotecas nem códigos de terceiros. Todo código deste laboratório é independente. Não é permitida execução de scripts incluídos em definições remotas.

## Compilar no GitHub sem instalar SDK local

1. Crie uma branch de teste nova, por exemplo `laboratorio-provedores-v0.1`.
2. Envie **o conteúdo deste ZIP para a raiz da branch**, mantendo `.github/workflows/laboratorio-provedores.yml`.
3. Vá a `Actions` > `Laboratório de provedores - Windows x64` > `Run workflow` (se o disparo automático pelo push não tiver ocorrido).
4. Confirme que os testes e a publicação passaram. Baixe o artefato `Laboratorio-Provedores-Windows-x64`.
5. Extraia o ZIP gerado **por completo** em uma pasta nova. Execute `GerenciadorFinal.Laboratorio.exe`, mantendo o AVG ativo. Não execute se o antivírus bloquear: registre o alerta e pare.
6. Primeiro clique em `Testar com exemplos`. Depois, caso deseje, clique em `Consultar catálogo`, leia a confirmação de privacidade e autorize.
7. Envie uma captura do resultado e, se houver erro, o conteúdo do botão `Copiar diagnóstico`.

Os arquivos publicados incluem o runtime .NET e devem permanecer juntos na mesma pasta. Não mova somente o EXE. Não é necessário instalar o SDK na máquina de teste.

## Critérios objetivos do experimento

- A aplicação abre sem SideBySide e sem intervenção do antivírus.
- Testes de comparação passam no Actions; o teste de exemplos funciona.
- A consulta retorna catálogo ou informa indisponibilidade real, sem inventar zero atualizações.
- As operações não iniciam instaladores, scripts ou comandos WinGet.
- A cobertura das atualizações será avaliada **somente depois** de verificar uma máquina real, junto com falsos positivos.

## Privacidade e portabilidade

A aplicação não cria automaticamente configurações nem registros em disco. O próprio Windows, .NET, antivírus e serviços de rede podem manter seus registros e caches normais; por isso não é correto prometer zero resíduos em todo o sistema. A comunicação de rede ocorre somente após confirmação do usuário.

## Limites de validação

Os testes offline e estáticos não substituem um teste compilado em Windows. Este pacote **não inclui executável** nem garante que RuckZuck ainda ofereça acesso público à API em todas as redes.
