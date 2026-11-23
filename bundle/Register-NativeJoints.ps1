<#
.SYNOPSIS
  Registra (ou remove) as ligações nativas do PASB no Advance Steel 2027.

.DESCRIPTION
  Mesmo procedimento do SDK da Autodesk ("Joints NET.docx") e o mesmo que outros fornecedores de macros usam:
    1. copia PasbJoints.dll para a pasta do Advance Steel (ADVS). Pede o administrador do Windows (UAC) só para isso;
    2. registra a ligação no banco AstorRules (tabelas RulesDllSigned e HRLDefinition);
    3. grava os nomes das abas e os textos dos cliques no banco AstCrtlDb (tabela ErrorMessages).
  Antes de alterar os bancos, faz backup completo de cada um em %LOCALAPPDATA%\PASB\backup-bancos.
  Só mexe nas linhas do PASB (chaves 880000 a 880999). Rodar de novo é seguro: as linhas do PASB são regravadas.
  O Advance Steel precisa estar FECHADO.

.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File Register-NativeJoints.ps1
  powershell -NoProfile -ExecutionPolicy Bypass -File Register-NativeJoints.ps1 -Unregister
#>
param([switch]$Unregister, [string]$JointsDll = (Join-Path $PSScriptRoot 'PasbJoints.dll'))

$ErrorActionPreference = 'Stop'
$advs = 'C:\Program Files\Autodesk\AutoCAD 2027\ADVS'
$dataRoot = 'C:\ProgramData\Autodesk\Advance Steel 2027'
$instance = 'AdvanceSteel2027'
$dllKey = 880000

if (Get-Process acad -ErrorAction SilentlyContinue) { throw 'Feche o Advance Steel antes de registrar as macros do PASB.' }
if (-not (Test-Path $advs)) { throw "Pasta do Advance Steel não encontrada: $advs" }
if (-not $Unregister -and -not (Test-Path $JointsDll)) { throw "PasbJoints.dll não encontrada: $JointsDll" }

# ---------------------------------------------------------------- textos (ErrorMessages)
$texts = @(
    @('UPS', 880000, 'PASB - Contraventamento em X'),
    @('UPS', 880001, '1 - Geral'),
    @('UPS', 880002, '2 - Chapa de base'),
    @('UPS', 880003, '3 - Gusset'),
    @('UPS', 880004, '4 - Parafuso'),
    @('UPS', 880005, '5 - Barras'),
    @('UPS', 880006, '6 - Esticador'),
    @('UPS', 880007, '7 - Porcas e arruelas'),
    @('UPS', 880008, '8 - Soldas'),
    @('GUI', 880020, 'PASB contraventamento: clique o 1º canto do painel'),
    @('GUI', 880021, 'Clique o canto oposto do painel (na diagonal)'),
    @('GUI', 880022, 'Selecione as vigas de apoio para soldar (ENTER pula)'),
    @('UPS', 880100, 'PASB - Sistema antidobra'),
    @('UPS', 880101, '1 - Filas'),
    @('UPS', 880102, '2 - Entreterças'),
    @('UPS', 880103, '3 - Barra da agulha'),
    @('UPS', 880104, '4 - Porcas e arruelas'),
    @('UPS', 880105, '5 - Agulha dupla'),
    @('UPS', 880106, '6 - Barra rígida'),
    @('UPS', 880107, '7 - Cantoneira de ligação'),
    @('UPS', 880108, '8 - Onde vai o tirante'),
    @('UPS', 880109, '9 - Barra e ponta do tirante'),
    @('UPS', 880110, '10 - Furos da agulha'),
    @('UPS', 880111, '11 - Furos do tirante'),
    @('UPS', 880112, '12 - Parafusos na terça'),
    @('UPS', 880113, '13 - Parafusos na rígida'),
    @('GUI', 880120, 'Selecione as terças entre os dois banzos e tecle ENTER'),
    @('GUI', 880121, 'PASB antidobra: clique no 1º banzo (pórtico A)'),
    @('GUI', 880122, 'Clique no 2º banzo (pórtico B)')
)

function Invoke-Sql([string]$sql, [string]$db = 'master') {
    $cn = New-Object System.Data.SqlClient.SqlConnection "Server=(localdb)\$instance;Integrated Security=true;Connect Timeout=120;Database=$db"
    $cn.Open()
    try { $cmd = $cn.CreateCommand(); $cmd.CommandTimeout = 600; $cmd.CommandText = $sql; return $cmd.ExecuteScalar() }
    finally { $cn.Close() }
}

function Sql-Text([string]$s) { "N'" + $s.Replace("'", "''") + "'" }

# Nome do banco igual ao que o Advance Steel usa ao anexar: o caminho do .mdf em maiúsculas.
function Use-Database([string]$mdf) {
    $name = $mdf.ToUpperInvariant()
    $exists = Invoke-Sql ("select count(*) from sys.databases where name = " + (Sql-Text $name))
    if ($exists -eq 0) {
        $ldf = [IO.Path]::Combine([IO.Path]::GetDirectoryName($mdf), [IO.Path]::GetFileNameWithoutExtension($mdf) + '_log.ldf')
        $files = "(FILENAME = " + (Sql-Text $mdf) + ")"
        if (Test-Path $ldf) { $files += ", (FILENAME = " + (Sql-Text $ldf) + ")" }
        Invoke-Sql ("CREATE DATABASE [" + $name + "] ON " + $files + " FOR ATTACH") | Out-Null
    }
    return $name
}

