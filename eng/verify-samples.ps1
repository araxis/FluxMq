[CmdletBinding()]
param(
    [string]$Configuration = "Debug",
    [int]$DurationMilliseconds = 1000
)

$ErrorActionPreference = "Stop"

function Invoke-Dotnet {
    param(
        [string[]]$Arguments,
        [string]$FailureMessage
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw $FailureMessage
    }
}

function Write-Step {
    param([string]$Message)

    Write-Host ""
    Write-Host "==> $Message"
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$cliProject = Join-Path $repoRoot "src\FluxMq.Cli\FluxMq.Cli.csproj"
$cliDll = Join-Path $repoRoot "src\FluxMq.Cli\bin\$Configuration\net10.0\FluxMq.Cli.dll"
$metricsSample = Join-Path $repoRoot "samples\flow-applications\metrics-only.json"
$generatedSample = Join-Path $repoRoot "samples\flow-applications\generated-traffic-inspect.json"

Write-Step "Build CLI"
Invoke-Dotnet `
    -Arguments @(
        "build",
        $cliProject,
        "-c",
        $Configuration,
        "--nologo",
        "-p:UseSharedCompilation=false",
        "-p:UseAppHost=false",
        "/nr:false"
    ) `
    -FailureMessage "CLI build failed."

if (-not (Test-Path $cliDll)) {
    throw "Expected CLI output was not found: $cliDll"
}

Write-Step "Validate metrics sample"
Invoke-Dotnet `
    -Arguments @(
        $cliDll,
        "validate",
        "--config",
        $metricsSample,
        "--output",
        "json"
    ) `
    -FailureMessage "Metrics sample validation failed."

Write-Step "Validate generated traffic sample"
Invoke-Dotnet `
    -Arguments @(
        $cliDll,
        "validate",
        "--config",
        $generatedSample,
        "--output",
        "json"
    ) `
    -FailureMessage "Generated traffic sample validation failed."

Write-Step "Run generated traffic sample"
Invoke-Dotnet `
    -Arguments @(
        $cliDll,
        "run",
        "--config",
        $generatedSample,
        "--duration-ms",
        $DurationMilliseconds.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    ) `
    -FailureMessage "Generated traffic sample run failed."

Write-Host ""
Write-Host "Sample verification completed."
