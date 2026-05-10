[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Framework = "net10.0-windows10.0.19041.0",
    [string]$Runtime = "win-x64",
    [string]$Version = "0.1.0",
    [string]$ArtifactsPath = ""
)

$ErrorActionPreference = "Stop"

function Convert-ToMsiVersion {
    param([string]$InputVersion)

    $clean = $InputVersion.TrimStart([char[]]"vV")
    $parts = @($clean -split "[^0-9]+" | Where-Object { $_ -ne "" } | Select-Object -First 3)
    while ($parts.Count -lt 3) {
        $parts += "0"
    }

    return ($parts | ForEach-Object { [int]$_ }) -join "."
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repoRoot "src\FluxMq.UI\FluxMq.UI.csproj"
$installerPath = Join-Path $repoRoot "installer\FluxMq.UI\Product.wxs"

if ([string]::IsNullOrWhiteSpace($ArtifactsPath)) {
    $ArtifactsPath = Join-Path $repoRoot "artifacts\windows"
}

$publishPath = Join-Path $ArtifactsPath "portable\FluxMQ"
$distPath = Join-Path $ArtifactsPath "dist"
$portableZip = Join-Path $distPath "FluxMQ-$Version-portable-$Runtime.zip"
$msiPath = Join-Path $distPath "FluxMQ-$Version-$Runtime.msi"
$msiVersion = Convert-ToMsiVersion $Version

if (Test-Path $ArtifactsPath) {
    Remove-Item -LiteralPath $ArtifactsPath -Recurse -Force
}

New-Item -ItemType Directory -Path $publishPath -Force | Out-Null
New-Item -ItemType Directory -Path $distPath -Force | Out-Null

dotnet publish $projectPath `
    -f $Framework `
    -c $Configuration `
    -p:RuntimeIdentifierOverride=$Runtime `
    -p:WindowsPackageType=None `
    -p:WindowsAppSDKSelfContained=true `
    --self-contained true `
    -p:PublishDir="$publishPath\"

if ($LASTEXITCODE -ne 0) {
    throw "Windows publish failed."
}

Get-ChildItem -Path $publishPath -Recurse -Filter *.pdb | Remove-Item -Force

Compress-Archive -Path (Join-Path $publishPath "*") -DestinationPath $portableZip -Force

if (-not (Get-Command wix -ErrorAction SilentlyContinue)) {
    throw "WiX CLI was not found. Install it with: dotnet tool install --global wix"
}

wix build $installerPath `
    -arch x64 `
    -d "PublishDir=$publishPath" `
    -d "ProductVersion=$msiVersion" `
    -out $msiPath

if ($LASTEXITCODE -ne 0) {
    throw "MSI build failed."
}

Write-Host "Portable package: $portableZip"
Write-Host "MSI package: $msiPath"
