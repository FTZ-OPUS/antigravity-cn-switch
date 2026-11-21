$ErrorActionPreference = "Stop"

$RootDir = Split-Path -Parent $PSScriptRoot
$WinDir = $PSScriptRoot
$OutDir = Join-Path $RootDir "dist\AntigravityCnSwitch-Windows"

if (Test-Path $OutDir) {
    Remove-Item -Recurse -Force $OutDir
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$CscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $CscPath)) {
    throw "csc.exe not found at $CscPath"
}

$NodeVersion = "v22.14.0"
$NodeZipUrl = "https://nodejs.org/dist/$NodeVersion/node-$NodeVersion-win-x64.zip"
$CacheDir = Join-Path $RootDir ".cache"
New-Item -ItemType Directory -Force -Path $CacheDir | Out-Null
$NodeZipPath = Join-Path $CacheDir "node-win-x64.zip"
$CachedNodeExe = Join-Path $CacheDir "node.exe"

if (-not (Test-Path $CachedNodeExe)) {
    Write-Host "Downloading Node.js $NodeVersion (win-x64)..."
    Invoke-WebRequest -Uri $NodeZipUrl -OutFile $NodeZipPath
    $ExtractDir = Join-Path $CacheDir "node-extract"
    if (Test-Path $ExtractDir) { Remove-Item -Recurse -Force $ExtractDir }
    Expand-Archive -Path $NodeZipPath -DestinationPath $ExtractDir -Force
    Copy-Item (Join-Path $ExtractDir "node-$NodeVersion-win-x64\node.exe") $CachedNodeExe -Force
    Remove-Item -Recurse -Force $ExtractDir, $NodeZipPath
}

# Build embedded payload.zip containing runtime/node.exe and bundled-patch/
$StageDir = Join-Path $CacheDir "payload-stage"
if (Test-Path $StageDir) { Remove-Item -Recurse -Force $StageDir }
New-Item -ItemType Directory -Force -Path (Join-Path $StageDir "runtime") | Out-Null
Copy-Item $CachedNodeExe (Join-Path $StageDir "runtime\node.exe") -Force
Copy-Item -Recurse -Force (Join-Path $RootDir "patch") (Join-Path $StageDir "bundled-patch")

$PayloadZip = Join-Path $CacheDir "payload.zip"
if (Test-Path $PayloadZip) { Remove-Item -Force $PayloadZip }
Compress-Archive -Path (Join-Path $StageDir "*") -DestinationPath $PayloadZip -Force
Remove-Item -Recurse -Force $StageDir

$ExeOut = Join-Path $OutDir "反重力汉化开关.exe"
Write-Host "Compiling standalone single-file Windows exe -> $ExeOut"

& $CscPath `
    /nologo `
    /optimize+ `
    /target:winexe `
    /utf8output `
    "/win32icon:$WinDir\app.ico" `
    "/win32manifest:$WinDir\app.manifest" `
    "/resource:$PayloadZip,AntigravityCnSwitch.payload.zip" `
    "/out:$ExeOut" `
    "/reference:System.dll" `
    "/reference:System.Core.dll" `
    "/reference:System.Drawing.dll" `
    "/reference:System.Windows.Forms.dll" `
    "/reference:System.Web.Extensions.dll" `
    "/reference:System.IO.Compression.dll" `
    "/reference:System.IO.Compression.FileSystem.dll" `
    "$WinDir\Program.cs"

if ($LASTEXITCODE -ne 0) {
    throw "csc.exe failed with exit code $LASTEXITCODE"
}

Remove-Item -Force $PayloadZip
Write-Host "Built standalone exe at: $ExeOut"
