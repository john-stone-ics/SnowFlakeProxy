# Requires Windows PowerShell 5.1
# Two-process multi-client integration test for ASCOM.SnowFlakeProxy1.FilterWheel
#
# Writes each client to a temp .ps1 and launches it with -File so format
# strings keep their quotes (Start-Process -Command strips them).

$ErrorActionPreference = "Stop"
$ps = "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"
$log_dir = Join-Path $env:TEMP "SnowFlakeProxyMultiClient"
New-Item -ItemType Directory -Path $log_dir -Force | Out-Null

$client_a_ps1 = Join-Path $log_dir "client-a.ps1"
$client_b_ps1 = Join-Path $log_dir "client-b.ps1"
$client_a_log = Join-Path $log_dir "client-a.log"
$client_a_err = Join-Path $log_dir "client-a.err"
$client_b_log = Join-Path $log_dir "client-b.log"
$client_b_err = Join-Path $log_dir "client-b.err"

@(
    $client_a_log, $client_a_err, $client_b_log, $client_b_err
) | ForEach-Object {
    if (Test-Path $_) {
        Remove-Item $_ -Force
    }
}

Set-Content -Path $client_a_ps1 -Encoding ASCII -Value @'
$ErrorActionPreference = "Stop"
$prog_id = "ASCOM.SnowFlakeProxy1.FilterWheel"
$fw = New-Object -ComObject $prog_id
try {
    $fw.Connected = $true
    $end = [DateTime]::UtcNow.AddSeconds(45)
    while ([DateTime]::UtcNow -lt $end) {
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $p = $fw.Position
        $sw.Stop()
        $line = "{0:HH:mm:ss.fff} Position={1} getter={2}ms" -f [DateTime]::Now, $p, $sw.ElapsedMilliseconds
        Write-Output $line
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

Set-Content -Path $client_b_ps1 -Encoding ASCII -Value @'
$ErrorActionPreference = "Stop"
$prog_id = "ASCOM.SnowFlakeProxy1.FilterWheel"
Start-Sleep -Seconds 2
$fw = New-Object -ComObject $prog_id
try {
    $fw.Connected = $true
    $start = [int]$fw.Position
    $names = @($fw.Names)
    $target = $start + 1
    if ($target -ge $names.Length) {
        $target = 0
    }
    $line = "{0:HH:mm:ss.fff} command Position={1} from {2}" -f [DateTime]::Now, $target, $start
    Write-Output $line
    $set_sw = [Diagnostics.Stopwatch]::StartNew()
    $fw.Position = [int16]$target
    $set_sw.Stop()
    $line = "{0:HH:mm:ss.fff} setter returned after {1} ms" -f [DateTime]::Now, $set_sw.ElapsedMilliseconds
    Write-Output $line
    $end = [DateTime]::UtcNow.AddSeconds(40)
    while ([DateTime]::UtcNow -lt $end) {
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $p = $fw.Position
        $sw.Stop()
        $line = "{0:HH:mm:ss.fff} Position={1} getter={2}ms" -f [DateTime]::Now, $p, $sw.ElapsedMilliseconds
        Write-Output $line
        if ($p -eq $target) {
            break
        }
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

$proc_a = Start-Process -FilePath $ps -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $client_a_ps1) -RedirectStandardOutput $client_a_log -RedirectStandardError $client_a_err -NoNewWindow -PassThru
Start-Sleep -Milliseconds 500
$proc_b = Start-Process -FilePath $ps -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $client_b_ps1) -RedirectStandardOutput $client_b_log -RedirectStandardError $client_b_err -NoNewWindow -PassThru

Wait-Process -Id @($proc_a.Id, $proc_b.Id) -Timeout 90 -ErrorAction SilentlyContinue

Write-Host "==== Client A stdout ===="
if (Test-Path $client_a_log) { Get-Content $client_a_log }
Write-Host "==== Client A stderr ===="
if (Test-Path $client_a_err) { Get-Content $client_a_err }
Write-Host "==== Client B stdout ===="
if (Test-Path $client_b_log) { Get-Content $client_b_log }
Write-Host "==== Client B stderr ===="
if (Test-Path $client_b_err) { Get-Content $client_b_err }
Write-Host ("Logs: {0}" -f $log_dir)
