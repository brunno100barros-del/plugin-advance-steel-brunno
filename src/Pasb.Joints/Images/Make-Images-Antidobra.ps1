# Gera as imagens das abas da ligação "PASB - Sistema antidobra" (PNG embutidos na PasbJoints.dll).
# Rodar quando mudar algum desenho:  powershell -NoProfile -File src\Pasb.Joints\Images\Make-Images-Antidobra.ps1
Add-Type -AssemblyName System.Drawing
$dir = $PSScriptRoot
$W = 240; $H = 260
$ink = [System.Drawing.Color]::FromArgb(18, 21, 26)
$copper = [System.Drawing.Color]::FromArgb(168, 77, 28)
$blue = [System.Drawing.Color]::FromArgb(30, 100, 160)
$steel = [System.Drawing.Color]::FromArgb(200, 205, 212)
$rodC = [System.Drawing.Color]::FromArgb(120, 170, 120)
$thrC = [System.Drawing.Color]::FromArgb(150, 120, 90)
$weldC = [System.Drawing.Color]::FromArgb(210, 40, 40)
$angC = [System.Drawing.Color]::FromArgb(230, 200, 80)

function New-Canvas([string]$title) {
    $bmp = New-Object System.Drawing.Bitmap $W, $H
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'ClearTypeGridFit'
    $g.Clear([System.Drawing.Color]::White)
    $f = New-Object System.Drawing.Font 'Segoe UI', 9, ([System.Drawing.FontStyle]::Bold)
    $g.DrawString($title, $f, (New-Object System.Drawing.SolidBrush $ink), 8, 6)
    return @($bmp, $g)
}
function Pen($c, [float]$w = 1.5) { New-Object System.Drawing.Pen $c, $w }
function Brush($c) { New-Object System.Drawing.SolidBrush $c }
function Label($g, [string]$t, [float]$x, [float]$y, $c = $blue, [float]$size = 8) {
    $f = New-Object System.Drawing.Font 'Consolas', $size, ([System.Drawing.FontStyle]::Bold)
    $g.DrawString($t, $f, (Brush $c), $x, $y)
}
function Note($g, [string]$t, [float]$y) {
    $f = New-Object System.Drawing.Font 'Consolas', 7.5
    $g.DrawString($t, $f, (Brush $blue), 8, $y)
}
function DimH($g, [float]$x1, [float]$x2, [float]$y, [string]$t) {
    $p = Pen $blue 1; $g.DrawLine($p, $x1, $y, $x2, $y); $g.DrawLine($p, $x1, $y - 4, $x1, $y + 4); $g.DrawLine($p, $x2, $y - 4, $x2, $y + 4)
    Label $g $t (($x1 + $x2) / 2 - 8) ($y - 15)
}
function DimV($g, [float]$x, [float]$y1, [float]$y2, [string]$t) {
    $p = Pen $blue 1; $g.DrawLine($p, $x, $y1, $x, $y2); $g.DrawLine($p, $x - 4, $y1, $x + 4, $y1); $g.DrawLine($p, $x - 4, $y2, $x + 4, $y2)
    Label $g $t ($x + 3) (($y1 + $y2) / 2 - 6)
}
function Save($pair, [string]$name) { $pair[1].Dispose(); $pair[0].Save((Join-Path $dir $name), [System.Drawing.Imaging.ImageFormat]::Png); $pair[0].Dispose() }
# Planta do telhado: pórticos A e B nas laterais, 3 terças (horizontais), 3 filas (verticais tracejadas).
function Plan($g, [float]$top = 40) {
    $frame = Pen $ink 3; $g.DrawLine($frame, 20, $top, 20, $top + 120); $g.DrawLine($frame, 220, $top, 220, $top + 120)
    Label $g 'A' 14 ($top - 14) $ink; Label $g 'B' 214 ($top - 14) $ink
    foreach ($y in @(($top + 110), ($top + 60), ($top + 10))) { $g.DrawLine((Pen $blue 2.5), 20, $y, 220, $y) }
    $dash = Pen $steel 1; $dash.DashStyle = 'Dash'
    foreach ($x in @(70, 120, 170)) { $g.DrawLine($dash, $x, $top - 4, $x, $top + 124) }
}

# 1 - Filas
$c = New-Canvas '1 - Filas'; $g = $c[1]; Plan $g 40
foreach ($x in @(70, 120, 170)) { $g.DrawLine((Pen $rodC 2.5), $x, 150, $x, 100) }
DimH $g 20 70 180 '3'; Label $g 'sobe' 200 112 $ink 7
Note $g '1 nº de fileiras ou espaçamento' 200
Note $g '4 vão entre eixos ou faces' 214
Note $g 'filas iguais entre os pórticos' 228
Save $c 'pasb_ad_filas.png'

# 2 - Entreterças
$c = New-Canvas '2 - Entreterças'; $g = $c[1]; Plan $g 40
Label $g '1' 2 122 $copper; Label $g '2' 2 72 $copper
$g.FillRectangle((Brush $angC), 66, 52, 8, 46); $g.FillRectangle((Brush $angC), 116, 52, 8, 46); $g.FillRectangle((Brush $angC), 166, 52, 8, 46)
Note $g 'entreterça 1 = entre as 2 de baixo' 190
Note $g '2 rígidas: ex. "1 6 T" (T = topo)' 204
Note $g '3 sem agulha: ex. "5 8"' 218
Save $c 'pasb_ad_entreter.png'

