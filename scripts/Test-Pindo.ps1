param([switch]$LiveCloud)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
foreach ($name in @('PINDO_IMAGEMAGICK','PINDO_POPPLER','PINDO_YTDLP','PINDO_FFMPEG')) {
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name, 'Process'))) {
        [Environment]::SetEnvironmentVariable($name, [Environment]::GetEnvironmentVariable($name, 'User'), 'Process')
    }
}
if ($env:PINDO_POPPLER) { $env:POPPLER_BIN_DIR = $env:PINDO_POPPLER }
$env:PINDO_INPUT_TEST = '0' # Native input validation runs separately.
$env:PINDO_LIVE_TEST = if ($LiveCloud) { '1' } else { '0' }
dotnet test (Join-Path $repositoryRoot 'Pindo.sln') -c Debug --verbosity minimal
exit $LASTEXITCODE
