# Requires Windows PowerShell 5.1 and an elevated session.

$ErrorActionPreference = "Stop"

function Assert-Elevated {
    $current = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($current)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Register-Driver.ps1 must be run from an elevated Windows PowerShell 5.1 session."
    }
}

Assert-Elevated

$root = Split-Path -Parent $PSScriptRoot
$candidates = @(
    (Join-Path $root "src\ASCOM.SnowFlakeProxy\bin\Release\ASCOM.SnowFlakeProxy.exe"),
    (Join-Path $root "src\ASCOM.SnowFlakeProxy\bin\Debug\ASCOM.SnowFlakeProxy.exe")
)

$exe_path = $null
foreach ($candidate in $candidates) {
    if (Test-Path $candidate) {
        $exe_path = $candidate
        break
    }
}

if ($null -eq $exe_path) {
    throw "Could not find ASCOM.SnowFlakeProxy.exe. Build the solution first."
}

Write-Host "Registering $exe_path"
& $exe_path "/regserver"
if ($LASTEXITCODE -ne 0 -and $null -ne $LASTEXITCODE) {
    throw ("/regserver exited with code {0}" -f $LASTEXITCODE)
}

Write-Host "Registered ASCOM.SnowFlakeProxy.FilterWheel"
