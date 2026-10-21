@echo off
setlocal EnableExtensions DisableDelayedExpansion
rem A versao vem do projeto para nao rotular novas builds com versoes antigas.
set "GF_BUILD_VERSION="
for /f "tokens=*" %%L in ('findstr /R /C:"<Version>[0-9][0-9.]*</Version>" "%~dp0GerenciadorAplicativos.csproj"') do for /f "tokens=2 delims=<>" %%V in ("%%L") do set "GF_BUILD_VERSION=%%V"
if not defined GF_BUILD_VERSION (
  echo ERRO: versao ausente no projeto.
  exit /b 1
)
rem Consulta sem efeitos para regressao Windows dos nomes de distribuicao.
if /I "%~1"=="--print-version" (
  echo %GF_BUILD_VERSION%
  exit /b 0
)
cd /d "%~dp0"
title GERENCIADOR FINAL v%GF_BUILD_VERSION% - CRIAR INSTALADOR

rem Somente para o COMPUTADOR DE COMPILACAO.
rem O usuario final NAO precisa do SDK .NET nem de Inno Setup.
rem O instalador nao cria exclusoes de antivirus ou regras de firewall.
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "DOTNET_NOLOGO=1"
set "DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1"
set "DOTNET_CLI_HOME=%~dp0.cache\dotnet-home"
set "NUGET_PACKAGES=%~dp0.cache\nuget\packages"

rem PUBLICACAO PUBLICA: um certificado valido e obrigatorio.
rem Certificado no repositorio CurrentUser\My, com chave acessivel pelo SignTool.
echo.
echo ASSINATURA DIGITAL - GERENCIADOR FINAL v%GF_BUILD_VERSION%
if not defined GF_CERT_SHA1 (
  echo Digite a impressao digital SHA1 de seu certificado de ASSINATURA DE CODIGO.
  echo Nao informe senhas ou chaves privadas neste script.
  set /p "GF_CERT_SHA1=SHA1 do certificado: "
)
if not defined GF_CERT_SHA1 goto cert_missing
set "GF_CERT_SHA1=%GF_CERT_SHA1: =%"

call :find_dotnet
if not defined DOTNET_EXE goto sdk_missing
"%DOTNET_EXE%" --list-sdks 2>nul | findstr /B /C:"10.0." >nul
if errorlevel 1 goto sdk_missing

goto publish

:sdk_missing
echo SDK .NET 10 ausente na maquina de compilacao. Nenhuma dependencia sera instalada.
echo Prepare o ambiente de desenvolvimento com o SDK oficial antes de compilar.
goto no_sdk

:find_dotnet
set "DOTNET_EXE="
if exist "%ProgramFiles%\dotnet\dotnet.exe" set "DOTNET_EXE=%ProgramFiles%\dotnet\dotnet.exe"
if not defined DOTNET_EXE for /f "delims=" %%D in ('where dotnet.exe 2^>nul') do if not defined DOTNET_EXE set "DOTNET_EXE=%%D"
exit /b 0

:publish
set "DIST_DIR=%~dp0Distribuicao"
echo.
echo =========================================================
echo  GERENCIADOR FINAL v%GF_BUILD_VERSION% - INSTALADOR WINDOWS x64
echo =========================================================
echo Publicando aplicativo com runtime .NET incluido...
if exist "%DIST_DIR%" rmdir /S /Q "%DIST_DIR%"
if exist "%DIST_DIR%" goto app_open
mkdir "%DIST_DIR%"
if errorlevel 1 goto publish_failed
"%DOTNET_EXE%" publish "%~dp0GerenciadorAplicativos.csproj" -c Release -r win-x64 --self-contained true -o "%DIST_DIR%" -p:PublishSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=false -p:DebugType=None -p:DebugSymbols=false
if errorlevel 1 goto publish_failed
if not exist "%DIST_DIR%\GerenciadorFinal.exe" goto publish_failed

call :find_iscc
if defined ISCC_EXE goto compile

echo.
echo Inno Setup nao foi encontrado. Prepare manualmente o ambiente de compilacao.
goto no_inno

