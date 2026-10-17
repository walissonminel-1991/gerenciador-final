@echo off
setlocal EnableExtensions DisableDelayedExpansion
if "%~1"=="" (
  echo ERRO: informe o instalador final.
  exit /b 1
)
if not exist "%~1" (
  echo ERRO: instalador final nao encontrado.
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
  echo ERRO: SignTool nao encontrado.
  exit /b 1
)
set "EXE=%~dp0Distribuicao\GerenciadorFinal.exe"
set "DLL=%~dp0Distribuicao\GerenciadorFinal.dll"
for %%F in ("%EXE%" "%DLL%" "%~1") do (
  if not exist "%%~fF" (
    echo ERRO: arquivo inexistente %%~fF
    exit /b 1
  )
  echo Verificando assinatura: %%~nxF
  "%GF_SIGNTOOL%" verify /pa /v "%%~fF"
  if errorlevel 1 exit /b 1
)
set "HASHES=%~dp1SHA256-GerenciadorFinal-v5.12.3.txt"
>"%HASHES%" echo Gerenciador Final v5.12.3 - hashes apos assinatura final.
for %%F in ("%EXE%" "%DLL%" "%~1") do (
  >>"%HASHES%" echo.
  >>"%HASHES%" echo ARQUIVO: %%~nxF
  certutil -hashfile "%%~fF" SHA256 >>"%HASHES%"
  if errorlevel 1 exit /b 1
)
echo.
echo Assinaturas verificadas e hashes salvos em:
echo %HASHES%
exit /b 0
