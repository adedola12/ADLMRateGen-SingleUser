param(
    [switch]$EnableSearch,
    [string]$PythonExe = "python"
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$scriptPath = Join-Path $scriptDir "procurement_forecast_pipeline.py"
$outputDir = Join-Path $scriptDir "procurement_forecast_outputs"

$arguments = @(
    $scriptPath
    "--output-dir"
    $outputDir
)

if ($EnableSearch) {
    $arguments += "--enable-search"
}

& $PythonExe @arguments
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Results saved to:" -ForegroundColor Cyan
Write-Host $outputDir
