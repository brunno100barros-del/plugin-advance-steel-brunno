<#
.SYNOPSIS
  Compila o diagnóstico e monta a pasta do bundle em artifacts\PASB-Diagnostics.bundle.

.DESCRIPTION
  Não instala nada: só compila e copia arquivos para dentro do repositório (pasta artifacts, ignorada pelo git).
  Para instalar, use Install-Bundle.ps1, com o Advance Steel fechado e com autorização do desenvolvedor.

.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File bundle\Build-Bundle.ps1
#>
param([string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\Pasb.Diagnostics.AdvanceSteel\Pasb.Diagnostics.AdvanceSteel.csproj'
$binDir = Join-Path $root "src\Pasb.Diagnostics.AdvanceSteel\bin\$Configuration"
$bundle = Join-Path $root 'artifacts\PASB-Diagnostics.bundle'

& dotnet build $project -c $Configuration -nologo
if ($LASTEXITCODE -ne 0) { throw "Falha na compilação (código $LASTEXITCODE)." }

if (Test-Path $bundle) { Remove-Item $bundle -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $bundle 'Contents') | Out-Null

Copy-Item (Join-Path $PSScriptRoot 'PASB-Diagnostics\PackageContents.xml') $bundle
# Só as DLLs do projeto; nenhuma DLL da Autodesk vai para o bundle.
foreach ($name in 'Pasb.Diagnostics.AdvanceSteel.dll', 'Pasb.Diagnostics.Core.dll', 'Pasb.Model.dll', 'Pasb.Georeferencing.dll', 'Pasb.IfcExport.dll', 'Pasb.Ui.dll', 'Pasb.Diagnostics.AdvanceSteel.runtimeconfig.json') {
    Copy-Item (Join-Path $binDir $name) (Join-Path $bundle 'Contents')
}

Write-Host "Bundle montado em: $bundle"
Get-ChildItem $bundle -Recurse -File | ForEach-Object { Write-Host ('  ' + $_.FullName.Substring($bundle.Length + 1)) }
