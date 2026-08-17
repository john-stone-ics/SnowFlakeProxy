# Requires Windows PowerShell 5.1
# Single-client integration test for ASCOM.SnowFlakeProxy.FilterWheel

$ErrorActionPreference = "Stop"
$prog_id = "ASCOM.SnowFlakeProxy.FilterWheel"
$poll_ms = 100
$max_getter_ms = 100

function Release-Com {
    param($com_object)
    if (($null -ne $com_object) -and [Runtime.InteropServices.Marshal]::IsComObject($com_object)) {
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($com_object)
    }
}

Write-Host "Instantiating $prog_id"
$filter_wheel = New-Object -ComObject $prog_id
try {
    Write-Host ("Name: {0}" -f $filter_wheel.Name)
    $filter_wheel.Connected = $true
    Write-Array "Names" $filter_wheel.Names
    $start_position = $filter_wheel.Position
    $target = $start_position + 1
    if ($target -ge @($filter_wheel.Names).Length) {
        $target = 0
    }
    if ($target -eq $start_position) {
        throw "Need at least two filter slots to test a move."
    }

    Write-Host ("Starting position: {0}" -f $start_position)
    Write-Host ("Commanding position: {0}" -f $target)
    $set_sw = [Diagnostics.Stopwatch]::StartNew()
    $filter_wheel.Position = $target
    $set_sw.Stop()
    Write-Host ("Setter returned after {0} ms" -f $set_sw.ElapsedMilliseconds)

    $deadline = [DateTime]::UtcNow.AddMinutes(2)
    $saw_moving = $false
    $saw_stale = $false
    while ([DateTime]::UtcNow -lt $deadline) {
        $get_sw = [Diagnostics.Stopwatch]::StartNew()
        $position = $filter_wheel.Position
        $get_sw.Stop()
        Write-Host ("Position = {0}   getter={1}ms" -f $position, $get_sw.ElapsedMilliseconds)
        if ($get_sw.ElapsedMilliseconds -gt $max_getter_ms) {
            throw ("Position GET took {0} ms (limit {1} ms)" -f $get_sw.ElapsedMilliseconds, $max_getter_ms)
        }
        if ($position -eq -1) {
            $saw_moving = $true
        }
        elseif ($position -eq $start_position) {
            $saw_stale = $true
            throw "Saw stale start position during/after move."
        }
        elseif ($position -eq $target) {
            break
        }
        Start-Sleep -Milliseconds $poll_ms
    }

    if (-not $saw_moving) {
        Write-Warning "Did not observe Position=-1. Move may have been faster than the poll interval."
    }
    if ($filter_wheel.Position -ne $target) {
        throw "Did not reach target position."
    }
    if ($saw_stale) {
        throw "Stale position was observed."
    }
    Write-Host "Single-client test passed."
}
finally {
    try { $filter_wheel.Connected = $false } catch { }
    Release-Com $filter_wheel
}

function Write-Array {
    param([string]$label, $items)
    Write-Host ("{0}:" -f $label)
    $index = 0
    foreach ($item in @($items)) {
        Write-Host ("  [{0}] {1}" -f $index, $item)
        $index++
    }
}
