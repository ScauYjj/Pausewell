param()
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationCore,PresentationFramework,WindowsBase
$pageRoot=Split-Path -Parent $PSScriptRoot
$visual=[Windows.Media.DrawingVisual]::new();$dc=$visual.RenderOpen();$dc.DrawRectangle([Windows.Media.Brushes]::White,$null,[Windows.Rect]::new(0,0,1920,1560))
$entries=@(@('83-history','History'),@('74-record-detail','Record detail'),@('75-record-save-failure','Correction and retry'),@('82-general-settings','General settings'),@('79-appearance','Appearance'),@('80-appearance-compact','Compact appearance'))
for($i=0;$i -lt $entries.Count;$i++){
 $entry=$entries[$i];$x=24+($i%3)*632;$y=20+[Math]::Floor($i/3)*770
 $label=[Windows.Media.FormattedText]::new($entry[1],[Globalization.CultureInfo]::InvariantCulture,[Windows.FlowDirection]::LeftToRight,[Windows.Media.Typeface]::new('Segoe UI'),18,[Windows.Media.Brushes]::Black,1.0);$dc.DrawText($label,[Windows.Point]::new($x,$y))
 $stream=[IO.File]::OpenRead((Join-Path $pageRoot ('artifacts/v038/release-gui/'+$entry[0]+'.png')))
 try{$bitmap=[Windows.Media.Imaging.BitmapImage]::new();$bitmap.BeginInit();$bitmap.CacheOption='OnLoad';$bitmap.StreamSource=$stream;$bitmap.EndInit();$bitmap.Freeze()}finally{$stream.Dispose()}
 $fit=[Math]::Min(600.0/$bitmap.PixelWidth,700.0/$bitmap.PixelHeight);$dc.DrawImage($bitmap,[Windows.Rect]::new($x,$y+36,$bitmap.PixelWidth*$fit,$bitmap.PixelHeight*$fit))
}
$dc.Close();$output=[Windows.Media.Imaging.RenderTargetBitmap]::new(1920,1560,96,96,[Windows.Media.PixelFormats]::Pbgra32);$output.Render($visual);$encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new();$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($output));$stream=[IO.File]::Create((Join-Path $pageRoot 'artifacts/v038/01-records-settings.png'));try{$encoder.Save($stream)}finally{$stream.Dispose()}
Write-Output 'Rendered record and settings overview from native captures.'
