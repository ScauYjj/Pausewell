param()
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationCore,PresentationFramework,WindowsBase
$integrationRoot=Split-Path -Parent $PSScriptRoot
$visual=[Windows.Media.DrawingVisual]::new();$dc=$visual.RenderOpen()
$dc.DrawRectangle([Windows.Media.Brushes]::White,$null,[Windows.Rect]::new(0,0,1960,540))
$x=24
foreach($entry in @(@('30-theme-home-champagne','Champagne'),@('30-theme-home-violet','Violet'),@('30-theme-home-blue','Blue'))){
 $stream=[IO.File]::OpenRead((Join-Path $integrationRoot ('artifacts/v035/release-gui/'+$entry[0]+'.png')))
 try{$bitmap=[Windows.Media.Imaging.BitmapImage]::new();$bitmap.BeginInit();$bitmap.CacheOption='OnLoad';$bitmap.StreamSource=$stream;$bitmap.EndInit();$bitmap.Freeze()}finally{$stream.Dispose()}
 $label=[Windows.Media.FormattedText]::new($entry[1],[Globalization.CultureInfo]::InvariantCulture,[Windows.FlowDirection]::LeftToRight,[Windows.Media.Typeface]::new('Segoe UI'),18,[Windows.Media.Brushes]::Black,1.0);$dc.DrawText($label,[Windows.Point]::new($x,16))
 $fit=[Math]::Min(620.0/$bitmap.PixelWidth,440.0/$bitmap.PixelHeight)
 $dc.DrawImage($bitmap,[Windows.Rect]::new($x,52,$bitmap.PixelWidth*$fit,$bitmap.PixelHeight*$fit));$x+=644
}
$dc.Close();$output=[Windows.Media.Imaging.RenderTargetBitmap]::new(1960,540,96,96,[Windows.Media.PixelFormats]::Pbgra32);$output.Render($visual)
$encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new();$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($output));$stream=[IO.File]::Create((Join-Path $integrationRoot 'artifacts/v035/01-theme-overview.png'));try{$encoder.Save($stream)}finally{$stream.Dispose()}
Write-Output 'Rendered integrated page overview with preserved aspect ratios.'
