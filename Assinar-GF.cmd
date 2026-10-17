@echo off
setlocal EnableExtensions DisableDelayedExpansion
rem Assinatura Authenticode com certificado publico confiavel do publicador.
rem A chave privada nunca e copiada ao projeto. Nao armazene senha nem token nesta pasta.
if "%~1"=="" (
  echo ERRO: informe o arquivo a assinar.
  exit /b 1
)
if not exist "%~1" (
  echo ERRO: arquivo inexistente: %~1
  exit /b 1
)
if not defined GF_CERT_SHA1 (
  echo ERRO: falta GF_CERT_SHA1 com a impressao digital do certificado.
  exit /b 1
)
if not defined GF_SIGNTOOL (
  for /f "delims=" %%A in ('where signtool.exe 2^>nul') do if not defined GF_SIGNTOOL set "GF_SIGNTOOL=%%A"
)
if not defined GF_SIGNTOOL (
  for /d %%D in ("%ProgramFiles(x86)%\Windows Kits\10\bin\10.*") do (
    if exist "%%~fD\x64\signtool.exe" set "GF_SIGNTOOL=%%~fD\x64\signtool.exe"
  )
)
if not defined GF_SIGNTOOL (
  echo ERRO: SignTool nao encontrado. Instale o Windows SDK neste computador de compilacao.
  exit /b 1
)
if not exist "%GF_SIGNTOOL%" (
  echo ERRO: caminho do SignTool nao existe: %GF_SIGNTOOL%
  exit /b 1
)
echo.
echo Assinando: %~nx1
"%GF_SIGNTOOL%" sign /sha1 "%GF_CERT_SHA1%" /s My /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /v "%~1"
if errorlevel 1 (
  echo ERRO: falha de assinatura. Verifique certificado, acesso a chave e internet para timestamp.
  exit /b 1
)
"%GF_SIGNTOOL%" verify /pa /v "%~1"
if errorlevel 1 (
  echo ERRO: a assinatura nao foi validada pelo Windows.
  exit /b 1
)
exit /b 0