:find_iscc
set "ISCC_EXE="
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC_EXE=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC_EXE=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if exist "%ProgramFiles%\Inno Setup 7\ISCC.exe" set "ISCC_EXE=%ProgramFiles%\Inno Setup 7\ISCC.exe"
if exist "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" set "ISCC_EXE=%LocalAppData%\Programs\Inno Setup 6\ISCC.exe"
if exist "%LocalAppData%\Programs\Inno Setup 7\ISCC.exe" set "ISCC_EXE=%LocalAppData%\Programs\Inno Setup 7\ISCC.exe"
if not defined ISCC_EXE for /f "delims=" %%I in ('where ISCC.exe 2^>nul') do if not defined ISCC_EXE set "ISCC_EXE=%%I"
exit /b 0

:compile
echo.
echo Assinando os binarios do Gerenciador antes de gerar o instalador...
call "%~dp0Assinar-GF.cmd" "%DIST_DIR%\GerenciadorFinal.exe"
if errorlevel 1 goto signing_failed
if not exist "%DIST_DIR%\GerenciadorFinal.dll" goto signing_failed
call "%~dp0Assinar-GF.cmd" "%DIST_DIR%\GerenciadorFinal.dll"
if errorlevel 1 goto signing_failed

set "RESULT=%~dp0Instalador\GerenciadorFinal-Setup-v%GF_BUILD_VERSION%-Windows-x64.exe"
if exist "%RESULT%" del /q "%RESULT%"
if exist "%RESULT%" goto compiler_failed

echo.
echo Compilando e assinando instalador e desinstalador...
echo O UAC sera solicitado NO MOMENTO DA INSTALACAO.
"%ISCC_EXE%" "/Sgf_sign=$q%~dp0Assinar-GF.cmd$q $f" "%~dp0GerenciadorFinal-Instalador.iss"
if errorlevel 1 goto compiler_failed
if not exist "%RESULT%" goto compiler_failed
call "%~dp0Verificar-Assinaturas-GF.cmd" "%RESULT%"
if errorlevel 1 goto verification_failed
echo.
echo =========================================================
echo  INSTALADOR GERADO COM SUCESSO
 echo =========================================================
echo %RESULT%
echo.
echo Instalador assinado. Confira o registro de hashes SHA256 na pasta Instalador.
echo Distribua somente depois dos testes de seguranca e instalacao.
echo Nao e necessario SDK ou runtime .NET separado no destino.
echo Consultas de atualizacao usam fontes HTTPS; MSIX requer recursos do Windows.
echo IMPORTANTE: o projeto nao adiciona exclusoes de antivirus nem firewall.
echo.
pause
exit /b 0

:cert_missing
echo ERRO: assinatura obrigatoria. Nenhum instalador publico sera gerado sem certificado.
goto fail
:signing_failed
echo ERRO: nao foi possivel assinar o aplicativo. Nao distribuia os arquivos.
goto fail
:verification_failed
echo ERRO: assinaturas do instalador/arquivos nao validadas.
goto fail
:no_winget
echo Nao ha WinGet neste computador para instalar ferramentas de compilacao.
echo Instale o SDK oficial somente nesta maquina e execute o publicador novamente:
echo https://dotnet.microsoft.com/download/dotnet/10.0
goto fail
:no_sdk
echo Compilacao cancelada. Falta o SDK .NET 10 na maquina de compilacao.
goto fail
:sdk_install_failed
echo ERRO: instalacao/verificacao do SDK falhou. Consulte a mensagem acima.
goto fail
:no_inno
echo Publiquei o aplicativo, mas nao foi possivel criar o instalador sem Inno Setup.
echo Instale o Inno Setup pelo site oficial https://jrsoftware.org/isinfo.php e tente novamente.
goto fail
:inno_install_failed
echo ERRO: Inno Setup nao foi detectado apos a instalacao.
goto fail
:publish_failed
echo ERRO: publicacao falhou ou GerenciadorFinal.exe nao foi criado.
goto fail
:app_open
echo ERRO: a pasta Distribuicao esta em uso. Feche o Gerenciador Final e tente de novo.
goto fail
:compiler_failed
echo ERRO: compilacao do instalador falhou. Revise as mensagens acima.
goto fail
:fail
echo.
pause
exit /b 1