function Backup-Database([string]$name, [string]$label, [string]$folder) {
    $file = Join-Path $folder ($label + '.bak')
    Invoke-Sql ("BACKUP DATABASE [" + $name + "] TO DISK = " + (Sql-Text $file) + " WITH COPY_ONLY, INIT") | Out-Null
    Write-Host "  backup: $file"
}

# ---------------------------------------------------------------- 1. DLL na pasta ADVS (administrador)
$target = Join-Path $advs 'PasbJoints.dll'
if ($Unregister) {
    if (Test-Path $target) {
        Write-Host 'Removendo PasbJoints.dll da pasta do Advance Steel (o Windows vai pedir permissão de administrador)...'
        $p = Start-Process powershell -Verb RunAs -Wait -PassThru -ArgumentList @('-NoProfile', '-Command', "Remove-Item -LiteralPath '$target' -Force")
        if ($p.ExitCode -ne 0 -or (Test-Path $target)) { throw 'Não foi possível remover a DLL (permissão negada?).' }
    }
} else {
    $same = (Test-Path $target) -and ((Get-FileHash $target).Hash -eq (Get-FileHash $JointsDll).Hash)
    if (-not $same) {
        Write-Host 'Copiando PasbJoints.dll para a pasta do Advance Steel (o Windows vai pedir permissão de administrador)...'
        $src = (Resolve-Path $JointsDll).Path
        $p = Start-Process powershell -Verb RunAs -Wait -PassThru -ArgumentList @('-NoProfile', '-Command', "Copy-Item -LiteralPath '$src' -Destination '$target' -Force")
        if ($p.ExitCode -ne 0 -or -not (Test-Path $target)) { throw 'Não foi possível copiar a DLL (permissão negada?).' }
        if ((Get-FileHash $target).Hash -ne (Get-FileHash $JointsDll).Hash) { throw 'A DLL copiada não confere com a original.' }
    }
    Write-Host "  DLL: $target"
}

# ---------------------------------------------------------------- 2 e 3. bancos de cada país instalado
& sqllocaldb start $instance | Out-Null
$countries = Get-ChildItem $dataRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Steel\Data\AstorRules.mdf') }
if (-not $countries) { throw "Nenhum banco AstorRules encontrado em $dataRoot" }
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupDir = Join-Path $env:LOCALAPPDATA "PASB\backup-bancos\$stamp"
New-Item -ItemType Directory -Force $backupDir | Out-Null

foreach ($c in $countries) {
    Write-Host "Bancos de $($c.Name):"
    $rules = Use-Database (Join-Path $c.FullName 'Steel\Data\AstorRules.mdf')
    $crtl = Use-Database (Join-Path $c.FullName 'Steel\Data\AstCrtlDb.mdf')
    Backup-Database $rules "$($c.Name)-AstorRules" $backupDir
    Backup-Database $crtl "$($c.Name)-AstCrtlDb" $backupDir

    $sql = "BEGIN TRAN;`n" +
        "DELETE FROM [$rules].dbo.HRLDefinition WHERE [Key] BETWEEN 880000 AND 880999;`n" +
        "DELETE FROM [$rules].dbo.RulesDllSigned WHERE [Key] BETWEEN 880000 AND 880999;`n" +
        "DELETE FROM [$crtl].dbo.ErrorMessages WHERE ErrorNumber BETWEEN 880000 AND 880999 AND Note = N'PASB';`n"
    if (-not $Unregister) {
        $sql += "INSERT INTO [$rules].dbo.RulesDllSigned ([Key],[FileName],[Tech],[Signature]) VALUES ($dllKey, N'PasbJoints.dll', 2, N'');`n"
        $sql += "INSERT INTO [$rules].dbo.HRLDefinition ([Key],[RuleRunName],[InternalName],[Category],[Dll],[SubNameInDll],[ClassID],[Version]) VALUES " +
            "(880000, N'PASB - Contraventamento em X', N'PasbContraventamento', N'PASB', $dllKey, N'Pasb.Joints.BracingJoint', N'{3B9F2C71-6E54-4A8D-9C1E-58D07A2F4B16}', N'31');`n"
        $sql += "INSERT INTO [$rules].dbo.HRLDefinition ([Key],[RuleRunName],[InternalName],[Category],[Dll],[SubNameInDll],[ClassID],[Version]) VALUES " +
            "(880001, N'PASB - Sistema antidobra', N'PasbAntidobra', N'PASB', $dllKey, N'Pasb.Joints.SagRodJoint', N'{8C2E5A14-3D7B-4F90-A1C6-2B94E07D5F38}', N'31');`n"
        foreach ($t in $texts) {
            $sql += "INSERT INTO [$crtl].dbo.ErrorMessages (ErrorClass,ErrorType,ErrorNumber,ErrorText,Note) VALUES (N'$($t[0])', N'PRP', $($t[1]), " + (Sql-Text $t[2]) + ", N'PASB');`n"
        }
    }
    $sql += "COMMIT;"
    Invoke-Sql $sql | Out-Null
    $n = Invoke-Sql "select count(*) from [$rules].dbo.HRLDefinition where [Key] in (880000, 880001)"
    Write-Host ("  ligações registradas (2): " + ($(if ($n -eq 2) { 'sim' } else { 'não' })))
}

if ($Unregister) { Write-Host 'Pronto: macros do PASB removidas do Advance Steel.' }
else { Write-Host 'Pronto. Abra o Advance Steel e use PASB > Menu de ligações (Contraventamento em X e Sistema antidobra).' }
