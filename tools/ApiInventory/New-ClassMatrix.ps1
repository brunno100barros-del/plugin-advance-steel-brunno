<#
.SYNOPSIS
  Gera docs/API-CLASS-MATRIX.md a partir do CSV do ApiInventory.

.DESCRIPTION
  Somente leitura: lê inventory-output/api-2027-members.csv e escreve um Markdown.
  A coluna "MCP" vem da lista $McpReads abaixo, montada à mão a partir de
  AdvancedSteelMCP commit b1a627e, plugin/Core/Objects.cs. Atualize a lista
  se o MCP mudar.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\ApiInventory\New-ClassMatrix.ps1
#>
param(
    [string]$Csv = 'inventory-output\api-2027-members.csv',
    [string]$Out = 'docs\API-CLASS-MATRIX.md'
)

$ErrorActionPreference = 'Stop'
$ns = 'Autodesk.AdvanceSteel.'

# Classes na ordem da herança; nome curto => nome completo sem o prefixo $ns.
$Classes = [ordered]@{
    'FilerObject'                = 'CADAccess.FilerObject'
    'ConstructionElement'        = 'ConstructionTypes.ConstructionElement'
    'ActiveConstructionElement'  = 'ConstructionTypes.ActiveConstructionElement'
    'AtomicElement'              = 'ConstructionTypes.AtomicElement'
    'MainAlias'                  = 'ConstructionTypes.MainAlias'
    'Beam'                       = 'Modelling.Beam'
    'PlateBase'                  = 'Modelling.PlateBase'
    'BoltPattern'                = 'Modelling.BoltPattern'
    'ScrewBoltPattern'           = 'Modelling.ScrewBoltPattern'
    'AnchorPattern'              = 'Modelling.AnchorPattern'
    'WeldPattern'                = 'Modelling.WeldPattern'
    'SpecialPart'                = 'Modelling.SpecialPart'
    'FeatureObject'              = 'ConstructionTypes.FeatureObject'
    'PassiveConstructionObject'  = 'ConstructionTypes.PassiveConstructionObject'
    'UserAutoConstructionObject' = 'ConstructionTypes.UserAutoConstructionObject'
}

# Membros que o MCP lê, por classe declaradora (Objects.cs, commit b1a627e).
$McpReads = @{
    'FilerObject'                = 'Handle', 'Layer', 'Type'
    'ConstructionElement'        = 'Role', 'CenterPoint', 'GeomExtents'
    'AtomicElement'              = 'Denotation', 'Material', 'Coating', 'Assembly', 'GetSinglePartPositionNumber',
                                   'GetMainPartPositionNumber', 'Note', 'ItemNumber', 'IsMainPart', 'SinglePartPrefix',
                                   'MainPartPrefix', 'Volume', 'GetUserAttribute', 'GetFeatures'
    'Beam'                       = 'ProfSectionName', 'ProfSectionType', 'GetPointAtStart', 'GetPointAtEnd', 'GetLength',
                                   'GetWeight', 'ProfName', 'SysLength', 'Angle', 'RefAxis', 'GetWeightPerMeter',
                                   'GetPaintArea', 'PhysCSStart'
    'PlateBase'                  = 'Thickness', 'GetPhysLengthAndWidth', 'GetWeight', 'PlateNormal', 'GetArea',
                                   'Portioning', 'IsRectangular', 'GetBaseContourPolygon'
    'BoltPattern'                = 'NumberOfScrews', 'Center', 'Normal', 'XDirection', 'GetMidpoints'
    'ScrewBoltPattern'           = 'Standard', 'Grade', 'ScrewDiameter', 'ScrewLength', 'BoltAssembly',
                                   'AssemblyLocation', 'HoleTolerance'
    'AnchorPattern'              = 'AnchorType'
    'WeldPattern'                = 'GetSeamThickness', 'GetWeldType', 'Length', 'AssemblyLocation', 'GetCenterPoint', 'IsClosed'
    'SpecialPart'                = 'BlockName', 'Weight'
    'PassiveConstructionObject'  = 'Drivers', 'Drivens'
    'UserAutoConstructionObject' = 'PrimaryJointName', 'NSAModuleName'
}

# Métodos que alteram o modelo ou gravam em disco: nunca entram na leitura automática.
$WritePattern = '^(Set|set|Add|Del|Remove|Rem|Move|Change|Split|Connect|Transform|Adjust|Empty|Update|update|upgrade|regist|unregist|Write|Export|Create|Free|Detect|Check|Presize|Load|Save|Replace|Mark|mark)'
# Infraestrutura .NET/visitante: não são dados do objeto.
$InfraPattern = '^(Accept|Dispose|Finalize|Equals|GetHashCode|op_)'

function Get-Params([string]$sig) {
    $open = $sig.IndexOf('('); $close = $sig.LastIndexOf(')')
    if ($open -lt 0 -or $close -le $open + 1) { return @() }
    return $sig.Substring($open + 1, $close - $open - 1) -split ',\s*'
}

function Get-ReadKind($row) {
    if ($row.member_kind -eq 'property') {
        if ($row.can_read -ne '1') { return 'só escrita' }
        if ($row.signature -match '\[') { return 'indexada' }
        return 'getter'
    }
    if ($row.member -match $InfraPattern) { return 'infraestrutura' }
    if ($row.member -match $WritePattern) { return 'escrita/efeito' }
    if ($row.signature -match '^\S+\*\s') { return 'ponteiro nativo' }
    $ps = @(Get-Params $row.signature)
    if ($ps.Count -eq 0) { return 'sem parâmetros' }
    if (@($ps | Where-Object { $_ -notmatch '&\s+out\s' }).Count -eq 0) { return 'só out' }
    return 'com parâmetros'
}