# 3 - Barra da agulha
$c = New-Canvas '3 - Barra da agulha'; $g = $c[1]
$g.FillRectangle((Brush $steel), 40, 50, 6, 60); $g.FillRectangle((Brush $steel), 194, 50, 6, 60)
$g.DrawLine((Pen $thrC 4), 14, 80, 66, 80); $g.DrawLine((Pen $rodC 4), 66, 80, 174, 80); $g.DrawLine((Pen $thrC 4), 174, 80, 226, 80)
$g.DrawLine((Pen $weldC 3), 64, 74, 68, 86); $g.DrawLine((Pen $weldC 3), 172, 74, 176, 86)
DimH $g 14 66 40 '5'; DimH $g 14 40 112 '6'; DimH $g 200 226 112 '7'
Label $g 'lisa' 104 64 $ink 7; Label $g 'alma' 30 116 $ink 7
$g.DrawLine((Pen $rodC 4), 50, 170, 180, 170); $g.DrawLine((Pen $thrC 4), 120, 176, 226, 176); $g.FillRectangle((Brush $steel), 200, 150, 6, 40)
DimH $g 120 180 150 '8'; DimH $g 180 200 200 '11'
Note $g '8 de topo ou por traspasse' 214
Note $g '9 cordão  10 espaço (0 = 5φ)' 228
Save $c 'pasb_ad_agulha.png'

# 4 - Porcas e arruelas
$c = New-Canvas '4 - Porcas e arruelas'; $g = $c[1]
$g.DrawLine((Pen $thrC 5), 20, 110, 220, 110)
$g.FillRectangle((Brush $steel), 117, 50, 6, 120); Label $g 'alma da terça' 86 174 $ink 7
$g.FillRectangle((Brush $steel), 110, 92, 6, 36); $g.FillRectangle((Brush $steel), 124, 92, 6, 36)
$g.FillRectangle((Brush $copper), 96, 98, 14, 24); $g.FillRectangle((Brush $copper), 130, 98, 14, 24)
Label $g 'antes' 60 80 $ink 7; Label $g 'depois' 150 80 $ink 7
Note $g '1 antes / depois / dos dois lados' 200
Note $g '"sobe" = da terça de baixo para a' 214
Note $g 'de cima. Blocos próprios do PASB' 228
Save $c 'pasb_ad_porcas.png'

# 5 - Agulha dupla
$c = New-Canvas '5 - Agulha dupla'; $g = $c[1]
$g.FillRectangle((Brush $steel), 30, 50, 6, 100); $g.FillRectangle((Brush $steel), 204, 50, 6, 100)
$g.DrawLine((Pen $rodC 3), 36, 70, 204, 130); $g.DrawLine((Pen $rodC 3), 36, 130, 204, 70)
DimV $g 22 50 70 '2'; DimV $g 22 130 150 '3'
Note $g '1 duas agulhas cruzadas na alma' 190
Note $g '2 cota da mesa de cima' 204
Note $g '3 cota da mesa de baixo' 218
Save $c 'pasb_ad_dupla.png'

# 6 - Barra rígida
$c = New-Canvas '6 - Barra rígida'; $g = $c[1]
$g.FillRectangle((Brush $steel), 30, 50, 6, 70); $g.FillRectangle((Brush $steel), 204, 50, 6, 70)
$g.FillRectangle((Brush $angC), 39, 78, 162, 8); Label $g '1 rígida' 96 64 $ink 7
DimH $g 36 39 100 '3'; DimH $g 201 204 100 '3'
$g.FillRectangle((Brush $angC), 90, 170, 60, 7); $g.FillRectangle((Brush $angC), 90, 170, 7, 40)
Label $g 'aba em cima, abre p/ lado' 70 214 $ink 7
DimV $g 160 170 210 '4'
Note $g '6 deslocamento ao longo da terça' 232
Save $c 'pasb_ad_rigida.png'

# 7 - Cantoneira de ligação
$c = New-Canvas '7 - Cantoneira de ligação'; $g = $c[1]
$g.FillRectangle((Brush $steel), 40, 40, 8, 120); Label $g 'alma' 30 164 $ink 7
$g.FillRectangle((Brush $angC), 48, 60, 7, 60); $g.FillRectangle((Brush $angC), 48, 60, 50, 7)
$g.FillRectangle((Brush ([System.Drawing.Color]::FromArgb(205, 175, 60))), 98, 60, 110, 7); Label $g 'rígida' 140 46 $ink 7
$g.FillEllipse((Brush $copper), 40, 84, 16, 10)
DimV $g 30 60 120 '3'; DimH $g 48 98 140 '4'
$g.DrawLine((Pen $weldC 3), 98, 58, 98, 70)
Note $g '5 rígida por fora / por dentro' 190
Note $g '6 soldada ou parafusada na rígida' 204
Note $g '8 solda  7 ponta ao parafuso' 218
Save $c 'pasb_ad_cantoneira.png'

