param()
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationCore,PresentationFramework,WindowsBase
$taskRoot=Split-Path -Parent $PSScriptRoot
$visual=[Windows.Media.DrawingVisual]::new();$dc=$visual.RenderOpen();$dc.DrawRectangle([Windows.Media.Brushes]::White,$null,[Windows.Rect]::new(0,0,1920,1560))
$entries=@(@('08-meeting-config','Meeting'),@('10-pause-config','Pause'),@('14-water-reminder','Reminder'),@('13-medicine-confirm','Medication confirmation'),@('60-queue','Follow-up queue'),@('15-fullscreen','Fullscreen reminder'))
for($i=0;$i -lt $entries.Count;$i++){
 $entry=$entries[$i];$x=24+($i%3)*632;$y=20+[Math]::Floor($i/3)*770
 $label=[Windows.Media.FormattedText]::new($entry[1],[Globalization.CultureInfo]::InvariantCulture,[Windows.FlowDirection]::LeftToRight,[Windows.Media.Typeface]::new('Segoe UI'),18,[Windows.Media.Brushes]::Black,1.0);$dc.DrawText($label,[Windows.Point]::new($x,$y))
 $stream=[IO.File]::OpenRead((Join-Path $taskRoot ('artifacts/v037/release-gui/'+$entry[0]+'.png')))
 try{$bitmap=[Windows.Media.Imaging.BitmapImage]::new();$bitmap.BeginInit();$bitmap.CacheOption='OnLoad';$bitmap.StreamSource=$stream;$bitmap.EndInit();$bitmap.Freeze()}finally{$stream.Dispose()}
 $fit=[Math]::Min(600.0/$bitmap.PixelWidth,700.0/$bitmap.PixelHeight);$dc.DrawImage($bitmap,[Windows.Rect]::new($x,$y+36,$bitmap.PixelWidth*$fit,$bitmap.PixelHeight*$fit))
}
$dc.Close();$output=[Windows.Media.Imaging.RenderTargetBitmap]::new(1920,1560,96,96,[Windows.Media.PixelFormats]::Pbgra32);$output.Render($visual);$encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new();$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($output));$stream=[IO.File]::Create((Join-Path $taskRoot 'artifacts/v037/01-task-windows.png'));try{$encoder.Save($stream)}finally{$stream.Dispose()}
Write-Output 'Rendered task-window overview from native captures.'
