param([switch]$Install)
$ErrorActionPreference = 'Stop'
if ($Install) {
    winget install --id ImageMagick.ImageMagick --exact --silent --accept-package-agreements --accept-source-agreements --disable-interactivity
    winget install --id oschwartz10612.Poppler --exact --silent --accept-package-agreements --accept-source-agreements --disable-interactivity
    winget install --id yt-dlp.yt-dlp --version 2026.08.19 --exact --silent --accept-package-agreements --accept-source-agreements --disable-interactivity
    winget install --id Gyan.FFmpeg --version 9.0.1 --exact --silent --accept-package-agreements --accept-source-agreements --disable-interactivity
}
$packageRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages'
function Find-PackageFile([string]$packagePattern, [string]$filename) {
    Get-ChildItem -LiteralPath $packageRoot -Directory -Filter $packagePattern -ErrorAction SilentlyContinue |
        ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Filter $filename -File -Recurse } |
        Select-Object -First 1 -ExpandProperty FullName
}
$magick = Get-ChildItem -LiteralPath $env:ProgramFiles -Directory -Filter 'ImageMagick*' |
    ForEach-Object { Join-Path $_.FullName 'magick.exe' } | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
$pdfInfo = Find-PackageFile 'oschwartz10612.Poppler_*' 'pdfinfo.exe'
$poppler = if ($pdfInfo) { Split-Path -Parent $pdfInfo } else { $null }
$ytdlp = Find-PackageFile 'yt-dlp.yt-dlp_*' 'yt-dlp.exe'
if (-not $ytdlp) { $ytdlp = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Links\yt-dlp.exe' }
$ffmpeg = Find-PackageFile 'Gyan.FFmpeg_*' 'ffmpeg.exe'
# Respect explicit Pindo paths before using auto-discovered installations.
function Read-ConfiguredPath([string]$name) {
    $configured = [Environment]::GetEnvironmentVariable($name, 'Process')
    if ([string]::IsNullOrWhiteSpace($configured)) { $configured = [Environment]::GetEnvironmentVariable($name, 'User') }
    return $configured
}
if ($configured = Read-ConfiguredPath 'PINDO_IMAGEMAGICK') { $magick = $configured }
if ($configured = Read-ConfiguredPath 'PINDO_POPPLER') { $poppler = $configured; $pdfInfo = Join-Path $poppler 'pdfinfo.exe' }
if ($configured = Read-ConfiguredPath 'PINDO_YTDLP') { $ytdlp = $configured }
if ($configured = Read-ConfiguredPath 'PINDO_FFMPEG') { $ffmpeg = $configured }
foreach ($entry in @{ ImageMagick=$magick; Poppler=$pdfInfo; 'yt-dlp'=$ytdlp; FFmpeg=$ffmpeg }.GetEnumerator()) {
    if (-not $entry.Value -or -not (Test-Path -LiteralPath $entry.Value -PathType Leaf)) { throw "Missing $($entry.Key). Run this script with -Install." }
}
foreach ($name in @('pdfinfo','pdfunite','pdfseparate','pdfimages','pdftotext','pdftoppm')) {
    if (-not (Test-Path -LiteralPath (Join-Path $poppler "$name.exe"))) { throw "Poppler is missing $name.exe" }
}
if ((& $ytdlp --version) -ne '2026.08.19') { throw 'yt-dlp must be version 2026.08.19.' }
$ffmpegVersion = (& $ffmpeg -version | Select-Object -First 1)
if ($ffmpegVersion -notmatch '^ffmpeg version 9\.0\.1(?:-| )') { throw 'FFmpeg must be version 9.0.1.' }
$settings = @{ PINDO_IMAGEMAGICK=$magick; PINDO_POPPLER=$poppler; POPPLER_BIN_DIR=$poppler; PINDO_YTDLP=$ytdlp; PINDO_FFMPEG=$ffmpeg }
foreach ($entry in $settings.GetEnumerator()) {
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($entry.Key, 'User'))) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'User')
    }
    [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    Write-Host "$($entry.Key): $($entry.Value)"
}
& $magick -version | Select-Object -First 1
& $pdfInfo -v
Write-Host $ffmpegVersion
Write-Host 'Tool dependencies configured. Restart Pindo to load them.'
