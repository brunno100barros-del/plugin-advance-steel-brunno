<#
.SYNOPSIS
  Gera o pacote de instalação pronto (sem compilar no destino): release\PASB-<versão>.zip.

.DESCRIPTION
  1. Compila e monta o bundle (Build-Bundle.ps1).
  2. Junta o bundle, os instaladores (Instalar/Desinstalar/Registrar Conector IA) e o LEIA-ME numa pasta PASB-<versão>.
  3. Compacta em release\PASB-<versão>.zip (apaga zips de versões anteriores da pasta release) e grava o SHA-256 ao lado.
  Não instala nada.

.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File bundle\Build-Release.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'Build-Bundle.ps1')
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "Falha ao montar o bundle." }

$bundle = Join-Path $root 'artifacts\PASB-Diagnostics.bundle'
$version = ([xml](Get-Content (Join-Path $bundle 'PackageContents.xml') -Raw)).ApplicationPackage.AppVersion
$name = "PASB-$version"
$stage = Join-Path $root "artifacts\release\$name"
if (Test-Path (Split-Path $stage)) { Remove-Item (Split-Path $stage) -Recurse -Force }
New-Item -ItemType Directory -Force $stage | Out-Null

Copy-Item $bundle (Join-Path $stage 'PASB-Diagnostics.bundle') -Recurse
$files = Join-Path $PSScriptRoot 'release-files'
$bom = New-Object System.Text.UTF8Encoding($true)
foreach ($ps in Get-ChildItem $files -Filter *.ps1) {
    # Windows PowerShell 5.1 precisa de BOM para ler acentos corretamente.
    [IO.File]::WriteAllText((Join-Path $stage $ps.Name), [IO.File]::ReadAllText($ps.FullName), $bom)
}
Copy-Item (Join-Path $files '*.cmd') $stage
$readme = [IO.File]::ReadAllText((Join-Path $files 'LEIA-ME.txt')).Replace('{{VERSION}}', $version)
[IO.File]::WriteAllText((Join-Path $stage 'LEIA-ME.txt'), $readme, (New-Object System.Text.UTF8Encoding($false)))
Copy-Item (Join-Path $root 'Registrar-Conector-IA.cmd'), (Join-Path $root 'Remover-Conector-IA.cmd'), (Join-Path $root 'Registrar-Macros-PASB.cmd'), (Join-Path $root 'Remover-Macros-PASB.cmd') $stage

$outDir = Join-Path $root 'release'
New-Item -ItemType Directory -Force $outDir | Out-Null
Get-ChildItem $outDir -Filter 'PASB-*.zip' | Remove-Item -Force
Get-ChildItem $outDir -Filter 'PASB-*.zip.sha256' | Remove-Item -Force
$zip = Join-Path $outDir "$name.zip"
Compress-Archive -Path $stage -DestinationPath $zip -CompressionLevel Optimal
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$zip.sha256", "$hash  $name.zip`n", (New-Object System.Text.UTF8Encoding($false)))

Write-Host ""
Write-Host "Pacote: $zip  ($([math]::Round((Get-Item $zip).Length / 1KB)) KB)"
Write-Host "SHA-256: $hash"
