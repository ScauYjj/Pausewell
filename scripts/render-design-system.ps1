param()
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationCore,PresentationFramework,WindowsBase
$specRoot=Split-Path -Parent $PSScriptRoot
$tokens=Get-Content -LiteralPath (Join-Path $specRoot 'design/system/v0.3/tokens.json') -Raw -Encoding UTF8|ConvertFrom-Json
function Spec-Brush([string]$color){return [Windows.Media.BrushConverter]::new().ConvertFromString($color)}
function Spec-Text([string]$text,[double]$size=14,[string]$color='#292824',[bool]$bold=$false){$t=[Windows.Controls.TextBlock]::new();$t.Text=$text;$t.FontFamily=[Windows.Media.FontFamily]::new('Microsoft YaHei UI');$t.FontSize=$size;$t.Foreground=Spec-Brush $color;$t.TextWrapping='Wrap';if($bold){$t.FontWeight='SemiBold'};return $t}
function Spec-Place($parent,$child,[double]$x,[double]$y,[double]$width=-1,[double]$height=-1){[Windows.Controls.Canvas]::SetLeft($child,$x);[Windows.Controls.Canvas]::SetTop($child,$y);if($width -ge 0){$child.Width=$width};if($height -ge 0){$child.Height=$height};[void]$parent.Children.Add($child)}
function Spec-Panel([double]$width,[double]$height){$p=[Windows.Controls.Border]::new();$p.Width=$width;$p.Height=$height;$p.CornerRadius=$tokens.radius.card;$p.Background=Spec-Brush '#FCFBF8';$p.BorderBrush=Spec-Brush $tokens.color.border;$p.BorderThickness=1;$c=[Windows.Controls.Canvas]::new();$p.Child=$c;return $p}
function Spec-Label($parent,[string]$text,[double]$x,[double]$y,[double]$size=14,[string]$color='#292824',[bool]$bold=$false,[double]$width=-1){Spec-Place $parent (Spec-Text $text $size $color $bold) $x $y $width}
function Spec-Button([string]$text,[string]$bg,[string]$fg='#FFFFFF',[bool]$focus=$false){$b=[Windows.Controls.Button]::new();$b.Content=$text;$b.Background=Spec-Brush $bg;$b.Foreground=Spec-Brush $fg;$b.BorderBrush=Spec-Brush $bg;$b.BorderThickness=1;$b.FontFamily=[Windows.Media.FontFamily]::new('Microsoft YaHei UI');$b.FontSize=14;$b.Height=$tokens.component.buttonHeight;$b.Width=104;$b.Padding='16,0';$b.Template=$script:buttonTemplate;if($focus){$outer=[Windows.Controls.Border]::new();$outer.CornerRadius=14;$outer.BorderBrush=Spec-Brush $tokens.color.focus;$outer.BorderThickness=2;$outer.Padding=2;$outer.Child=$b;return $outer};return $b}
function Spec-Input([string]$text,[string]$border,[bool]$focus=$false){$box=[Windows.Controls.TextBox]::new();$box.Text=$text;$box.FontFamily=[Windows.Media.FontFamily]::new('Microsoft YaHei UI');$box.FontSize=14;$box.Background=Spec-Brush '#FCFBF8';$box.Foreground=Spec-Brush '#292824';$box.BorderBrush=Spec-Brush $border;$box.BorderThickness=1;$box.Height=40;$box.Width=304;$box.Padding='12,0';$box.VerticalContentAlignment='Center';$box.Template=$script:inputTemplate;if($focus){$outer=[Windows.Controls.Border]::new();$outer.BorderBrush=Spec-Brush $tokens.color.focus;$outer.BorderThickness=2;$outer.CornerRadius=14;$outer.Padding=2;$outer.Child=$box;return $outer};return $box}
function Spec-Toggle([string]$color,[bool]$on,[string]$caption){$row=[Windows.Controls.StackPanel]::new();$row.Orientation='Horizontal';$track=[Windows.Controls.Border]::new();$track.Width=$tokens.component.toggleWidth;$track.Height=$tokens.component.toggleHeight;$track.CornerRadius=14;$track.Background=Spec-Brush $color;$thumb=[Windows.Controls.Border]::new();$thumb.Width=$tokens.component.toggleThumb;$thumb.Height=$tokens.component.toggleThumb;$thumb.CornerRadius=11;$thumb.Background=Spec-Brush '#FFFFFF';$thumb.Margin=3;$thumb.HorizontalAlignment=if($on){'Right'}else{'Left'};$track.Child=$thumb;[void]$row.Children.Add($track);$label=Spec-Text $caption 14;$label.VerticalAlignment='Center';$label.Margin='8,0,0,0';[void]$row.Children.Add($label);return $row}
$script:buttonTemplate=[Windows.Markup.XamlReader]::Parse('<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="Button"><Border CornerRadius="10" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" Padding="{TemplateBinding Padding}"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border></ControlTemplate>')
$script:inputTemplate=[Windows.Markup.XamlReader]::Parse('<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="TextBox"><Border CornerRadius="10" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" Padding="{TemplateBinding Padding}"><ScrollViewer x:Name="PART_ContentHost" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" VerticalAlignment="Center"/></Border></ControlTemplate>')
$canvas=[Windows.Controls.Canvas]::new();$canvas.Width=1200;$canvas.Height=1240;$canvas.Background=Spec-Brush '#F3F0E9'
Spec-Label $canvas 'PAUSEWELL  /  DESIGN SYSTEM 0.3' 40 28 12 '#65615B' $true
Spec-Label $canvas '间歇 · 组件与排版规范' 40 58 28 '#292824' $true
Spec-Label $canvas '暖香槟基准 · 草案样张 · 尚未接入当前程序' 40 108 14 '#65615B'
Spec-Label $canvas '所有尺寸为 DIP；截图按 96 DPI 渲染' 800 110 12 '#746F67'
$themes=Spec-Panel 1120 140;Spec-Place $canvas $themes 40 148
Spec-Label $themes.Child '01  主题角色' 24 18 18 '#292824' $true
$index=0;foreach($property in $tokens.themes.PSObject.Properties){$t=$property.Value;$x=24+$index*220;$swatch=[Windows.Controls.Border]::new();$swatch.CornerRadius=6;$swatch.Background=Spec-Brush $t.primary;Spec-Place $themes.Child $swatch $x 58 28 28;Spec-Label $themes.Child $t.label ($x+40) 58 14 '#292824' $true;Spec-Label $themes.Child ($t.primary+' / 开关 '+$t.toggle) $x 100 11 '#65615B';Spec-Place $themes.Child (Spec-Toggle $t.toggle $true '') ($x+130) 58;$index++}
$type=Spec-Panel 360 304;Spec-Place $canvas $type 40 312
Spec-Label $type.Child '02  排版层级' 24 20 18 '#292824' $true
Spec-Label $type.Child '提醒中' 24 64 28 '#292824' $true;Spec-Label $type.Child '28 / 40 · 页面标题' 170 76 12 '#746F67'
$time=Spec-Text '09:30' 44 '#292824' $true;$time.FontFamily=[Windows.Media.FontFamily]::new('Segoe UI');Spec-Place $type.Child $time 24 108;Spec-Label $type.Child '44 / 56 · 当天时间' 170 132 12 '#746F67'
Spec-Label $type.Child '我的提醒' 24 184 18 '#292824' $true;Spec-Label $type.Child '18 / 28 · 区块标题' 170 186 12 '#746F67'
Spec-Label $type.Child '完成后再等 30 分钟' 24 228 14 '#65615B';Spec-Label $type.Child '14 / 22 · 正文' 170 230 12 '#746F67'
Spec-Label $type.Child '辅助说明不承载关键状态' 24 266 12 '#746F67'
$buttons=Spec-Panel 736 304;Spec-Place $canvas $buttons 424 312
Spec-Label $buttons.Child '03  按钮状态' 24 20 18 '#292824' $true
Spec-Label $buttons.Child '常规高 40 / 提醒主动作高 44 / 圆角 10 / 按钮间距 8' 24 54 12 '#65615B'
$states=@(@('保存','#343434','#FFFFFF','Default'),@('保存','#45433F','#FFFFFF','Hover'),@('保存','#292824','#FFFFFF','Pressed'),@('保存','#343434','#FFFFFF','Focus'),@('保存','#D4D0C7','#746F67','Disabled'))
for($i=0;$i -lt $states.Count;$i++){$s=$states[$i];Spec-Place $buttons.Child (Spec-Button $s[0] $s[1] $s[2] ($i -eq 3)) (24+$i*138) 100;Spec-Label $buttons.Child $s[3] (24+$i*138) 160 12 '#746F67'}
Spec-Place $buttons.Child (Spec-Button '取消' '#EDE9E1' '#292824') 24 212;Spec-Place $buttons.Child (Spec-Button '保存提醒' '#343434') 136 212
Spec-Label $buttons.Child '每个任务区一个主动作；焦点环不改变布局' 280 222 12 '#65615B'
$fields=Spec-Panel 736 316;Spec-Place $canvas $fields 40 640
Spec-Label $fields.Child '04  表单字段' 24 20 18 '#292824' $true
Spec-Label $fields.Child '提醒名称' 24 64 14 '#292824' $true;Spec-Place $fields.Child (Spec-Input '喝水' $tokens.color.inputBoundary) 24 92
Spec-Label $fields.Child '标签 → 8 → 输入框 40 → 6 → 说明' 24 144 12 '#65615B'
Spec-Label $fields.Child '间隔分钟（正在输入）' 384 64 14 '#292824' $true;Spec-Place $fields.Child (Spec-Input '30' $tokens.color.inputBoundary $true) 380 88
Spec-Label $fields.Child '完成确认后重新计算间隔' 384 144 12 '#65615B'
Spec-Label $fields.Child '实际时间' 24 194 14 '#292824' $true;Spec-Place $fields.Child (Spec-Input '25:70' $tokens.color.danger) 24 222
Spec-Label $fields.Child '请输入有效时刻，例如 08:30。' 24 274 12 $tokens.color.danger
Spec-Label $fields.Child '未知实际时间' 384 194 14 '#292824' $true;Spec-Place $fields.Child (Spec-Input '记不清具体时间' '#B9B3AA') 384 222
Spec-Label $fields.Child '明确记录未知，不用确认时间替代。' 384 274 12 '#65615B'
$switches=Spec-Panel 360 316;Spec-Place $canvas $switches 800 640
Spec-Label $switches.Child '05  开关与间距' 24 20 18 '#292824' $true
Spec-Place $switches.Child (Spec-Toggle '#9B8D70' $true '开启') 24 72;Spec-Place $switches.Child (Spec-Toggle '#8B8985' $false '关闭') 178 72
Spec-Label $switches.Child '轨道 48 × 28 / 滑块 22 / 命中高 36' 24 124 12 '#65615B'
Spec-Label $switches.Child '4   8   12   16   24   32   40   48' 24 184 16 '#292824' $true
Spec-Label $switches.Child '组件内 8～16，字段组与页面区块 24。' 24 224 12 '#65615B' $false 306
Spec-Label $switches.Child '不再逐页随意写边距。' 24 256 12 '#65615B'
$layers=Spec-Panel 1120 180;Spec-Place $canvas $layers 40 980
Spec-Label $layers.Child '06  层级契约' 24 20 18 '#292824' $true
$levels=@(@('L0  环境底','全窗口低对比暖色'),@('L1  内容层','稳定阅读 / E1'),@('L2  控制浮层','导航、菜单 / E2'),@('L3  任务浮层','提醒、确认 / E3'))
for($i=0;$i -lt 4;$i++){Spec-Label $layers.Child $levels[$i][0] (24+$i*274) 68 16 '#292824' $true;Spec-Label $layers.Child $levels[$i][1] (24+$i*274) 100 13 '#65615B'}
Spec-Label $layers.Child '材质需另外进行原生打样；本页只展示基础排版、尺寸和静态状态，不作为玻璃效果完成的证据。' 24 142 12 '#746F67'
Spec-Label $canvas '配套：设计系统规范 v0.3 · tokens.json · 页面与状态矩阵 · 真实界面复审' 40 1192 12 '#746F67'
$canvas.Measure([Windows.Size]::new(1200,1240));$canvas.Arrange([Windows.Rect]::new(0,0,1200,1240));$canvas.UpdateLayout()
$bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new(1200,1240,96,96,[Windows.Media.PixelFormats]::Pbgra32);$bitmap.Render($canvas);$encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new();$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap));$destination=Join-Path $specRoot 'design/system/v0.3/组件规范样张.png';$stream=[IO.File]::Create($destination);try{$encoder.Save($stream)}finally{$stream.Dispose()}
Write-Output 'Design system specimen rendered from proposed tokens.'
