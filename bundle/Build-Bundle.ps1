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
foreach ($name in 'Pasb.Diagnostics.AdvanceSteel.dll', 'Pasb.Diagnostics.Core.dll', 'Pasb.Model.dll', 'Pasb.Georeferencing.dll', 'Pasb.IfcExport.dll', 'Pasb.Ui.dll', 'Pasb.Bridge.dll', 'Pasb.Connector.dll', 'Pasb.IfcImport.Model.dll', 'Pasb.Diagnostics.AdvanceSteel.runtimeconfig.json') {
    Copy-Item (Join-Path $binDir $name) (Join-Path $bundle 'Contents')
}

# Servidor MCP do Conector IA (programa separado, iniciado pelo assistente de IA): Contents\Mcp
$mcpProject = Join-Path $root 'src\Pasb.Mcp\Pasb.Mcp.csproj'
& dotnet publish $mcpProject -c $Configuration -nologo -o (Join-Path $bundle 'Contents\Mcp')
if ($LASTEXITCODE -ne 0) { throw "Falha ao publicar o servidor MCP (código $LASTEXITCODE)." }
Get-ChildItem (Join-Path $bundle 'Contents\Mcp') -Filter *.pdb | Remove-Item

# Leitor de IFC da importação (xBIM, programa separado, só win-x64): Contents\IfcImport
$impProject = Join-Path $root 'src\Pasb.IfcImport\Pasb.IfcImport.csproj'
& dotnet publish $impProject -c $Configuration -nologo -r win-x64 --self-contained false -o (Join-Path $bundle 'Contents\IfcImport')
if ($LASTEXITCODE -ne 0) { throw "Falha ao publicar o leitor de IFC (código $LASTEXITCODE)." }
Get-ChildItem (Join-Path $bundle 'Contents\IfcImport') -Recurse -Filter *.pdb | Remove-Item
Get-ChildItem (Join-Path $bundle 'Contents\IfcImport') -Recurse -Filter *.xml | Remove-Item
# O leitor roda sempre em 64 bits: a cópia 32 bits do OpenCascade (win-x86, ~44 MB) não é usada.
$x86 = Join-Path $bundle 'Contents\IfcImport\win-x86'; if (Test-Path $x86) { Remove-Item $x86 -Recurse -Force }
Copy-Item (Join-Path $root 'THIRD-PARTY-NOTICES.md') (Join-Path $bundle 'Contents\IfcImport')

# Ligações nativas (PasbJoints.dll): ficam no bundle e o Registrar-Macros-PASB.cmd as copia para a pasta ADVS.
$jointsProject = Join-Path $root 'src\Pasb.Joints\Pasb.Joints.csproj'
& dotnet build $jointsProject -c $Configuration -nologo
if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar as ligações nativas (código $LASTEXITCODE)." }
New-Item -ItemType Directory -Force (Join-Path $bundle 'Contents\Joints') | Out-Null
Copy-Item (Join-Path $root "src\Pasb.Joints\bin\$Configuration\PasbJoints.dll") (Join-Path $bundle 'Contents\Joints')
Copy-Item (Join-Path $PSScriptRoot 'Register-NativeJoints.ps1') (Join-Path $bundle 'Contents\Joints')

Write-Host "Bundle montado em: $bundle"
Get-ChildItem $bundle -Recurse -File | ForEach-Object { Write-Host ('  ' + $_.FullName.Substring($bundle.Length + 1)) }
