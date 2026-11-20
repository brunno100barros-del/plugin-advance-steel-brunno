@echo off
rem Remove o servidor MCP "pasb" da configuracao do Claude Code (faz copia de seguranca antes).
set "MCP=%APPDATA%\Autodesk\ApplicationPlugins\PASB-Diagnostics.bundle\Contents\Mcp\Pasb.Mcp.exe"
if not exist "%MCP%" (
  echo Pasb.Mcp.exe nao encontrado; o plugin nao esta instalado.
  pause
  exit /b 1
)
"%MCP%" --unregister
pause
