@echo off
rem Instala o plugin PASB (Coordix - BIMOFFICE) no Advance Steel 2027. Feche o Advance Steel antes.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Instalar-PASB.ps1"
if errorlevel 1 (
  echo.
  echo Falhou. Leia a mensagem acima: normalmente basta fechar o Advance Steel e tentar de novo.
  pause
  exit /b 1
)
echo.
echo Pronto.
pause
