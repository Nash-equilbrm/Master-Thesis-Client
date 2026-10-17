<#
.SYNOPSIS
    Reconstructs Assets/StreamingAssets/{FFmpeg,MediaMTX,DibrBridge,OpenDIBR} —
    the ~467MB of third-party/build binaries the live DIBR camera-switch
    feature needs, which are gitignored rather than committed (see
    Master-Thesis-Reports/action_log_Sep_20th_dibr_camera_switch.md).

    Requires the dibr-bridge and OpenDIBR repos checked out as siblings of
    this repo (same layout as CLAUDE.md's workspace root table), winget for
    FFmpeg/MediaMTX, and (for -DibrBridge) Python 3.11+ on PATH.

    DibrBridge builds the Fast-FoundationStereo variant ONLY
    (dibr-bridge-ffs.spec: torch + FFS source + 20-30-48 checkpoint and
    cfg.yaml under DibrBridge\weights, plus FFS LICENSE.txt; multi-GB,
    NVIDIA GPU viewers). Needs dibr-bridge\.venv with requirements-ffs.txt
    installed and the Fast-FoundationStereo repo as a sibling; throws if any
    of that is missing.

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
        winget install --id $PackageId -e --accept-package-agreements --accept-source-agreements | Out-Null
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

    # The FFS build is the ONLY dibr-bridge.exe we ship (no SGBM-only exe):
    # fail loudly if anything it needs is missing rather than building a
    # variant that silently lacks the model.
    $specName = 'dibr-bridge-ffs.spec'
    $ffsRepo = Join-Path $WorkspaceRoot 'Fast-FoundationStereo'
    $ffsWeights = Join-Path $ffsRepo 'weights\20-30-48'
    foreach ($f in @('model_best_bp2_serialize.pth', 'cfg.yaml')) {
        if (-not (Test-Path (Join-Path $ffsWeights $f))) {
            throw "[DibrBridge] needs $ffsWeights\$f (Fast-FoundationStereo sibling repo + 20-30-48 checkpoint)."
        }
    }
    if (-not (Test-Path (Join-Path $ffsRepo 'core'))) {
        throw "[DibrBridge] Fast-FoundationStereo repo not found at $ffsRepo (clone it next to this repo)."
    }
    if (-not (Test-Path (Join-Path $ffsRepo 'LICENSE.txt'))) {
        throw "[DibrBridge] needs $ffsRepo\LICENSE.txt (must ship with the weights)."
    }
    if (-not (Test-Path (Join-Path $sourceRepo $specName))) {
        throw "[DibrBridge] $specName missing in $sourceRepo."
    }
    # Separate work/dist dirs from the repo's default dist\ and build\.
    $pyiArgs = @('--workpath', 'build-ffs', '--distpath', 'dist-ffs')
    $distDir = Join-Path $sourceRepo 'dist-ffs\dibr-bridge'

    Push-Location $sourceRepo
    try {
        $venv = Join-Path $sourceRepo '.venv'
        if (-not (Test-Path $venv)) {
            Write-Host "[DibrBridge] creating venv and installing dependencies..."
            python -m venv .venv
            & "$venv\Scripts\pip.exe" install -r requirements.txt pyinstaller
        }

        & "$venv\Scripts\python.exe" -c "import torch, timm, einops, omegaconf" 2>$null
        if ($LASTEXITCODE -ne 0) {
            throw "[DibrBridge] torch/timm/einops/omegaconf missing in $venv (see dibr-bridge\requirements-ffs.txt)."
        }

        Write-Host "[DibrBridge] freezing via PyInstaller ($specName)..."
        & "$venv\Scripts\python.exe" -m PyInstaller $specName --noconfirm @pyiArgs
        if ($LASTEXITCODE -ne 0) { throw "[DibrBridge] PyInstaller failed (exit $LASTEXITCODE)" }

        if (-not (Test-Path $distDir)) {
            throw "[DibrBridge] PyInstaller did not produce $distDir"
        }
        if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
        Copy-Item $distDir -Destination $dest -Recurse -Force
        Write-Host "[DibrBridge] copied to $dest"

        $wDest = Join-Path $dest 'weights\20-30-48'
        New-Item -ItemType Directory -Force -Path $wDest | Out-Null
        Copy-Item (Join-Path $ffsWeights 'model_best_bp2_serialize.pth') -Destination $wDest -Force
        Copy-Item (Join-Path $ffsWeights 'cfg.yaml') -Destination $wDest -Force
        Copy-Item (Join-Path $ffsRepo 'LICENSE.txt') -Destination (Join-Path $dest 'FFS_LICENSE.txt') -Force
        Write-Host "[DibrBridge] FFS checkpoint + license copied to $dest"
    } finally {
        Pop-Location
    }
}

function Step-OpenDIBR {
    $dest = Join-Path $StreamingAssets 'OpenDIBR'
    $sourceRepo = Join-Path $WorkspaceRoot 'open-dibr'
    if (-not (Test-Path $sourceRepo)) {
        Write-Warning "[OpenDIBR] sibling repo not found at $sourceRepo — skipping. Clone open-dibr next to this repo first."
        return
    }
    if ((Test-Path (Join-Path $dest 'RealtimeDIBR.exe')) -and -not $Force) {
        Write-Host "[OpenDIBR] already present, skipping (use -Force to redo)."
        return
    }

    $candidates = @(
        (Join-Path $sourceRepo 'bin\Release'),
        (Join-Path $sourceRepo 'bin\Debug'),
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
