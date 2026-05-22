param(
    [string]$Version
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\ChromeIsolator.App\ChromeIsolator.App.csproj"
$output = Join-Path $repoRoot "artifacts\publish\win-x64"
$zipPath = Join-Path $repoRoot "artifacts\publish\ChromeIsolator-win-x64-v$Version.zip"

function Get-ProjectVersion {
    $propsPath = Join-Path $repoRoot "Directory.Build.props"
    [xml]$props = Get-Content -LiteralPath $propsPath -Raw
    return $props.Project.PropertyGroup.Version
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-ProjectVersion
    $zipPath = Join-Path $repoRoot "artifacts\publish\ChromeIsolator-win-x64-v$Version.zip"
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Version is empty."
}

if (Test-Path -LiteralPath $output) {
    Remove-Item -LiteralPath $output -Recurse -Force
}

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:Version=$Version `
    -p:AssemblyVersion="$Version.0" `
    -p:FileVersion="$Version.0" `
    -p:InformationalVersion=$Version `
    -o $output

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -Path (Join-Path $output "*") -DestinationPath $zipPath -Force

Write-Host "Published to $output"
Write-Host "Zip created at $zipPath"
