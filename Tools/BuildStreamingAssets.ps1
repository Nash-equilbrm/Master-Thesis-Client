<#
.SYNOPSIS
    Reconstructs Assets/StreamingAssets/{FFmpeg,MediaMTX,DibrBridge,OpenDIBR} —
    the ~467MB of third-party/build binaries the live DIBR camera-switch
    feature needs, which are gitignored rather than committed (see
    Master-Thesis-Reports/action_log_Sep_20th_dibr_camera_switch.md).

    Requires the dibr-bridge and OpenDIBR repos checked out as siblings of
    this repo (same layout as CLAUDE.md's workspace root table), winget for
    FFmpeg/MediaMTX, and (for -DibrBridge) Python 3.11+ on PATH.

.PARAMETER Only
    Restrict the run to one or more of: FFmpeg, MediaMTX, DibrBridge, OpenDIBR.
    Default: all four.

.PARAMETER Force
    Re-run a step even if its StreamingAssets target already has files
    (e.g. re-freeze dibr-bridge.exe after a code change).

.EXAMPLE
    ./Tools/BuildStreamingAssets.ps1
.EXAMPLE
    ./Tools/BuildStreamingAssets.ps1 -Only DibrBridge -Force
#>
param(
    [ValidateSet('FFmpeg', 'MediaMTX', 'DibrBridge', 'OpenDIBR')]
    [string[]]$Only = @('FFmpeg', 'MediaMTX', 'DibrBridge', 'OpenDIBR'),
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$WorkspaceRoot = Split-Path -Parent $RepoRoot
$StreamingAssets = Join-Path $RepoRoot 'Assets\StreamingAssets'

function Find-WinGetBinary([string]$PackageId, [string]$FileName) {
    $existing = Get-Command $FileName -ErrorAction SilentlyContinue
    if ($existing) { return $existing.Source }

    winget list --id $PackageId -e | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Installing $PackageId via winget..."
        winget install --id $PackageId -e --accept-package-agreements --accept-source-agreements
    }

    $packagesDir = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages'
    $found = Get-ChildItem $packagesDir -Recurse -Filter $FileName -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $found) {
        throw "Could not locate $FileName after installing $PackageId — check $packagesDir manually."
    }
    return $found.FullName
}

function Step-FFmpeg {
    $dest = Join-Path $StreamingAssets 'FFmpeg'
    if ((Test-Path (Join-Path $dest 'ffmpeg.exe')) -and -not $Force) {
        Write-Host "[FFmpeg] already present, skipping (use -Force to redo)."
        return
    }
    Write-Host "[FFmpeg] locating/installing (Gyan.FFmpeg.Full, needed for libx264)..."
    $exe = Find-WinGetBinary 'Gyan.FFmpeg.Full' 'ffmpeg.exe'
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Copy-Item $exe -Destination (Join-Path $dest 'ffmpeg.exe') -Force
    Write-Host "[FFmpeg] copied to $dest"
}

function Step-MediaMTX {
    $dest = Join-Path $StreamingAssets 'MediaMTX'
    if ((Test-Path (Join-Path $dest 'mediamtx.exe')) -and -not $Force) {
        Write-Host "[MediaMTX] already present, skipping (use -Force to redo)."
        return
    }
    Write-Host "[MediaMTX] locating/installing (bluenviron.mediamtx)..."
    $exe = Find-WinGetBinary 'bluenviron.mediamtx' 'mediamtx.exe'
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Copy-Item $exe -Destination (Join-Path $dest 'mediamtx.exe') -Force
    $yml = Join-Path (Split-Path $exe -Parent) 'mediamtx.yml'
    if (Test-Path $yml) {
        Copy-Item $yml -Destination (Join-Path $dest 'mediamtx.yml') -Force
    } else {
        Write-Warning "[MediaMTX] no mediamtx.yml found next to the exe — default config (RTSP on :8554) will be used at runtime."
    }
    Write-Host "[MediaMTX] copied to $dest"
}

function Step-DibrBridge {
    $dest = Join-Path $StreamingAssets 'DibrBridge'
    $sourceRepo = Join-Path $WorkspaceRoot 'dibr-bridge'
    if (-not (Test-Path $sourceRepo)) {
        Write-Warning "[DibrBridge] sibling repo not found at $sourceRepo — skipping. Clone dibr-bridge next to this repo first."
        return
    }
    if ((Test-Path (Join-Path $dest 'dibr-bridge.exe')) -and -not $Force) {
        Write-Host "[DibrBridge] already present, skipping (use -Force to rebuild)."
        return
    }

    Push-Location $sourceRepo
    try {
        $venv = Join-Path $sourceRepo '.venv'
        if (-not (Test-Path $venv)) {
            Write-Host "[DibrBridge] creating venv and installing dependencies..."
            python -m venv .venv
            & "$venv\Scripts\pip.exe" install -r requirements.txt pyinstaller
        }

        Write-Host "[DibrBridge] freezing via PyInstaller..."
        & "$venv\Scripts\python.exe" -m PyInstaller --onedir --name dibr-bridge --console --noconfirm entrypoint.py

        $distDir = Join-Path $sourceRepo 'dist\dibr-bridge'
        if (-not (Test-Path $distDir)) {
            throw "[DibrBridge] PyInstaller did not produce $distDir"
        }
        if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
        Copy-Item $distDir -Destination $dest -Recurse -Force
        Write-Host "[DibrBridge] copied to $dest"
    } finally {
        Pop-Location
    }
}

function Step-OpenDIBR {
    $dest = Join-Path $StreamingAssets 'OpenDIBR'
    $sourceRepo = Join-Path $WorkspaceRoot 'OpenDIBR'
    if (-not (Test-Path $sourceRepo)) {
        Write-Warning "[OpenDIBR] sibling repo not found at $sourceRepo — skipping. Clone OpenDIBR next to this repo first."
        return
    }
    if ((Test-Path (Join-Path $dest 'RealtimeDIBR.exe')) -and -not $Force) {
        Write-Host "[OpenDIBR] already present, skipping (use -Force to redo)."
        return
    }

    $candidates = @(
        (Join-Path $sourceRepo 'out\build\x64-Release\Release'),
        (Join-Path $sourceRepo 'out\build\x64-Debug\Debug')
    )
    $buildDir = $candidates | Where-Object { Test-Path (Join-Path $_ 'RealtimeDIBR.exe') } | Select-Object -First 1
    if (-not $buildDir) {
        throw "[OpenDIBR] no built RealtimeDIBR.exe found under $sourceRepo\out\build\* — build it in Visual Studio/CMake first (see OpenDIBR/README.md)."
    }

    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Get-ChildItem $buildDir -Include '*.exe', '*.dll', '*.pdb' -Recurse |
        Copy-Item -Destination $dest -Force
    Write-Host "[OpenDIBR] copied runtime files from $buildDir to $dest"
}

foreach ($step in $Only) {
    switch ($step) {
        'FFmpeg'     { Step-FFmpeg }
        'MediaMTX'   { Step-MediaMTX }
        'DibrBridge' { Step-DibrBridge }
        'OpenDIBR'   { Step-OpenDIBR }
    }
}

Write-Host "`nDone. Re-open/re-import in Unity so it picks up new StreamingAssets files (new .meta files get generated automatically)."