function Format-Type([string]$s) {
    $s = $s -replace [regex]::Escape($ns), '' -replace 'System\.Collections\.Generic\.', '' -replace 'Geometry\.', '' -replace 'CADLink\.Database\.', ''
    return $s -replace '\|', '\|'
}

$rows = Import-Csv $Csv -Delimiter ';'
$sb = New-Object System.Text.StringBuilder
function Add-Line([string]$s = '') { [void]$sb.AppendLine($s) }

Add-Line '# Matriz detalhada por classe: API Advance Steel 2027 × AdvancedSteelMCP'
Add-Line
Add-Line '> Arquivo gerado por `tools/ApiInventory/New-ClassMatrix.ps1`. Não edite à mão: rode o script de novo.'
Add-Line
Add-Line "Fonte da API: inventário de metadados das assemblies 31.0.260.0. Fonte do MCP: AdvancedSteelMCP commit ``b1a627e``, ``plugin/Core/Objects.cs``. Gerado em $(Get-Date -Format 'yyyy-MM-dd')."
Add-Line
Add-Line 'Nenhuma linha foi validada em modelo real. "Sim" na coluna MCP só significa que o MCP chama esse membro, não que o valor esteja correto ou completo.'
Add-Line
Add-Line '## Legenda da coluna "Leitura"'
Add-Line
Add-Line '| Valor | Significado |'
Add-Line '|---|---|'
Add-Line '| getter | Propriedade legível sem argumentos: entra na enumeração automática do diagnóstico |'
Add-Line '| sem parâmetros / só out | Método de leitura chamável sem escolher argumentos: candidato à enumeração automática, após revisão |'
Add-Line '| com parâmetros | Exige escolher argumentos (índice, contexto, nível): precisa de regra específica |'
Add-Line '| indexada | Propriedade com índice |'
Add-Line '| escrita/efeito | Pelo nome, altera o modelo, grava arquivo ou dispara cálculo: excluído do diagnóstico |'
Add-Line '| infraestrutura | Métodos .NET (`Equals`, `Dispose`...) e de visitante: não são dados do objeto |'
Add-Line '| ponteiro nativo | Retorna ponteiro C++ interno: excluído |'
Add-Line
Add-Line 'A classificação é heurística, feita pelo nome e pela assinatura. Todo método candidato será revisado antes de entrar no diagnóstico.'
Add-Line '| só escrita | Propriedade sem getter |'
Add-Line
Add-Line '## Resumo'
Add-Line
Add-Line '| Classe | Getters | Lidos pelo MCP | Métodos de leitura sem argumentos | Métodos com parâmetros | Excluídos (escrita/efeito) |'
Add-Line '|---|---|---|---|---|---|'

$detail = New-Object System.Text.StringBuilder
foreach ($short in $Classes.Keys) {
    $full = $ns + $Classes[$short]
    $members = @($rows | Where-Object { $_.type -eq $full -and $_.access -eq 'public' -and $_.static -eq '0' -and $_.member_kind -in 'property', 'method' })
    $mcp = @($McpReads[$short])
    $items = foreach ($m in $members) {
        [pscustomobject]@{
            Kind = $m.member_kind; Name = $m.member; Sig = Format-Type $m.signature
            Read = Get-ReadKind $m; Rw = $(if ($m.can_write -eq '1') { 'sim' } else { '' })
            Mcp = $(if ($mcp -contains $m.member) { 'Sim' } else { '' })
        }
    }
    # Sobrecargas do mesmo método viram uma linha só.
    $items = @($items | Group-Object Kind, Name | ForEach-Object {
        $g = $_.Group
        [pscustomobject]@{
            Kind = $g[0].Kind; Name = $g[0].Name; Mcp = $g[0].Mcp; Rw = $g[0].Rw
            Read = (($g.Read | Sort-Object -Unique) -join ', ')
            Sig  = (($g.Sig | Sort-Object -Unique) -join '<br>')
        }
    } | Sort-Object Kind, Name)

    $getters = @($items | Where-Object { $_.Kind -eq 'property' -and $_.Read -eq 'getter' })
    $readM = @($items | Where-Object { $_.Kind -eq 'method' -and $_.Read -match 'sem parâmetros|só out' -and $_.Read -notmatch 'escrita' })
    $paramM = @($items | Where-Object { $_.Kind -eq 'method' -and $_.Read -eq 'com parâmetros' })
    $writeM = @($items | Where-Object { $_.Read -eq 'escrita/efeito' })
    $mcpCount = @($items | Where-Object { $_.Mcp }).Count
    Add-Line "| [$short](#$($short.ToLower())) | $($getters.Count) | $mcpCount | $($readM.Count) | $($paramM.Count) | $($writeM.Count) |"

    [void]$detail.AppendLine("## $short")
    [void]$detail.AppendLine()
    [void]$detail.AppendLine("``$full``")
    [void]$detail.AppendLine()
    [void]$detail.AppendLine('| Membro | Tipo | Assinatura | Leitura | Gravável | MCP |')
    [void]$detail.AppendLine('|---|---|---|---|---|---|')
    foreach ($i in $items) {
        $k = $(if ($i.Kind -eq 'property') { 'propriedade' } else { 'método' })
        $sigCell = '`' + ($i.Sig -replace '<br>', '`<br>`') + '`'
        [void]$detail.AppendLine("| ``$($i.Name)`` | $k | $sigCell | $($i.Read) | $($i.Rw) | $($i.Mcp) |")
    }
    [void]$detail.AppendLine()
}

Add-Line
Add-Line 'Os números contam só membros **declarados na própria classe**; os herdados aparecem na seção da classe-base.'
Add-Line
[void]$sb.Append($detail.ToString())

$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path (Get-Location) $Out), $sb.ToString(), $utf8)
Write-Host "Gerado: $Out"
