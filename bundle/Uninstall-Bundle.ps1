<#
.SYNOPSIS
  Remove o bundle PASB-Diagnostics de %APPDATA%\Autodesk\ApplicationPlugins.

.DESCRIPTION
  Executar com o Advance Steel FECHADO. Apaga apenas a pasta PASB-Diagnostics.bundle.
  Não apaga as saídas já geradas em Documentos\PASB-Diagnostics.
#>
$ErrorActionPreference = 'Stop'
$target = Join-Path $env:APPDATA 'Autodesk\ApplicationPlugins\PASB-Diagnostics.bundle'

if (Get-Process -Name acad -ErrorAction SilentlyContinue) { throw "O AutoCAD/Advance Steel está aberto. Feche-o antes de remover." }
if (-not (Test-Path $target)) { Write-Host "Nada a remover: $target não existe."; return }

Remove-Item $target -Recurse -Force
Write-Host "Removido: $target"
