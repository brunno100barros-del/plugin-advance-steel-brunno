<#
.SYNOPSIS
  Remove o plugin PASB do Advance Steel 2027 (apaga só a pasta PASB-Diagnostics.bundle do usuário atual).
  As saídas em Documentos\PASB-Diagnostics e as configurações em %APPDATA%\PASB são mantidas.
#>
$ErrorActionPreference = 'Stop'
$target = Join-Path $env:APPDATA 'Autodesk\ApplicationPlugins\PASB-Diagnostics.bundle'
if (Get-Process -Name acad -ErrorAction SilentlyContinue) { throw "O AutoCAD / Advance Steel está aberto. Feche-o e tente de novo." }
if (Get-Process -Name Pasb.Mcp -ErrorAction SilentlyContinue) { throw "O servidor do Conector IA está em uso pelo Claude Code. Feche o Claude Code e tente de novo." }
if (Test-Path $target) { Remove-Item $target -Recurse -Force; Write-Host "PASB removido de: $target" }
else { Write-Host "O PASB não estava instalado para este usuário." }
