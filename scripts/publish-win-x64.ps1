$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\ChromeIsolator.App\ChromeIsolator.App.csproj"
$output = Join-Path $repoRoot "artifacts\publish\win-x64"

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -o $output

Write-Host "Published to $output"
