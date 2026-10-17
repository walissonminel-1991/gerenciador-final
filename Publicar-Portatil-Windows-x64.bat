@echo off
setlocal EnableExtensions
cd /d "%~dp0"
title Gerenciador Final v5.12.3 - Gerar versao portatil

rem ESTE SCRIPT E PARA GERAR A DISTRIBUICAO PORTATIL.
rem O usuario final nao executa este arquivo e nao precisa do SDK .NET.
rem Este script NAO instala o SDK nem altera politicas de seguranca do Windows.

set "DOTNET_EXE="
if exist "%ProgramFiles%\dotnet\dotnet.exe" set "DOTNET_EXE=%ProgramFiles%\dotnet\dotnet.exe"
if not defined DOTNET_EXE for /f "delims=" %%D in ('where dotnet.exe 2^>nul') do if not defined DOTNET_EXE set "DOTNET_EXE=%%D"

if not defined DOTNET_EXE goto sdk_missing
"%DOTNET_EXE%" --list-sdks 2>nul | findstr /R /C:"^10\.0\." >nul
if errorlevel 1 goto sdk_missing

rem Mantem cache de build/restore dentro da pasta-fonte, para facilitar a limpeza.
set "DOTNET_CLI_HOME=%~dp0.cache\dotnet-home"
set "NUGET_PACKAGES=%~dp0.cache\nuget\packages"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "DOTNET_NOLOGO=1"
set "DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1"

set "DIST_DIR=%~dp0Distribuicao"
set "ZIP_PATH=%~dp0..\GerenciadorFinal-v5.12.3-Portatil-Windows-x64.zip"

echo ============================================================
echo GERENCIADOR FINAL v5.12.3 - VERSAO PORTATIL
echo ============================================================
echo O programa nao sera instalado neste computador.
echo Nao serao criados atalhos nem configuracoes persistentes do app.
echo Todos os arquivos publicados ficarao na pasta Distribuicao.
echo.

if exist "%DIST_DIR%" rmdir /s /q "%DIST_DIR%"
if exist "%DIST_DIR%" goto clean_failed
if exist "%~dp0bin" rmdir /s /q "%~dp0bin"
if exist "%~dp0obj" rmdir /s /q "%~dp0obj"
if exist "%~dp0.cache" rmdir /s /q "%~dp0.cache"
if exist "%ZIP_PATH%" del /q "%ZIP_PATH%"

mkdir "%DIST_DIR%"
if errorlevel 1 goto clean_failed

echo Publicando para Windows x64 com runtime incluido...
echo Os arquivos do runtime permanecerao ao lado do EXE, sem extracao do app para TEMP.
echo.
"%DOTNET_EXE%" publish "%~dp0GerenciadorAplicativos.csproj" -c Release -r win-x64 --self-contained true -o "%DIST_DIR%" -p:PublishSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=false -p:DebugType=None -p:DebugSymbols=false
if errorlevel 1 goto publish_failed

if not exist "%DIST_DIR%\GerenciadorFinal.exe" goto exe_missing
copy /y "%~dp0docs\USO-PORTATIL.md" "%DIST_DIR%\LEIA-ME.md" >nul
if errorlevel 1 goto copy_failed

rem Cria um ZIP pronto para copiar para outro computador usando o tar incluso no Windows.
where tar.exe >nul 2>&1
if errorlevel 1 goto no_tar
pushd "%~dp0"
tar.exe -a -c -f "%ZIP_PATH%" "Distribuicao"
set "ZIP_EXIT=%ERRORLEVEL%"
popd
if not "%ZIP_EXIT%"=="0" goto zip_failed

rem Limpa somente artefatos de desenvolvimento desta pasta apos a publicacao bem sucedida.
if exist "%~dp0bin" rmdir /s /q "%~dp0bin"
if exist "%~dp0obj" rmdir /s /q "%~dp0obj"
if exist "%~dp0.cache" rmdir /s /q "%~dp0.cache"

echo.
echo ============================================================
echo PUBLICACAO PORTATIL CONCLUIDA
echo ============================================================
echo Pasta de teste: %DIST_DIR%
echo ZIP pronto para distribuir: %ZIP_PATH%
echo.
echo COMO USAR EM OUTRO COMPUTADOR:
echo 1. Copie o ZIP e extraia a pasta Distribuicao.
echo 2. Abra GerenciadorFinal.exe diretamente.
echo 3. Nao e necessario SDK, runtime separado, instalador ou atalho.
echo.
echo O aplicativo nao grava suas configuracoes/logs proprios fora da pasta.
echo O Windows e o WinGet ainda podem manter seus proprios registros/cache.
echo.
pause
exit /b 0

:no_tar
echo AVISO: o comando tar.exe nao foi encontrado; o aplicativo foi publicado em:
echo %DIST_DIR%
echo Para distribuir, compacte a pasta Distribuicao pelo Explorador de Arquivos.
goto cleanup_success

:cleanup_success
if exist "%~dp0bin" rmdir /s /q "%~dp0bin"
if exist "%~dp0obj" rmdir /s /q "%~dp0obj"
if exist "%~dp0.cache" rmdir /s /q "%~dp0.cache"
echo.
echo Publicacao concluida. A pasta portatil esta pronta para teste.
pause
exit /b 0

:sdk_missing
echo ERRO: o SDK .NET 10 nao foi encontrado neste computador.
echo Ele e necessario apenas para gerar a distribuicao, nao para usar o programa portatil.
echo Este script nao instala software nem altera este computador automaticamente.
echo Instale o SDK .NET 10 apenas no computador de compilacao:
echo https://dotnet.microsoft.com/pt-br/download/dotnet/10.0
pause
exit /b 1

:clean_failed
echo ERRO: nao foi possivel preparar a pasta de publicacao.
echo Feche o aplicativo caso esteja aberto dentro da pasta Distribuicao e tente novamente.
pause
exit /b 1

:publish_failed
echo.
echo ERRO: a compilacao/publicacao falhou. O pacote portatil nao foi declarado pronto.
echo Os artefatos de compilacao foram mantidos para diagnostico dentro da pasta-fonte.
pause
exit /b 1

:exe_missing
echo ERRO: a publicacao terminou sem GerenciadorFinal.exe.
pause
exit /b 1

:copy_failed
echo ERRO: nao foi possivel copiar docs\USO-PORTATIL.md para a pasta portatil.
pause
exit /b 1

:zip_failed
echo AVISO: o EXE foi publicado, mas nao foi possivel gerar o ZIP automaticamente.
echo Compacte a pasta Distribuicao pelo Explorador de Arquivos.
goto cleanup_success
