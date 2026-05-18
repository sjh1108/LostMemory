$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$running = Get-CimInstance Win32_Process -Filter "Name='python.exe'" |
    Where-Object { $_.CommandLine -and $_.CommandLine -match 'main\.py.+--port\s+8188' }
if ($running) {
    Write-Host ("ComfyUI already running (PID {0}); skip" -f $running.ProcessId)
    exit 0
}

$cmd = '".venv_local\Scripts\python.exe" main.py --listen 0.0.0.0 --port 8188 --disable-auto-launch --preview-method auto 1>>run\comfyui-stdout.log 2>>run\comfyui-stderr.log'
$proc = Start-Process -FilePath 'cmd.exe' -ArgumentList '/c', $cmd `
    -WorkingDirectory $root -WindowStyle Hidden -PassThru
Write-Host ("Started ComfyUI (host cmd PID {0}) -> http://127.0.0.1:8188 (bind 0.0.0.0:8188)" -f $proc.Id)
