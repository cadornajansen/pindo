param([switch]$Preview)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$executable = Join-Path $repositoryRoot 'src\Pindo.App\bin\Debug\net10.0-windows10.0.26100.0\Pindo.App.exe'
if (Get-Process Pindo.App -ErrorAction SilentlyContinue) {
    Write-Host 'Pindo is already running. Exit it from the tray before starting this version.'
    return
}
$env:PINDO_PREVIEW = if ($Preview) { 'true' } else { 'false' }
dotnet build (Join-Path $repositoryRoot 'Pindo.sln') -c Debug
if ($LASTEXITCODE -ne 0) { throw 'Build failed; Pindo was not started.' }
Start-Process -FilePath $executable -WindowStyle Hidden | Out-Null
Write-Host 'Pindo is running. Press and release Ctrl+Windows to open chat. Use + to select files; Enter sends; Escape cancels.'
