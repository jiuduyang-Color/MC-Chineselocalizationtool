# Build both Chinese and English standalone EXEs (Windows x64, self-contained, single-file)
param(
    [string]$Runtime = "win-x64",
    [string]$Config = "Release",
    [string]$OutDir = "publish"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $Root

$Csproj = Join-Path $Root "src\MMCT.CLI\MMCT.CLI.csproj"

function Publish-Exe($assemblyName, $subDir) {
    Write-Host "=== Publishing $assemblyName ($Runtime) ===" -ForegroundColor Cyan
    $publishDir = Join-Path $Root $OutDir | Join-Path -ChildPath $subDir
    if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }

    dotnet publish $Csproj `
        -c $Config `
        -r $Runtime `
        --self-contained true `
        -p:AssemblyName=$assemblyName `
        -p:PublishSingleFile=true `
        -p:EnableCompressionInSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=embedded `
        -o $publishDir

    if ($LASTEXITCODE -ne 0) { throw "Publish failed for $assemblyName" }

    $finalDir = Join-Path $Root $OutDir
    if (!(Test-Path $finalDir)) { New-Item -ItemType Directory -Force -Path $finalDir | Out-Null }

    $srcExe = Join-Path $publishDir "$assemblyName.exe"
    if (Test-Path $srcExe) {
        Copy-Item -Force $srcExe (Join-Path $finalDir "$assemblyName.exe")
        Write-Host "Final EXE copied to: $(Join-Path $finalDir $assemblyName.exe)" -ForegroundColor Green
    }

    # create resource_icon folder next to exe
    $iconDir = Join-Path $publishDir "resource_icon"
    if (!(Test-Path $iconDir)) { New-Item -ItemType Directory -Force -Path $iconDir | Out-Null }
}

Publish-Exe "MMCT_ZH" "MMCT_ZH_tmp"
Publish-Exe "MMCT_EN" "MMCT_EN_tmp"

Write-Host ""
Write-Host "=== Done ===" -ForegroundColor Green
Write-Host "Output folder: $(Join-Path $Root $OutDir)"
