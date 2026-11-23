# Gera as imagens das abas da ligação "PASB - Contraventamento em X" (PNG embutidos na PasbJoints.dll).
# Rodar quando mudar algum desenho:  powershell -NoProfile -File src\Pasb.Joints\Images\Make-Images.ps1
Add-Type -AssemblyName System.Drawing
$dir = $PSScriptRoot
$W = 240; $H = 260
$ink = [System.Drawing.Color]::FromArgb(18, 21, 26)
$copper = [System.Drawing.Color]::FromArgb(168, 77, 28)
$blue = [System.Drawing.Color]::FromArgb(30, 100, 160)
$steel = [System.Drawing.Color]::FromArgb(200, 205, 212)
$rodC = [System.Drawing.Color]::FromArgb(120, 170, 120)
$weldC = [System.Drawing.Color]::FromArgb(210, 40, 40)

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
function Click($g, [float]$x, [float]$y, [string]$t) {
    $g.FillEllipse((Brush ([System.Drawing.Color]::White)), $x - 5, $y - 5, 10, 10); $g.DrawEllipse((Pen $ink 1.5), $x - 5, $y - 5, 10, 10)
    Label $g $t ($x + 6) ($y - 4) $ink 7.5
}
function Save($pair, [string]$name) { $pair[1].Dispose(); $pair[0].Save((Join-Path $dir $name), [System.Drawing.Imaging.ImageFormat]::Png); $pair[0].Dispose() }

# 1 - Geral: painel em planta, X, cliques e posição do esticador
$c = New-Canvas '1 - Geral'; $g = $c[1]
$g.DrawRectangle((New-Object System.Drawing.Pen $steel, 1), 40, 40, 160, 150)
$g.DrawLine((Pen $rodC 2.5), 45, 185, 195, 45); $g.DrawLine((Pen $rodC 2.5), 45, 45, 195, 185)
$g.FillRectangle((Brush $copper), 66, 160, 22, 8)
Click $g 40 190 'clique 1'; Click $g 200 40 'clique 2'
Label $g '300' 56 172 $copper
Note $g 'plano XY da UCS, Z do 1º clique' 205
Note $g 'esticador: mm do clique (até a' 218
Note $g 'ponta ou o centro) ou % diagonal' 230
Save $c 'pasb_cv_geral.png'

# 2 - Chapa de base
$c = New-Canvas '2 - Chapa de base'; $g = $c[1]
$g.FillRectangle((Brush $steel), 50, 40, 110, 100); $g.DrawRectangle((Pen $ink 1), 50, 40, 110, 100)
DimH $g 50 160 32 'a1'; DimV $g 168 40 140 'a2'
Click $g 50 140 'clique'
$g.FillRectangle((Brush ([System.Drawing.Color]::FromArgb(220, 228, 240))), 30, 220, 180, 18); $g.DrawRectangle((Pen $ink 1), 30, 220, 180, 18); Label $g 'apoio (banzo)' 90 223 $ink 7
$g.FillRectangle((Brush $steel), 60, 206, 120, 10); $g.DrawRectangle((Pen $ink 1), 60, 206, 120, 10)
$g.DrawLine((New-Object System.Drawing.Pen $copper, 1), 20, 196, 220, 196); Label $g 'clique' 190 184 $copper 7
DimV $g 52 196 206 'a8'; DimV $g 186 206 216 'a3'
Note $g 'canto no clique, lados na viga;' 160
Note $g 'a4..a7: chanfros dos cantos' 172
Note $g 'corta no contato com o banzo' 246
Save $c 'pasb_cv_base.png'

# 3 - Gusset
$c = New-Canvas '3 - Gusset'; $g = $c[1]
$g.FillRectangle((Brush $steel), 60, 80, 130, 44); $g.DrawRectangle((Pen $ink 1), 60, 80, 130, 44)
$g.DrawLine((Pen $rodC 4), 110, 102, 232, 102); $g.DrawLine((Pen $weldC 3), 110, 98, 190, 98)
Click $g 20 102 'clique'
DimH $g 60 190 66 'b1'; DimV $g 196 80 124 'b2'; DimH $g 20 60 140 'b4'; DimH $g 110 190 140 'b7'
$g.FillRectangle((Brush $steel), 40, 200, 160, 8); $g.DrawRectangle((Pen $ink 1), 40, 200, 160, 8); DimV $g 205 200 208 'b3'
$g.DrawLine((Pen $rodC 4), 110, 196, 232, 196); Label $g 'barra por cima' 50 214 $ink 7
Note $g 'b10/b11: chanfro das pontas' 232
Save $c 'pasb_cv_gusset.png'

# 4 - Parafuso
$c = New-Canvas '4 - Parafuso'; $g = $c[1]
$g.FillRectangle((Brush $steel), 40, 70, 160, 60); $g.DrawRectangle((Pen $ink 1), 40, 70, 160, 60)
$g.FillRectangle((Brush ([System.Drawing.Color]::FromArgb(185, 190, 198))), 70, 88, 120, 24); $g.DrawRectangle((Pen $ink 1), 70, 88, 120, 24)
$g.FillEllipse((Brush $copper), 94, 94, 12, 12)
DimH $g 70 100 150 'c1'
Note $g 'liga o gusset à chapa de base' 180
Note $g 'norma, classe, diâmetro e conjunto' 194
Note $g 'vêm do catálogo do Advance Steel' 208
Save $c 'pasb_cv_parafuso.png'

