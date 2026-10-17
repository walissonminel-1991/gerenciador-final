@echo off
setlocal EnableExtensions
cd /d "%~dp0"
title Gerenciador Final - compilacao de codigo aberto
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "DOTNET_NOLOGO=1"
set "DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1"

where dotnet.exe >nul 2>&1
if errorlevel 1 goto missing

dotnet --list-sdks | findstr /B /C:"10.0." >nul
if errorlevel 1 goto missing

echo Gerando build de desenvolvimento NAO ASSINADA...
dotnet publish "%~dp0GerenciadorAplicativos.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -o "%~dp0Distribuicao"
if errorlevel 1 goto failed
if not exist "%~dp0Distribuicao\GerenciadorFinal.exe" goto failed
echo.
echo Build concluida em Distribuicao\GerenciadorFinal.exe
echo Sem assinatura digital. Nao distribuia sem concluir a revisao de seguranca.
pause
exit /b 0
:missing
echo ERRO: SDK .NET 10 ausente na maquina de desenvolvimento.
echo https://dotnet.microsoft.com/download/dotnet/10.0
pause
exit /b 1
:failed
echo ERRO: compilacao falhou. Confira a mensagem acima.
pause
exit /b 1
