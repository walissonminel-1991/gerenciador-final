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
rem Sem exclusoes de pastas, sem sobrescrever ZIP e sem upload.
rem Exige commit aprovado e PowerShell 7; --print-version continua sem efeitos.
if "%~1"=="" (
  echo Uso: %~nx0 SHA_FONTE_APROVADO [PASTA_SAIDA]
  exit /b 2
)
if not "%~3"=="" exit /b 2
where pwsh.exe >nul 2>&1
if errorlevel 1 (
  echo ERRO: PowerShell 7 necessario na maquina de compilacao.
  exit /b 1
)
if "%~2"=="" (
  pwsh.exe -NoLogo -NoProfile -File "%~dp0scripts\Compilar-Portatil-Privado.ps1" -ExpectedSourceCommit "%~1"
) else (
  pwsh.exe -NoLogo -NoProfile -File "%~dp0scripts\Compilar-Portatil-Privado.ps1" -ExpectedSourceCommit "%~1" -OutputDirectory "%~2"
)
exit /b %ERRORLEVEL%
