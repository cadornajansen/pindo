param([switch]$Preview)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$executable = Join-Path $repositoryRoot 'Pointly.App\bin\Debug\net10.0-windows10.0.26100.0\Pointly.App.exe'
$running = Get-Process Pointly.App -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $executable }
if ($running) {
    Write-Host 'Pindo is already running. Use Ctrl+Space, or Exit Pindo from its tray menu before rebuilding.'
    return
}

# A previous terminal preview must not silently disable real questions.
Remove-Item Env:POINTLY_DEBUG_PREVIEW, Env:POINTLY_DEBUG_WALKTHROUGH,
    Env:POINTLY_DEBUG_FORCE_VISION, Env:POINTLY_DEBUG_GROUNDING_QUERY -ErrorAction SilentlyContinue
if ($Preview) { $env:POINTLY_DEBUG_PREVIEW = 'true' }
$env:POINTLY_GROUNDING_PROVIDER = 'openrouter'
$env:POINTLY_VOICE_MODE = 'false'
foreach ($name in @('ASSEMBLYAI_API_KEY', 'OPENROUTER_API_KEY', 'ELEVENLABS_API_KEY',
    'LOCAL_TUTOR_IMAGEMAGICK', 'LOCAL_TUTOR_POPPLER', 'LOCAL_TUTOR_YTDLP', 'LOCAL_TUTOR_FFMPEG')) {
    $savedKey = [Environment]::GetEnvironmentVariable($name, 'User')
    if (-not [string]::IsNullOrWhiteSpace($savedKey)) {
        [Environment]::SetEnvironmentVariable($name, $savedKey, 'Process')
    }
}
$savedKey = $null

dotnet build (Join-Path $repositoryRoot 'Pointly.sln') -c Debug
if ($LASTEXITCODE -ne 0) { throw 'Build failed; Pindo was not started.' }
Start-Process -FilePath $executable -WindowStyle Hidden | Out-Null
Write-Host 'Pindo is running. Focus your target app and press Ctrl+Space. Enter sends; the microphone button starts voice.'
