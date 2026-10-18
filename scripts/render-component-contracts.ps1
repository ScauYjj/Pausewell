param()
$ErrorActionPreference='Stop'
$contractRoot=Split-Path -Parent $PSScriptRoot
$helperSource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'render-design-system.ps1'),[Text.Encoding]::UTF8)
$helperEnd=$helperSource.IndexOf('$canvas=[Windows.Controls.Canvas]::new()')
if($helperEnd -lt 0){throw 'Specimen helper boundary missing.'}
. ([ScriptBlock]::Create($helperSource.Substring(0,$helperEnd).Replace('$specRoot=Split-Path -Parent $PSScriptRoot','$specRoot=$contractRoot')))
$tokens=Get-Content -LiteralPath (Join-Path $contractRoot 'design/system/v0.3.1/tokens.json') -Raw -Encoding UTF8|ConvertFrom-Json
$cc=$tokens.componentContracts
function Contract-Icon([string]$name,[double]$size=16,[string]$color='#292824'){
 [xml]$svg=[IO.File]::ReadAllText((Join-Path $contractRoot ('design/system/v0.3.1/assets/'+$name+'.svg')))
 $box=$svg.DocumentElement.GetAttribute('viewBox').Split(' ');$drawing=[Windows.Controls.Canvas]::new();$drawing.Width=[double]$box[2];$drawing.Height=[double]$box[3]
 foreach($node in $svg.SelectNodes('//*[local-name()="path"]')){$path=[Windows.Shapes.Path]::new();$path.Data=[Windows.Media.Geometry]::Parse($node.GetAttribute('d'));$path.Fill=Spec-Brush $color;[void]$drawing.Children.Add($path)}
 $view=[Windows.Controls.Viewbox]::new();$view.Width=$size;$view.Height=$size;$view.Child=$drawing;return $view
}
function Contract-Focus($content,[double]$width,[double]$height,[double]$radius=10){
 $grid=[Windows.Controls.Grid]::new();$grid.Width=$width;$grid.Height=$height;$grid.ClipToBounds=$false;[void]$grid.Children.Add($content)
 $ring=[Windows.Controls.Border]::new();$ring.Margin=-4;$ring.BorderThickness=$tokens.component.focusThickness;$ring.BorderBrush=Spec-Brush $tokens.color.focus;$ring.CornerRadius=$radius+4;$ring.IsHitTestVisible=$false;[void]$grid.Children.Add($ring);return $grid
}
function Contract-Field([string]$text,[double]$width=208,[string]$icon='', [bool]$focused=$false,[bool]$disabled=$false,[bool]$invalid=$false){
 $border=[Windows.Controls.Border]::new();$border.Width=$width;$border.Height=$cc.field.minHeight;$border.CornerRadius=$cc.field.radius;$border.BorderThickness=$cc.field.border;$border.BorderBrush=Spec-Brush $(if($invalid){$tokens.color.danger}else{$tokens.color.inputBoundary});$border.Background=Spec-Brush $(if($disabled){$tokens.color.disabledSurface}else{$tokens.color.surfaceSolid})
 $inner=[Windows.Controls.Canvas]::new();$border.Child=$inner;$label=Spec-Text $text 14 $(if($disabled){$tokens.color.textTertiary}else{$tokens.color.textPrimary});Spec-Place $inner $label 12 8 ($width-56)
 if($icon){$size=if($icon -eq 'calendar'){20}else{16};Spec-Place $inner (Contract-Icon $icon $size $(if($disabled){$tokens.color.textTertiary}else{$tokens.color.textPrimary})) ($width-30) ((40-$size)/2)}
 if($focused){return Contract-Focus $border $width 40};return $border
}
function Contract-Check([string]$label,[bool]$checked=$false){$row=[Windows.Controls.StackPanel]::new();$row.Orientation='Horizontal';$face=[Windows.Controls.Border]::new();$face.Width=18;$face.Height=18;$face.CornerRadius=4;$face.BorderThickness=1;$face.BorderBrush=Spec-Brush $tokens.color.inputBoundary;$face.Background=Spec-Brush '#FCFBF8';if($checked){$face.Background=Spec-Brush $tokens.themes.champagne.toggle;$face.Child=Contract-Icon 'check' 12 '#FFFFFF'};[void]$row.Children.Add($face);$text=Spec-Text $label 14;$text.Margin='8,0,0,0';[void]$row.Children.Add($text);return $row}
function Contract-Card([double]$width,[double]$height,[bool]$floating=$false){$card=Spec-Panel $width $height;if($floating){$card.Effect=[Windows.Media.Effects.DropShadowEffect]::new();$card.Effect.BlurRadius=20;$card.Effect.ShadowDepth=6;$card.Effect.Direction=270;$card.Effect.Opacity=.10};return $card}
function Contract-Page([string]$title,[string]$subtitle){$page=[Windows.Controls.Canvas]::new();$page.Width=1200;$page.Height=1120;$page.Background=Spec-Brush '#F3F0E9';Spec-Label $page 'PAUSEWELL / COMPONENT CONTRACT 0.3.1' 40 28 12 '#65615B' $true;Spec-Label $page $title 40 58 28 '#292824' $true;Spec-Label $page $subtitle 40 108 14 '#65615B';Spec-Label $page '静态样张 · 部分状态示意 · 尚未接入程序；材质与原生交互仍待验证。' 40 1080 12 '#746F67';return $page}
function Contract-Save($page,[string]$filename){$page.Measure([Windows.Size]::new(1200,1120));$page.Arrange([Windows.Rect]::new(0,0,1200,1120));$page.UpdateLayout();$bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new(1200,1120,96,96,[Windows.Media.PixelFormats]::Pbgra32);$bitmap.Render($page);$encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new();$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap));$stream=[IO.File]::Create((Join-Path $contractRoot ('design/system/v0.3.1/'+$filename+'.png')));try{$encoder.Save($stream)}finally{$stream.Dispose()}}
function Contract-Calendar {
 $outer=Contract-Card $cc.calendar.width $cc.calendar.baseHeight $true;$outer.CornerRadius=16;$p=$outer.Child
 Spec-Place $p (Contract-Icon 'chevron-left' 16) 20 24;Spec-Place $p (Contract-Icon 'chevron-right' 16) 268 24;Spec-Label $p '2026 年 10 月' 94 22 14 '#292824' $true
 $week=@('一','二','三','四','五','六','日');for($column=0;$column -lt 7;$column++){Spec-Label $p $week[$column] (24+$column*40) 62 12 '#65615B'}
 $month=[DateTime]::new(2026,10,1);$offset=(([int]$month.DayOfWeek)+6)%7;$start=$month.AddDays(-$offset)
 for($i=0;$i -lt 42;$i++){$date=$start.AddDays($i);$col=$i%7;$row=[Math]::Floor($i/7);$face=[Windows.Controls.Border]::new();$face.Width=32;$face.Height=32;$face.CornerRadius=10;$face.BorderThickness=0;$dayCanvas=[Windows.Controls.Canvas]::new();$face.Child=$dayCanvas;$ink=if($date.Month -eq 10){'#292824'}else{'#65615B'}
  if($date.Month -eq 10 -and $date.Day -eq 16){$face.Background=Spec-Brush $tokens.themes.champagne.soft;$face.BorderBrush=Spec-Brush $tokens.themes.champagne.toggle;$face.BorderThickness=1}
  if($date.Month -eq 10 -and $date.Day -eq 21){$face.Background=Spec-Brush $tokens.color.fieldHover}
  $number=Spec-Text ([string]$date.Day) 14 $ink;$number.Width=32;$number.TextAlignment='Center';Spec-Place $dayCanvas $number 0 5
  if($date.Month -eq 10 -and $date.Day -eq 16){$marker=[Windows.Controls.Border]::new();$marker.Background=Spec-Brush $tokens.themes.champagne.toggle;$marker.CornerRadius=1;Spec-Place $dayCanvas $marker 11 27 10 2;$focusedFace=Contract-Focus $face 32 32 10;Spec-Place $p $focusedFace (16+$col*40) (88+$row*40)}else{Spec-Place $p $face (16+$col*40) (88+$row*40)}
 }
 $today=Spec-Button '今天' '#EDE9E1' '#292824';$today.Width=64;$today.Height=32;Spec-Place $p $today 12 336;Spec-Label $p '选择日期后返回字段' 100 344 12 '#65615B';return $outer
}
$page=Contract-Page '下拉与日期 · 配套样张' '统一字段与展开面；下拉项、日历、日期范围沿用同一套尺寸、字体和状态。'
$select=Contract-Card 520 584;Spec-Place $page $select 40 156;Spec-Label $select.Child '01  下拉框与选项浮层' 24 20 18 '#292824' $true
Spec-Label $select.Child '提醒方式' 24 70 14 '#292824' $true;Spec-Place $select.Child (Contract-Field '醒目弹窗' 456 'chevron-down') 24 100
$menu=Contract-Card 456 128 $true;$menu.CornerRadius=12;Spec-Place $select.Child $menu 24 146
$choices=@('轻提示','醒目弹窗','全屏提醒');for($i=0;$i -lt 3;$i++){$row=[Windows.Controls.Border]::new();$row.CornerRadius=8;$row.Background=Spec-Brush $(if($i -eq 1){$tokens.themes.champagne.soft}elseif($i -eq 2){$tokens.color.fieldHover}else{'#FCFBF8'});$rowCanvas=[Windows.Controls.Canvas]::new();$row.Child=$rowCanvas;Spec-Label $rowCanvas $choices[$i] 40 9 14 '#292824';if($i -eq 1){Spec-Place $rowCanvas (Contract-Icon 'check' 16) 12 12};Spec-Place $menu.Child $row 4 (4+$i*40) 448 40}
Spec-Label $select.Child '已选：浅主题底 + 勾选；悬停项保留独立状态。' 24 306 14 '#65615B' $false 464
Spec-Label $select.Child '字段到浮层 6 / 选项高 40 / 圆角 12 / E2' 24 344 14 '#65615B'
Spec-Label $select.Child '禁用' 24 402 14 '#292824' $true;Spec-Label $select.Child '键盘到达（未展开）' 264 402 14 '#292824' $true
Spec-Place $select.Child (Contract-Field '醒目弹窗' 216 'chevron-down' $false $true) 24 434;Spec-Place $select.Child (Contract-Field '醒目弹窗' 216 'chevron-down' $true) 264 434
Spec-Label $select.Child 'Esc 关闭当前弹层；Enter 不应连带提交外层表单。' 24 514 14 '#65615B' $false 464
$date=Contract-Card 576 584;Spec-Place $page $date 584 156;Spec-Label $date.Child '02  日期输入与日历' 24 20 18 '#292824' $true
Spec-Label $date.Child '开始日期' 24 66 14 '#292824' $true;Spec-Place $date.Child (Contract-Check '设置结束日期') 304 66
Spec-Place $date.Child (Contract-Field '2026-10-16' 208 'calendar') 24 98;Spec-Place $date.Child (Contract-Field '不设结束日期' 208 'calendar' $false $true) 304 98
Spec-Place $date.Child (Contract-Calendar) 24 144
Spec-Label $date.Child '日期状态' 360 164 14 '#292824' $true
Spec-Label $date.Child "16：选中 + 今天`n并叠加键盘焦点" 360 208 14 '#65615B' $false 180
Spec-Label $date.Child '21：悬停示意' 360 286 14 '#65615B'
Spec-Label $date.Child "今天标记、选择面`n和焦点环分别表达。" 360 338 14 '#65615B' $false 180
Spec-Label $date.Child "月 / 年 / 年代视图`n都需设计配套样式。" 360 418 14 '#65615B' $false 180
Spec-Label $date.Child '304 × 384 / 日期占位 40 / 日期视觉面 32' 24 548 12 '#746F67'
$range=Contract-Card 1120 280;Spec-Place $page $range 40 764;Spec-Label $range.Child '03  日期范围 · 共同基线与组级校验' 24 20 18 '#292824' $true
Spec-Label $range.Child '记录范围' 24 66 14 '#292824' $true;Spec-Place $range.Child (Contract-Field '2026-10-10' 208 'calendar') 24 96;Spec-Label $range.Child '至' 248 105 14;Spec-Place $range.Child (Contract-Field '2026-10-16' 208 'calendar') 280 96
Spec-Label $range.Child '字段同高；“至”在中线对齐。窄屏时整组换行。' 552 104 14 '#65615B' $false 520
Spec-Place $range.Child (Contract-Field '2026-10-20' 208 'calendar' $false $false $true) 24 172;Spec-Label $range.Child '至' 248 181 14;Spec-Place $range.Child (Contract-Field '2026-10-16' 208 'calendar' $true $false $true) 280 172
Spec-Label $range.Child '结束日期不能早于开始日期。' 24 228 12 $tokens.color.danger
Spec-Label $range.Child "错误 + 焦点：保留错误边界和说明。`n不偷偷改写用户选择的日期。" 552 176 14 '#65615B' $false 520
Contract-Save $page '选择与日期控件样张'
function Contract-Nav([int]$selected,[bool]$keyboard){$shell=[Windows.Controls.Border]::new();$shell.Width=464;$shell.Height=56;$shell.CornerRadius=28;$shell.Background=Spec-Brush '#EDE8DE';$canvas=[Windows.Controls.Canvas]::new();$shell.Child=$canvas;$names=@('提醒','服药','记录','设置');$icons=@('reminder','medicine','history','settings');for($i=0;$i -lt 4;$i++){$item=[Windows.Controls.Border]::new();$item.Width=110;$item.Height=48;$item.CornerRadius=24;if($i -eq $selected){$item.Background=Spec-Brush '#FCFBF8'};$body=[Windows.Controls.Canvas]::new();$item.Child=$body;Spec-Place $body (Contract-Icon $icons[$i] 20) 16 14;Spec-Label $body $names[$i] 44 14 14 '#292824' ($i -eq $selected);if($keyboard -and $i -eq $selected){Spec-Place $canvas (Contract-Focus $item 110 48 24) (4+$i*114) 4}else{Spec-Place $canvas $item (4+$i*114) 4}};return $shell}
$page=Contract-Page '输入、焦点与滚动 · 配套样张' '普通输入、复选、展开、导航和记录容器统一；键盘可见焦点保留。'
$navigation=Contract-Card 1120 216;Spec-Place $page $navigation 40 148;Spec-Label $navigation.Child '01  导航选中与键盘焦点分开' 24 20 18 '#292824' $true
Spec-Label $navigation.Child '鼠标选中：保留选中面' 24 66 14 '#65615B';Spec-Label $navigation.Child '键盘到达：选中面 + 可见焦点环' 584 66 14 '#65615B'
Spec-Place $navigation.Child (Contract-Nav 0 $false) 24 106;Spec-Place $navigation.Child (Contract-Nav 2 $true) 584 106
Spec-Label $navigation.Child '不保留黑色虚线，也不删除键盘焦点；焦点环不占布局空间。' 24 180 12 '#746F67'
$input=Contract-Card 736 336;Spec-Place $page $input 40 388;Spec-Label $input.Child '02  输入、备注与组合控件' 24 20 18 '#292824' $true
Spec-Label $input.Child '提醒名称' 24 66 14 '#292824' $true;Spec-Label $input.Child '实际时间（错误 + 焦点）' 384 66 14 '#292824' $true
Spec-Place $input.Child (Contract-Field '喝水' 304) 24 96;Spec-Place $input.Child (Contract-Field '25:70' 304 '' $true $false $true) 384 96
Spec-Label $input.Child '正文 14 / 最小高 40 / 圆角 10' 24 148 12 '#65615B';Spec-Label $input.Child '请输入有效时刻，例如 08:30。' 384 148 12 $tokens.color.danger
Spec-Label $input.Child '备注（可选）' 24 192 14 '#292824' $true
$area=[Windows.Controls.Border]::new();$area.CornerRadius=10;$area.BorderBrush=Spec-Brush $tokens.color.inputBoundary;$area.BorderThickness=1;$area.Background=Spec-Brush '#FCFBF8';$area.Padding=12;$area.Child=Spec-Text '需要补充时再填写。' 14 '#65615B';Spec-Place $input.Child $area 24 222 440 80
Spec-Place $input.Child (Contract-Check '启用此提醒' $true) 512 200;Spec-Place $input.Child (Contract-Icon 'chevron-down' 16) 512 254;Spec-Label $input.Child '更多设置' 536 252 14 '#292824';Spec-Label $input.Child '整行高 40，可点击展开' 512 292 12 '#746F67'
$scroll=Contract-Card 360 336;Spec-Place $page $scroll 800 388;Spec-Label $scroll.Child '03  细滚动条' 24 20 18 '#292824' $true
$viewport=[Windows.Controls.Border]::new();$viewport.CornerRadius=10;$viewport.BorderBrush=Spec-Brush '#D9D5CD';$viewport.BorderThickness=1;$viewport.Background=Spec-Brush '#F7F4EE';$viewportContent=[Windows.Controls.Canvas]::new();$viewport.ClipToBounds=$true;$viewport.Child=$viewportContent;Spec-Place $scroll.Child $viewport 24 60 312 208
for($i=0;$i -lt 8;$i++){Spec-Label $viewportContent ('示例内容 '+($i+1)) 16 (16+$i*40) 14 '#65615B'}
$rail=[Windows.Controls.Border]::new();$rail.Background=Spec-Brush $tokens.color.scrollRailHover;$rail.CornerRadius=7;Spec-Place $viewportContent $rail 294 8 14 192;$thumb=[Windows.Controls.Border]::new();$thumb.Background=Spec-Brush $tokens.color.scrollThumb;$thumb.CornerRadius=4;Spec-Place $viewportContent $thumb 297 36 8 64
Spec-Label $scroll.Child '静止宽 6 / 悬停宽 8 / 命中宽 14' 24 286 12 '#65615B';Spec-Label $scroll.Child '无上下箭头；有溢出才出现，仍支持拖动。' 24 310 12 '#746F67'
$records=Contract-Card 1120 320;Spec-Place $page $records 40 748;Spec-Label $records.Child '04  记录列表 · 一组一层外壳，内部行分隔' 24 20 18 '#292824' $true
$list=Contract-Card 1072 176;Spec-Place $records.Child $list 24 68
$recordTitles=@('喝水 · 已确认完成','起来活动 · 已取消');$recordDetails=@('原计划 10-16 09:30 · 实际 10-16 09:32 · 确认 10-16 09:32','原计划 10-16 10:15 · 确认 10-16 10:20')
for($i=0;$i -lt 2;$i++){Spec-Label $list.Child $recordTitles[$i] 16 (12+$i*88) 16 '#292824' $true;Spec-Label $list.Child $recordDetails[$i] 16 (44+$i*88) 14 '#65615B';$button=Spec-Button '查看详情' '#EDE9E1' '#292824';$button.Width=112;$button.Height=32;Spec-Place $list.Child $button 944 (28+$i*88)}
$divider=[Windows.Controls.Border]::new();$divider.Background=Spec-Brush $tokens.color.divider;Spec-Place $list.Child $divider 16 88 1040 1
Spec-Label $records.Child '需要独立卡片时，12 的外间距放在最外层容器；不再同时画卡片边界与行分隔线。' 24 274 12 '#746F67'
Contract-Save $page '输入与焦点及滚动样张'
Write-Output 'Two static component contract specimens rendered. Interactive validation is pending.'
