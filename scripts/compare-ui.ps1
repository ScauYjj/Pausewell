param([string]$CaptureDir='artifacts/ui-v02/release-gui')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationCore,PresentationFramework,WindowsBase
$comparisonRoot=Split-Path -Parent $PSScriptRoot
function Read-UiBitmap([string]$relative) {
 $stream=[IO.File]::OpenRead((Join-Path $comparisonRoot $relative))
 try { $bitmap=[System.Windows.Media.Imaging.BitmapImage]::new();$bitmap.BeginInit();$bitmap.CacheOption=[System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad;$bitmap.StreamSource=$stream;$bitmap.EndInit();$bitmap.Freeze();return $bitmap } finally { $stream.Dispose() }
}
function Write-UiComparison([string]$name,[object]$left,[object]$right) {
 $leftHeight=1004.0*$left.PixelHeight/$left.PixelWidth;$rightHeight=1004.0*$right.PixelHeight/$right.PixelWidth;$canvasHeight=[int][Math]::Ceiling([Math]::Max($leftHeight,$rightHeight)+24)
 $visual=[System.Windows.Media.DrawingVisual]::new();$context=$visual.RenderOpen();$context.DrawRectangle([System.Windows.Media.Brushes]::White,$null,[System.Windows.Rect]::new(0,0,2056,$canvasHeight));$context.DrawImage($left,[System.Windows.Rect]::new(12,12,1004,$leftHeight));$context.DrawImage($right,[System.Windows.Rect]::new(1040,12,1004,$rightHeight));$context.Close()
 $target=[System.Windows.Media.Imaging.RenderTargetBitmap]::new(2056,$canvasHeight,96,96,[System.Windows.Media.PixelFormats]::Pbgra32);$target.Render($visual);$encoder=[System.Windows.Media.Imaging.PngBitmapEncoder]::new();$encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($target));$stream=[IO.File]::Create((Join-Path $comparisonRoot ('artifacts/ui-v02/'+$name+'.png')));try { $encoder.Save($stream) } finally { $stream.Dispose() }
}
$source=Read-UiBitmap 'design/ui/轻透主题/01-暖香槟-默认.png'
$actual=Read-UiBitmap ($CaptureDir+'/04-home.png')
$sourceClient=[System.Windows.Media.Imaging.CroppedBitmap]::new($source,[System.Windows.Int32Rect]::new(52,122,1004,820))
$actualClient=[System.Windows.Media.Imaging.CroppedBitmap]::new($actual,[System.Windows.Int32Rect]::new(0,0,988,820))
Write-UiComparison 'comparison-main' $sourceClient $actualClient
$sourceFocus=[System.Windows.Media.Imaging.CroppedBitmap]::new($source,[System.Windows.Int32Rect]::new(88,516,940,362))
$actualFocus=[System.Windows.Media.Imaging.CroppedBitmap]::new($actual,[System.Windows.Int32Rect]::new(40,390,916,380))
Write-UiComparison 'comparison-controls' $sourceFocus $actualFocus
'Comparison sheets saved: original left, implementation right, aspect ratios preserved.'
