<#
.SYNOPSIS
  Instala o plugin PASB (Coordix · BIMOFFICE) no Advance Steel 2027 a partir deste pacote, sem compilar nada.

.DESCRIPTION
  - Copia a pasta PASB-Diagnostics.bundle para %APPDATA%\Autodesk\ApplicationPlugins (só do usuário atual; não precisa de administrador).
  - Desbloqueia os arquivos baixados da internet (marca "arquivo da internet" do Windows), para o AutoCAD conseguir carregar as DLLs.
  - Recusa instalar com o AutoCAD / Advance Steel aberto.
  - Não altera nenhum outro plugin nem configuração do AutoCAD.
#>
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$source = Join-Path $here 'PASB-Diagnostics.bundle'
$target = Join-Path $env:APPDATA 'Autodesk\ApplicationPlugins\PASB-Diagnostics.bundle'

if (-not (Test-Path (Join-Path $source 'PackageContents.xml'))) {
    throw "Pasta PASB-Diagnostics.bundle não encontrada ao lado deste script. Descompacte o .zip inteiro antes de instalar."
}
if (Get-Process -Name acad -ErrorAction SilentlyContinue) {
    throw "O AutoCAD / Advance Steel está aberto. Feche-o e rode o instalador de novo."
}

$version = ([xml](Get-Content (Join-Path $source 'PackageContents.xml') -Raw)).ApplicationPackage.AppVersion
Write-Host "PASB $version — instalando para o usuário $env:USERNAME..."

# Arquivos baixados da internet vêm bloqueados; desbloqueia só os deste pacote.
Get-ChildItem $here -Recurse -File | ForEach-Object { Unblock-File -LiteralPath $_.FullName -ErrorAction SilentlyContinue }

New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
$mcpInUse = [bool](Get-Process -Name Pasb.Mcp -ErrorAction SilentlyContinue)
if ($mcpInUse -and (Test-Path (Join-Path $target 'Contents\Mcp\Pasb.Mcp.exe'))) {
    # O Claude Code está usando o servidor do Conector IA: atualiza o plugin e mantém Contents\Mcp.
    Get-ChildItem $target -Force | Where-Object { $_.Name -ne 'Contents' } | Remove-Item -Recurse -Force
    Get-ChildItem (Join-Path $target 'Contents') -Force | Where-Object { $_.Name -ne 'Mcp' } | Remove-Item -Recurse -Force
    Get-ChildItem $source -Force | Where-Object { $_.Name -ne 'Contents' } | Copy-Item -Destination $target -Recurse
    Get-ChildItem (Join-Path $source 'Contents') -Force | Where-Object { $_.Name -ne 'Mcp' } | Copy-Item -Destination (Join-Path $target 'Contents') -Recurse
    Write-Host "Aviso: o servidor do Conector IA está em uso pelo Claude Code; ele foi mantido. Feche o Claude Code e instale de novo para atualizá-lo."
}
else {
    if ($mcpInUse) { throw "O servidor do Conector IA (Pasb.Mcp) está em uso. Feche o Claude Code e tente de novo." }
    if (Test-Path $target) { Remove-Item $target -Recurse -Force }
    Copy-Item $source $target -Recurse
}
Get-ChildItem $target -Recurse -File | ForEach-Object { Unblock-File -LiteralPath $_.FullName -ErrorAction SilentlyContinue }

Write-Host ""
Write-Host "Instalado em: $target"
Write-Host "Abra o Advance Steel 2027. No aviso de segurança do PASB, clique em 'Sempre carregar'."
Write-Host "A aba PASB aparece na faixa de opções logo depois que o programa termina de abrir."
