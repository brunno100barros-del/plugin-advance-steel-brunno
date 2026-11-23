@echo off
rem Instala (ou atualiza) o plugin PASB no Advance Steel 2027.
rem Feche o Advance Steel antes. Depois, abra o Advance Steel: a aba PASB aparece na faixa de opcoes.
echo Montando o plugin...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0bundle\Build-Bundle.ps1"
if errorlevel 1 goto erro
echo.
echo Instalando...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0bundle\Install-Bundle.ps1"
if errorlevel 1 goto erro
echo.
echo Pronto. Abra o Advance Steel 2027 e procure a aba PASB.
pause
exit /b 0
:erro
echo.
echo Falhou. Verifique se o Advance Steel esta fechado e tente de novo.
pause
exit /b 1
