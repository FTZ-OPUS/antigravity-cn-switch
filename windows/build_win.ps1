$ErrorActionPreference = "Stop"

$RootDir = Split-Path -Parent $PSScriptRoot
$WinDir = $PSScriptRoot
$OutDir = Join-Path $RootDir "dist\AntigravityCnSwitch-Windows"

if (Test-Path $OutDir) {
    Remove-Item -Recurse -Force $OutDir
}
New-Item -ItemType Directory -Force -Path (Join-Path $OutDir "runtime") | Out-Null

$CscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $CscPath)) {
    throw "csc.exe not found at $CscPath"
}

$ExeOut = Join-Path $OutDir "反重力汉化开关.exe"
Write-Host "Compiling Windows app -> $ExeOut"

& $CscPath `
    /nologo `
    /optimize+ `
    /target:winexe `
    /utf8output `
    "/win32manifest:$WinDir\app.manifest" `
    "/out:$ExeOut" `
    "/reference:System.dll" `
    "/reference:System.Core.dll" `
    "/reference:System.Drawing.dll" `
    "/reference:System.Windows.Forms.dll" `
    "/reference:System.Web.Extensions.dll" `
    "$WinDir\Program.cs"

if ($LASTEXITCODE -ne 0) {
    throw "csc.exe failed with exit code $LASTEXITCODE"
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

Copy-Item $CachedNodeExe (Join-Path $OutDir "runtime\node.exe") -Force
Copy-Item -Recurse -Force (Join-Path $RootDir "patch") (Join-Path $OutDir "patch")

Write-Host "Built Windows package at: $OutDir"
