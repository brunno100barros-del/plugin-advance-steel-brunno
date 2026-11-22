@echo off
rem Registra as macros nativas do PASB (contraventamento) no Advance Steel 2027.
rem Copia PasbJoints.dll para a pasta ADVS (pede administrador) e registra no AstorRules, com backup dos bancos antes.
rem Rode com o Advance Steel FECHADO, depois de Instalar-PASB.cmd.
set "PS1=%APPDATA%\Autodesk\ApplicationPlugins\PASB-Diagnostics.bundle\Contents\Joints\Register-NativeJoints.ps1"
if not exist "%PS1%" (
  echo O plugin nao esta instalado. Rode antes Instalar-PASB.cmd.
  pause
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%PS1%"
if errorlevel 1 goto erro
pause
exit /b 0
:erro
echo.
echo Falhou. Veja a mensagem acima.
pause
exit /b 1
