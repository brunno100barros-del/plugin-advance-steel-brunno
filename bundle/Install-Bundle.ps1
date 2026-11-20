<#
.SYNOPSIS
  Instala o bundle montado em %APPDATA%\Autodesk\ApplicationPlugins\PASB-Diagnostics.bundle.

.DESCRIPTION
  Executar só com autorização do desenvolvedor e com o Advance Steel FECHADO.
  Substitui apenas a pasta PASB-Diagnostics.bundle; não toca em outros bundles (SteelX, MCP...).
  Para remover: Uninstall-Bundle.ps1.
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root 'artifacts\PASB-Diagnostics.bundle'
$target = Join-Path $env:APPDATA 'Autodesk\ApplicationPlugins\PASB-Diagnostics.bundle'

if (-not (Test-Path (Join-Path $source 'PackageContents.xml'))) { throw "Bundle não montado. Rode antes: bundle\Build-Bundle.ps1" }
if (Get-Process -Name acad -ErrorAction SilentlyContinue) { throw "O AutoCAD/Advance Steel está aberto. Feche-o antes de instalar." }

if (Test-Path $target) { Remove-Item $target -Recurse -Force }
Copy-Item $source $target -Recurse
Write-Host "Instalado em: $target"
Write-Host "Abra o Advance Steel 2027 e digite PASB_DIAG_VERSION para conferir."