# 5 - Barras
$c = New-Canvas '5 - Barras'; $g = $c[1]
$g.DrawLine((Pen $rodC 4), 10, 110, 90, 110); $g.DrawLine((Pen ([System.Drawing.Color]::FromArgb(150, 120, 90)) 4), 90, 110, 140, 110)
$g.FillRectangle((Brush $copper), 140, 100, 50, 20); Label $g 'esticador' 140 124 $ink 7
$g.FillRectangle((Brush $steel), 75, 100, 30, 6); $g.FillRectangle((Brush $steel), 75, 114, 30, 6)
Label $g 'lisa' 30 92 $ink 7; Label $g 'roscada' 92 92 $ink 7
DimH $g 75 105 150 'e1'; DimH $g 90 160 170 'd2'; DimH $g 140 160 190 'd3'
Note $g 'e0: talas na emenda  e2: perfil' 214
Note $g 'perfis e materiais do catálogo' 228
Save $c 'pasb_cv_barras.png'

# 6 - Esticador
$c = New-Canvas '6 - Esticador'; $g = $c[1]
$g.FillRectangle((Brush $steel), 40, 70, 160, 40); $g.DrawRectangle((Pen $ink 1), 40, 70, 160, 40)
$g.FillRectangle((Brush ([System.Drawing.Color]::FromArgb(230, 200, 80))), 58, 64, 7, 52); $g.FillRectangle((Brush ([System.Drawing.Color]::FromArgb(230, 200, 80))), 175, 64, 7, 52)
$g.DrawLine((Pen $rodC 4), 10, 90, 90, 90); $g.DrawLine((Pen $rodC 4), 150, 90, 230, 90)
DimH $g 40 200 56 'f3'; DimH $g 40 58 128 'f8'; DimH $g 175 182 128 'f6'
$g.FillRectangle((Brush ([System.Drawing.Color]::FromArgb(230, 200, 80))), 80, 160, 60, 56); $g.DrawRectangle((Pen $ink 1), 80, 160, 60, 56)
$g.DrawLine((Pen $ink 4), 80, 216, 140, 216); $g.DrawLine((Pen $ink 4), 80, 160, 80, 216)
DimH $g 80 140 236 'f4'; DimV $g 146 160 216 'f5'
Note $g 'f1: rotação da L' 244
Save $c 'pasb_cv_esticador.png'

# 7 - Porcas e arruelas
$c = New-Canvas '7 - Porcas e arruelas'; $g = $c[1]
$g.DrawLine((Pen $rodC 5), 10, 110, 230, 110)
$g.FillRectangle((Brush ([System.Drawing.Color]::FromArgb(230, 200, 80))), 116, 60, 8, 100); Label $g 'chapa de topo' 88 164 $ink 7
$g.FillRectangle((Brush $steel), 108, 90, 6, 40); $g.FillRectangle((Brush $steel), 126, 90, 6, 40)
$g.FillRectangle((Brush $copper), 92, 96, 14, 28); $g.FillRectangle((Brush $copper), 134, 96, 14, 28)
DimV $g 84 96 124 'g1'; DimH $g 134 148 84 'g2'; DimV $g 152 90 130 'g3'; DimH $g 126 132 140 'g5'
Note $g 'porca e arruela dos dois lados' 196
Note $g 'blocos próprios do PASB' 210
Save $c 'pasb_cv_porcas.png'

# 8 - Soldas
$c = New-Canvas '8 - Soldas'; $g = $c[1]
$g.FillRectangle((Brush $steel), 20, 60, 90, 24); $g.DrawLine((Pen $rodC 4), 50, 56, 120, 56); $g.DrawLine((Pen $weldC 3), 50, 52, 110, 52); Label $g 'h1' 70 36 $weldC
$g.DrawLine((Pen $rodC 4), 140, 70, 230, 70); $g.FillRectangle((Brush $steel), 170, 62, 30, 4); $g.FillRectangle((Brush $steel), 170, 74, 30, 4); $g.DrawLine((Pen $weldC 3), 170, 60, 200, 60); Label $g 'h2' 178 40 $weldC
$g.FillRectangle((Brush ([System.Drawing.Color]::FromArgb(230, 200, 80))), 40, 130, 8, 50); $g.FillRectangle((Brush $steel), 48, 140, 60, 30); $g.DrawLine((Pen $weldC 3), 48, 138, 48, 172); Label $g 'h3' 60 112 $weldC
$g.FillRectangle((Brush ([System.Drawing.Color]::FromArgb(220, 228, 240))), 130, 170, 100, 16); $g.FillRectangle((Brush $steel), 150, 160, 60, 10); $g.DrawLine((Pen $weldC 3), 150, 170, 210, 170); Label $g 'h4' 170 140 $weldC
Note $g 'h1 barra x gusset  h2 emenda' 206
Note $g 'h3 L x chapa de topo' 220
Note $g 'h4 chapa de base x viga de apoio' 234
Save $c 'pasb_cv_soldas.png'

Get-ChildItem $dir -Filter *.png | ForEach-Object { $_.Name }
