# Requires Windows PowerShell 5.1
# Builds Release, then compiles installer\SnowFlakeProxy.iss with Inno Setup 6.

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
$iscc_candidates = @(
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
    (Join-Path ${env:ProgramFiles} "Inno Setup 6\ISCC.exe"),
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe")
)

if (-not (Test-Path $msbuild)) {
    throw "MSBuild.exe not found at $msbuild"
}

$iscc = $null
foreach ($candidate in $iscc_candidates) {
    if (Test-Path $candidate) {
        $iscc = $candidate
        break
    }
}
if ($null -eq $iscc) {
    throw "Inno Setup 6 ISCC.exe not found."
}

$sln = Join-Path $root "SnowFlakeProxy.sln"
Write-Host "Building Release..."
& $msbuild $sln /p:Configuration=Release /p:Platform="Any CPU" /v:m
if ($LASTEXITCODE -ne 0) {
    throw ("MSBuild failed with exit code {0}" -f $LASTEXITCODE)
}

$exe_path = Join-Path $root "src\ASCOM.SnowFlakeProxy\bin\Release\ASCOM.SnowFlakeProxy.exe"
if (-not (Test-Path $exe_path)) {
    throw "Release EXE was not produced: $exe_path"
}

$dist = Join-Path $root "dist"
New-Item -ItemType Directory -Path $dist -Force | Out-Null

$iss = Join-Path $PSScriptRoot "SnowFlakeProxy.iss"
Write-Host "Compiling installer..."
& $iscc $iss
if ($LASTEXITCODE -ne 0) {
    throw ("ISCC failed with exit code {0}" -f $LASTEXITCODE)
}

$setup = Join-Path $dist "SnowFlakeProxy-0.1.0-Setup.exe"
if (-not (Test-Path $setup)) {
    throw "Installer was not produced: $setup"
}

Write-Host ("Installer: {0}" -f $setup)
