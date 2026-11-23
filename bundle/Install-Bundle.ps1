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

$mcpInUse = [bool](Get-Process -Name Pasb.Mcp -ErrorAction SilentlyContinue)
$targetMcp = Join-Path $target 'Contents\Mcp'

if ($mcpInUse -and (Test-Path (Join-Path $targetMcp 'Pasb.Mcp.exe'))) {
    # O Claude Code está usando o servidor do Conector IA (arquivos travados): atualiza o plugin e mantém Contents\Mcp.
    Get-ChildItem $target -Force | Where-Object { $_.Name -ne 'Contents' } | Remove-Item -Recurse -Force
    Get-ChildItem (Join-Path $target 'Contents') -Force | Where-Object { $_.Name -ne 'Mcp' } | Remove-Item -Recurse -Force
    Get-ChildItem $source -Force | Where-Object { $_.Name -ne 'Contents' } | Copy-Item -Destination $target -Recurse
    Get-ChildItem (Join-Path $source 'Contents') -Force | Where-Object { $_.Name -ne 'Mcp' } | Copy-Item -Destination (Join-Path $target 'Contents') -Recurse
    Write-Host "Aviso: o servidor do Conector IA está em uso pelo Claude Code; a pasta Contents\Mcp foi mantida (só o plugin foi atualizado)."
    Write-Host "       Para atualizar também o servidor, feche o Claude Code e rode o instalador de novo."
}
else {
    if ($mcpInUse) { throw "O servidor do Conector IA (Pasb.Mcp) está em uso. Feche o Claude Code e tente de novo." }
    if (Test-Path $target) { Remove-Item $target -Recurse -Force }
    Copy-Item $source $target -Recurse
}
Write-Host "Instalado em: $target"
Write-Host "Abra o Advance Steel 2027: a aba PASB aparece na faixa de opções."
Write-Host "Para o Conector IA, rode uma vez Registrar-Conector-IA.cmd (registra o servidor pasb no Claude Code)."
