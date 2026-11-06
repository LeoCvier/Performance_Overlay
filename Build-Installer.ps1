
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'PerformanceOverlay.csproj'
$publish = Join-Path $root 'Publish'
$iss = Join-Path $root 'Installer\PerformanceOverlay.iss'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET 8 SDK is required to build the installer.'
}

# Find Inno Setup 7 or 6
$compilerCandidates = @(
    (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe')
)

$iscc = $compilerCandidates |
    Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1

if (-not $iscc) {
    throw 'Inno Setup 6 or 7 is required.'
}

# Publish application
if (Test-Path $publish) {
    Remove-Item $publish -Recurse -Force
}

dotnet publish $project -c Release -r win-x64 --self-contained true -o $publish

if ($LASTEXITCODE -ne 0) {
    throw 'dotnet publish failed.'
}

# Compile installer
& $iscc $iss

if ($LASTEXITCODE -ne 0) {
    throw 'Inno Setup compilation failed.'
}

Write-Host "Installer created in: $(Join-Path $root 'Output')" -ForegroundColor Green
