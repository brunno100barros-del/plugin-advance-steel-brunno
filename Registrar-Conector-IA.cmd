@echo off
rem Registra o servidor MCP "pasb" (Conector IA) na configuracao de usuario do Claude Code (~\.claude.json).
rem Altera somente a entrada "pasb" e faz copia de seguranca do arquivo antes. Rode depois de Instalar-PASB.cmd.
set "MCP=%APPDATA%\Autodesk\ApplicationPlugins\PASB-Diagnostics.bundle\Contents\Mcp\Pasb.Mcp.exe"
if not exist "%MCP%" (
  echo O plugin nao esta instalado. Rode antes Instalar-PASB.cmd.
  pause
  exit /b 1
)
"%MCP%" --register
if errorlevel 1 goto erro
echo.
echo Pronto. Reinicie o Claude Code (feche e abra o app) para ele enxergar o servidor "pasb".
echo No Advance Steel, clique em PASB ^> Conector IA para ligar a ponte.
pause
exit /b 0
:erro
echo.
echo Falhou. Nada foi alterado se a mensagem acima disser isso.
pause
exit /b 1
