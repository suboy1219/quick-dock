param(
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "src\QuickDock.csproj"
$output = Join-Path $root "dist"

if (Test-Path $output) {
    Remove-Item -LiteralPath $output -Recurse -Force
}

$arguments = @(
    "publish",
    $project,
    "-c", "Release",
    "-r", "win-x64",
    "-p:PublishSingleFile=true",
    "-p:DebugType=None",
    "-p:DebugSymbols=false",
    "-o", $output
)

if ($SelfContained) {
    $arguments += "-p:SelfContained=true"
    $arguments += "-p:IncludeNativeLibrariesForSelfExtract=true"
}
else {
    $arguments += "-p:SelfContained=false"
}

dotnet @arguments

if ($LASTEXITCODE -ne 0) {
    throw "Publish failed with exit code $LASTEXITCODE"
}

Write-Host ""
Write-Host "Published to: $output" -ForegroundColor Green
Get-ChildItem $output | Select-Object Name, Length
