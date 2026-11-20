<#
.SYNOPSIS
  Roda todos os testes automáticos do projeto (sem Advance Steel).

.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File run-tests.ps1
#>
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'; $env:DOTNET_NOLOGO = '1'
$projects = 'tests\Pasb.Diagnostics.Core.Tests', 'tests\Pasb.Model.Tests', 'tests\Pasb.Georeferencing.Tests', 'tests\Pasb.IfcExport.Tests', 'tests\Pasb.Ui.Tests', 'tests\Pasb.Connector.Tests'
# O teste de ponta a ponta do Conector IA inicia o Pasb.Mcp.exe compilado.
& dotnet build (Join-Path $PSScriptRoot 'src\Pasb.Mcp') -c Release -nologo -v q | Out-Null
$failed = 0
foreach ($p in $projects) {
    Write-Host "=== $p"
    & dotnet run --project (Join-Path $PSScriptRoot $p) -c Release | Select-Object -Last 1
    if ($LASTEXITCODE -ne 0) { $failed++ }
}
if ($failed -gt 0) { Write-Host "$failed projeto(s) de teste falharam."; exit 1 }
Write-Host 'Todos os testes passaram.'
