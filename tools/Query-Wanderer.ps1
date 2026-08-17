# Requires Windows PowerShell 5.1
# Queries ASCOM.WandererSnowflakeFilterWheel1.FilterWheel.
# Connecting to hardware is required for Names, FocusOffsets, and Position.
# The vendor Position getter can block for many seconds; this script times it.

$ErrorActionPreference = "Stop"
$prog_id = "ASCOM.WandererSnowflakeFilterWheel1.FilterWheel"

function Write-Section {
    param([string]$title)
    Write-Host ""
    Write-Host ("=" * 72)
    Write-Host $title
    Write-Host ("=" * 72)
}

function Format-Value {
    param($value)
    if ($null -eq $value) {
        return "<null>"
    }
    return [string]$value
}

function Write-Array {
    param(
        [string]$label,
        $items
    )
    if ($null -eq $items) {
        Write-Host ("{0}: <null>" -f $label)
        return
    }
    $count = @($items).Length
    Write-Host ("{0} (count={1}):" -f $label, $count)
    for ($index = 0; $index -lt $count; $index++) {
        Write-Host ("  [{0}] {1}" -f $index, $items[$index])
    }
}

function Release-Com {
    param($com_object)
    if (($null -ne $com_object) -and
        [Runtime.InteropServices.Marshal]::IsComObject($com_object)) {
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($com_object)
    }
}

Write-Section "ASCOM Platform"
$util = $null
try {
    $util = New-Object -ComObject "ASCOM.Utilities.Util"
    Write-Host ("PlatformVersion: {0}" -f $util.PlatformVersion)
}
catch {
    Write-Host ("FAILED to read PlatformVersion: {0}" -f $_.Exception.Message)
}
finally {
    Release-Com $util
}

Write-Section ("Instantiate {0}" -f $prog_id)
$filter_wheel = $null
try {
    $filter_wheel = New-Object -ComObject $prog_id
    Write-Host "Instantiated OK"
}
catch {
    Write-Host ("FAILED: {0}" -f $_.Exception.Message)
    throw
}

try {
    Write-Section "Identity (no Connect required)"
    foreach ($property_name in @("Name", "Description", "InterfaceVersion", "DriverVersion", "DriverInfo")) {
        try {
            $value = $filter_wheel.$property_name
            Write-Host ("{0}: {1}" -f $property_name, (Format-Value $value))
        }
        catch {
            Write-Host ("{0}: FAILED: {1}" -f $property_name, $_.Exception.Message)
        }
    }

    Write-Section "ASCOM Profile"
    $profile = $null
    try {
        $profile = New-Object -ComObject "ASCOM.Utilities.Profile"
        $profile.DeviceType = "FilterWheel"
        $value_names = $profile.RegisteredDeviceTypes
        Write-Host ("Chooser listing: {0}" -f ($profile.GetProfile($prog_id)))
    }
    catch {
        Write-Host ("Profile dump failed (non-fatal): {0}" -f $_.Exception.Message)
        try {
            if ($null -ne $profile) {
                $subkeys = $profile.Values($prog_id)
                Write-Host "Profile values:"
                foreach ($entry in $subkeys) {
                    Write-Host ("  {0}" -f $entry)
                }
            }
        }
        catch {
            Write-Host ("Profile Values() also failed: {0}" -f $_.Exception.Message)
        }
    }
    finally {
        Release-Com $profile
    }

    Write-Section "Connect"
    $connect_sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $filter_wheel.Connected = $true
        $connect_sw.Stop()
        Write-Host ("Connected = true  duration_ms={0}" -f $connect_sw.ElapsedMilliseconds)
        Write-Host ("Connected GET: {0}" -f $filter_wheel.Connected)
    }
    catch {
        $connect_sw.Stop()
        Write-Host ("Connected = true FAILED after {0} ms: {1}" -f $connect_sw.ElapsedMilliseconds, $_.Exception.Message)
        throw
    }

    Write-Section "Hardware properties"
    foreach ($property_name in @("Name", "Description", "InterfaceVersion", "DriverVersion", "DriverInfo")) {
        try {
            $value = $filter_wheel.$property_name
            Write-Host ("{0}: {1}" -f $property_name, (Format-Value $value))
        }
        catch {
            Write-Host ("{0}: FAILED: {1}" -f $property_name, $_.Exception.Message)
        }
    }

    try {
        Write-Array "Names" $filter_wheel.Names
    }
    catch {
        Write-Host ("Names: FAILED: {0}" -f $_.Exception.Message)
    }

    try {
        Write-Array "FocusOffsets" $filter_wheel.FocusOffsets
    }
    catch {
        Write-Host ("FocusOffsets: FAILED: {0}" -f $_.Exception.Message)
    }

    Write-Section "Position GET (vendor may block several seconds)"
    $position_sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $position = $filter_wheel.Position
        $position_sw.Stop()
        Write-Host ("Position: {0}  getter_ms={1}" -f $position, $position_sw.ElapsedMilliseconds)
    }
    catch {
        $position_sw.Stop()
        Write-Host ("Position GET FAILED after {0} ms: {1}" -f $position_sw.ElapsedMilliseconds, $_.Exception.Message)
    }

    Write-Section "SupportedActions / optional members"
    try {
        $actions = $filter_wheel.SupportedActions
        Write-Array "SupportedActions" $actions
    }
    catch {
        Write-Host ("SupportedActions: FAILED: {0}" -f $_.Exception.Message)
    }

    foreach ($property_name in @("Connecting")) {
        try {
            $value = $filter_wheel.$property_name
            Write-Host ("{0}: {1}" -f $property_name, (Format-Value $value))
        }
        catch {
            Write-Host ("{0}: not available: {1}" -f $property_name, $_.Exception.Message)
        }
    }
}
finally {
    Write-Section "Disconnect and release"
    if ($null -ne $filter_wheel) {
        try {
            if ($filter_wheel.Connected) {
                $disconnect_sw = [System.Diagnostics.Stopwatch]::StartNew()
                $filter_wheel.Connected = $false
                $disconnect_sw.Stop()
                Write-Host ("Connected = false  duration_ms={0}" -f $disconnect_sw.ElapsedMilliseconds)
            }
        }
        catch {
            Write-Host ("Disconnect failed: {0}" -f $_.Exception.Message)
        }
        Release-Com $filter_wheel
        Write-Host "COM object released"
    }
}

Write-Host ""
Write-Host "Done. Paste the full output back into the SnowFlakeProxy chat."
