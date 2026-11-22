# Stops the lab backends started by start-lab.ps1 (only the processes it recorded).
$file = Join-Path (Join-Path $PSScriptRoot 'data') 'lab.pids'
if (Test-Path $file) {
    foreach ($id in Get-Content $file) {
        $process = Get-Process -Id $id -ErrorAction SilentlyContinue
        if ($process -and $process.ProcessName -eq 'dotnet') {
            Stop-Process -Id $id -Force
            Write-Host "Stopped pid $id"
        }
    }
    Remove-Item $file
}
