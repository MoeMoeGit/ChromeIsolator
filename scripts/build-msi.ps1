param(
    [string]$Version
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $repoRoot "artifacts\publish\win-x64"
$installerObjDir = Join-Path $repoRoot "artifacts\installer-obj"
$installerOutDir = Join-Path $repoRoot "artifacts\installer"
$generatedWxs = Join-Path $installerObjDir "PublishedFiles.wxs"
$productWxs = Join-Path $repoRoot "installer\Product.wxs"

function Get-ProjectVersion {
    $propsPath = Join-Path $repoRoot "Directory.Build.props"
    [xml]$props = Get-Content -LiteralPath $propsPath -Raw
    return $props.Project.PropertyGroup.Version
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-ProjectVersion
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Version is empty."
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "MSI ProductVersion must use numeric major.minor.patch format. Actual: $Version"
}

$msiPath = Join-Path $installerOutDir "ChromeIsolator-Setup-x64-v$Version.msi"

& (Join-Path $PSScriptRoot "publish-win-x64.ps1") -Version $Version

New-Item -ItemType Directory -Force -Path $installerObjDir | Out-Null
New-Item -ItemType Directory -Force -Path $installerOutDir | Out-Null

function Convert-To-WixId {
    param([string]$Value)
    $id = [regex]::Replace($Value, '[^A-Za-z0-9_\.]', '_')
    if ($id -notmatch '^[A-Za-z_]') {
        $id = "_$id"
    }
    return $id
}

function Get-RelativePath {
    param(
        [string]$BasePath,
        [string]$FullPath
    )
    $baseUri = [Uri]((Resolve-Path -LiteralPath $BasePath).Path.TrimEnd('\') + '\')
    $fullUri = [Uri](Resolve-Path -LiteralPath $FullPath).Path
    return [Uri]::UnescapeDataString($baseUri.MakeRelativeUri($fullUri).ToString()).Replace('/', '\')
}

$files = Get-ChildItem -LiteralPath $publishDir -Recurse -File | Sort-Object FullName
$components = New-Object System.Collections.Generic.List[string]
$content = New-Object System.Text.StringBuilder

[void]$content.AppendLine('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">')
[void]$content.AppendLine('  <Fragment>')
[void]$content.AppendLine('    <DirectoryRef Id="INSTALLFOLDER">')

$rootFiles = $files | Where-Object { $_.DirectoryName -eq $publishDir }
foreach ($file in $rootFiles) {
    $relative = Get-RelativePath $publishDir $file.FullName
    $componentId = Convert-To-WixId "cmp_$relative"
    $components.Add($componentId)
    [void]$content.AppendLine("      <Component Id=""$componentId"" Guid=""*"">")
    [void]$content.AppendLine("        <File Source=""$($file.FullName)"" KeyPath=""yes"" />")
    [void]$content.AppendLine('      </Component>')
}

$directories = Get-ChildItem -LiteralPath $publishDir -Recurse -Directory | Sort-Object FullName
foreach ($directory in $directories) {
    $relativeDirectory = Get-RelativePath $publishDir $directory.FullName
    $directoryId = Convert-To-WixId "dir_$relativeDirectory"
    [void]$content.AppendLine("      <Directory Id=""$directoryId"" Name=""$($directory.Name)"">")

    $directoryFiles = $files | Where-Object { $_.DirectoryName -eq $directory.FullName }
    foreach ($file in $directoryFiles) {
        $relative = Get-RelativePath $publishDir $file.FullName
        $componentId = Convert-To-WixId "cmp_$relative"
        $components.Add($componentId)
        [void]$content.AppendLine("        <Component Id=""$componentId"" Guid=""*"">")
        [void]$content.AppendLine("          <File Source=""$($file.FullName)"" KeyPath=""yes"" />")
        [void]$content.AppendLine('        </Component>')
    }

    [void]$content.AppendLine('      </Directory>')
}

[void]$content.AppendLine('    </DirectoryRef>')
[void]$content.AppendLine('  </Fragment>')
[void]$content.AppendLine('  <Fragment>')
[void]$content.AppendLine('    <ComponentGroup Id="PublishedFiles">')
foreach ($component in $components) {
    [void]$content.AppendLine("      <ComponentRef Id=""$component"" />")
}
[void]$content.AppendLine('    </ComponentGroup>')
[void]$content.AppendLine('  </Fragment>')
[void]$content.AppendLine('</Wix>')

Set-Content -LiteralPath $generatedWxs -Value $content.ToString() -Encoding UTF8

wix build --acceptEula wix7 `
    -arch x64 `
    -d "ProjectRoot=$repoRoot" `
    -d "ProductVersion=$Version" `
    -out $msiPath `
    $productWxs `
    $generatedWxs

if (-not (Test-Path -LiteralPath $msiPath)) {
    throw "MSI was not created."
}

Write-Host "MSI created at $msiPath"