# 8 - Onde vai o tirante
$c = New-Canvas '8 - Onde vai o tirante'; $g = $c[1]; Plan $g 40
$g.DrawLine((Pen $copper 2.5), 30, 100, 60, 50); $g.DrawLine((Pen $copper 2.5), 80, 100, 110, 50)
$g.DrawLine((Pen $copper 2.5), 130, 50, 160, 100); $g.DrawLine((Pen $copper 2.5), 180, 50, 210, 100)
DimH $g 20 30 180 '6'; DimH $g 60 70 196 '5'
Note $g '1 vãos "1 2"  2 entreterças "T"' 214
Note $g '3-4 S sobe / D desce / X' 228
Save $c 'pasb_ad_tirante.png'

# 9 - Barra e ponta do tirante
$c = New-Canvas '9 - Barra e ponta do tirante'; $g = $c[1]
$g.FillRectangle((Brush $steel), 150, 40, 6, 120)
$g.DrawLine((Pen $copper 4), 20, 150, 130, 100); $g.DrawLine((Pen $thrC 4), 130, 100, 210, 100)
$g.FillRectangle((Brush $copper), 140, 92, 8, 16); $g.FillRectangle((Brush $copper), 158, 92, 8, 16)
$g.DrawLine((Pen $weldC 3), 128, 94, 132, 106)
DimH $g 130 210 76 '6'; DimH $g 130 150 124 '7'
Note $g '1 furo na alma + barra roscada' 190
Note $g '2-5 perfis e materiais (catálogo)' 204
Note $g 'ponta perpendicular à alma' 218
Save $c 'pasb_ad_ponta.png'

# 10 - Furos da agulha
$c = New-Canvas '10 - Furos da agulha'; $g = $c[1]
$g.FillRectangle((Brush $steel), 20, 50, 200, 70); Label $g 'alma da terça' 80 124 $ink 7
$g.FillEllipse((Brush ([System.Drawing.Color]::White)), 76, 79, 12, 12); $g.DrawEllipse((Pen $ink 1), 76, 79, 12, 12)
$g.FillEllipse((Brush ([System.Drawing.Color]::White)), 146, 79, 12, 12); $g.DrawEllipse((Pen $ink 1), 146, 79, 12, 12)
$d = Pen $ink 1; $d.DashStyle = 'Dash'; $g.DrawLine($d, 117, 44, 117, 126); Label $g 'fila' 108 30 $ink 7
DimH $g 82 117 150 '7'; DimH $g 117 152 168 '8'; DimV $g 226 50 85 '6'
Note $g '1-4 redondo/oblongo, Ø, direção' 196
Note $g '5 centro da alma ou cota da mesa' 210
Note $g '7 entreterça par  8 ímpar' 224
Save $c 'pasb_ad_furos_agulha.png'

# 11 - Furos do tirante
$c = New-Canvas '11 - Furos do tirante'; $g = $c[1]
$g.FillRectangle((Brush $steel), 20, 50, 200, 70); Label $g 'alma da terça' 80 124 $ink 7
$g.FillEllipse((Brush ([System.Drawing.Color]::White)), 70, 79, 12, 12); $g.DrawEllipse((Pen $ink 1), 70, 79, 12, 12)
$g.FillRectangle((Brush ([System.Drawing.Color]::White)), 140, 79, 30, 12); $g.DrawRectangle((Pen $ink 1), 140, 79, 30, 12)
Note $g '1-4 furo da ponta roscada' 196
Note $g 'redondo ou oblongo, Ø e direção' 210
Save $c 'pasb_ad_furos_tirante.png'

# 12 - Parafusos na terça
$c = New-Canvas '12 - Parafusos na terça'; $g = $c[1]
$g.FillRectangle((Brush $angC), 90, 50, 50, 110); $g.DrawRectangle((Pen $ink 1), 90, 50, 50, 110)
$g.FillEllipse((Brush $copper), 108, 84, 14, 14); $g.FillEllipse((Brush $copper), 108, 114, 14, 14)
DimV $g 150 91 121 '2'
Note $g 'cantoneira de ligação x alma' 190
Note $g '1 nº  3 folga do furo' 204
Note $g 'norma/classe/Ø/conjunto: catálogo' 218
Save $c 'pasb_ad_paraf_terca.png'

# 13 - Parafusos na rígida
$c = New-Canvas '13 - Parafusos na rígida'; $g = $c[1]
$g.FillRectangle((Brush $angC), 40, 90, 160, 30); $g.DrawRectangle((Pen $ink 1), 40, 90, 160, 30)
$g.FillEllipse((Brush $copper), 60, 98, 14, 14)
Note $g 'só com a cantoneira parafusada' 190
Note $g 'na rígida (aba 7, item 6)' 204
Note $g '1 nº  3 vertical/horizontal  4 folga' 218
Save $c 'pasb_ad_paraf_rigida.png'

Get-ChildItem $dir -Filter 'pasb_ad_*.png' | ForEach-Object { $_.Name }
