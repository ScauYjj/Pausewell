param()
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationCore,PresentationFramework,WindowsBase
$componentRoot=Split-Path -Parent $PSScriptRoot
function Read-LabImage([string]$relative){$stream=[IO.File]::OpenRead((Join-Path $componentRoot $relative));try{$bitmap=[Windows.Media.Imaging.BitmapImage]::new();$bitmap.BeginInit();$bitmap.CacheOption='OnLoad';$bitmap.StreamSource=$stream;$bitmap.EndInit();$bitmap.Freeze();return $bitmap}finally{$stream.Dispose()}}
function Draw-LabLabel($context,[string]$text,[double]$x,[double]$y){$label=[Windows.Media.FormattedText]::new($text,[Globalization.CultureInfo]::GetCultureInfo('zh-CN'),[Windows.FlowDirection]::LeftToRight,[Windows.Media.Typeface]::new('Microsoft YaHei UI'),14,[Windows.Media.Brushes]::Black,1.0);$context.DrawText($label,[Windows.Point]::new($x,$y))}
$source=Read-LabImage 'design/system/v0.3.1/选择与日期控件样张.png'
$source=[Windows.Media.Imaging.CroppedBitmap]::new($source,[Windows.Int32Rect]::new(609,301,304,384))
$actual=Read-LabImage 'artifacts/component-lab/release-verification/09-calendar-month.png'
$visual=[Windows.Media.DrawingVisual]::new();$context=$visual.RenderOpen();$context.DrawRectangle([Windows.Media.Brushes]::White,$null,[Windows.Rect]::new(0,0,720,500))
Draw-LabLabel $context '规范样张 / 304 × 384 DIP' 24 20
Draw-LabLabel $context '原生窗口 / 150% 归一化' 384 20
$context.DrawImage($source,[Windows.Rect]::new(24,60,304,384))
$context.DrawImage($actual,[Windows.Rect]::new(384,60,304,384))
Draw-LabLabel $context '比较今天 + 选中 + 键盘焦点。左图另含 21 日悬停和提示文案。' 24 462
$context.Close();$render=[Windows.Media.Imaging.RenderTargetBitmap]::new(720,500,96,96,[Windows.Media.PixelFormats]::Pbgra32);$render.Render($visual);$encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new();$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($render));$output=[IO.File]::Create((Join-Path $componentRoot 'artifacts/component-lab/calendar-comparison.png'));try{$encoder.Save($output)}finally{$output.Dispose()}
Write-Output 'Calendar comparison rendered; popup shadow is excluded from capture.'
