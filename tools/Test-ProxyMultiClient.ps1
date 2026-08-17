# Requires Windows PowerShell 5.1
# Two-process multi-client integration test for ASCOM.SnowFlakeProxy.FilterWheel

$ErrorActionPreference = "Stop"
$prog_id = "ASCOM.SnowFlakeProxy.FilterWheel"
$root = Split-Path -Parent $PSScriptRoot
$log_dir = Join-Path $env:TEMP "SnowFlakeProxyMultiClient"
New-Item -ItemType Directory -Path $log_dir -Force | Out-Null
$client_a_log = Join-Path $log_dir "client-a.log"
$client_b_log = Join-Path $log_dir "client-b.log"
Remove-Item $client_a_log -ErrorAction SilentlyContinue
Remove-Item $client_b_log -ErrorAction SilentlyContinue

$client_a = @'
$ErrorActionPreference = "Stop"
$prog_id = "ASCOM.SnowFlakeProxy.FilterWheel"
$fw = New-Object -ComObject $prog_id
try {
    $fw.Connected = $true
    $end = [DateTime]::UtcNow.AddSeconds(45)
    while ([DateTime]::UtcNow -lt $end) {
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $p = $fw.Position
        $sw.Stop()
        Write-Output ("{0:HH:mm:ss.fff} Position={1} getter={2}ms" -f [DateTime]::Now, $p, $sw.ElapsedMilliseconds)
        Start-Sleep -Milliseconds 100
    }
}
finally {
    try { $fw.Connected = $false } catch { }
    if ([Runtime.InteropServices.Marshal]::IsComObject($fw)) {
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($fw)
    }
}
'@

$client_b = @'
$ErrorActionPreference = "Stop"
$prog_id = "ASCOM.SnowFlakeProxy.FilterWheel"
Start-Sleep -Seconds 2
$fw = New-Object -ComObject $prog_id
try {
    $fw.Connected = $true
    $start = $fw.Position
    $target = $start + 1
    if ($target -ge @($fw.Names).Length) { $target = 0 }
    Write-Output ("{0:HH:mm:ss.fff} command Position={1} from {2}" -f [DateTime]::Now, $target, $start)
    $set_sw = [Diagnostics.Stopwatch]::StartNew()
    $fw.Position = $target
    $set_sw.Stop()
    Write-Output ("{0:HH:mm:ss.fff} setter returned after {1} ms" -f [DateTime]::Now, $set_sw.ElapsedMilliseconds)
    $end = [DateTime]::UtcNow.AddSeconds(40)
    while ([DateTime]::UtcNow -lt $end) {
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $p = $fw.Position
        $sw.Stop()
        Write-Output ("{0:HH:mm:ss.fff} Position={1} getter={2}ms" -f [DateTime]::Now, $p, $sw.ElapsedMilliseconds)
        if ($p -eq $target) { break }
        Start-Sleep -Milliseconds 100
    }
}
finally {
    try { $fw.Connected = $false } catch { }
    if ([Runtime.InteropServices.Marshal]::IsComObject($fw)) {
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($fw)
    }
}
'@

$ps = "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"
$proc_a = Start-Process -FilePath $ps -ArgumentList @("-NoProfile", "-Command", $client_a) -RedirectStandardOutput $client_a_log -NoNewWindow -PassThru
$proc_b = Start-Process -FilePath $ps -ArgumentList @("-NoProfile", "-Command", $client_b) -RedirectStandardOutput $client_b_log -NoNewWindow -PassThru
Wait-Process -Id $proc_a.Id, $proc_b.Id -Timeout 90

Write-Host "==== Client A ===="
Get-Content $client_a_log
Write-Host "==== Client B ===="
Get-Content $client_b_log
Write-Host ("Logs: {0}" -f $log_dir)
