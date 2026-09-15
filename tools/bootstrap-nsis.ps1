#requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath("$PSScriptRoot\..")
$version = '3.12'
$sha256 = '56581F90DB321581C5381193D796FFFCF2D24B2F8FED2160A6C6A3BAA67F2C4F'
$toolRoot = Join-Path $root ".tools\nsis-$version"
$compiler = Join-Path $toolRoot 'Bin\makensis.exe'
if (Test-Path -LiteralPath $compiler) {
    $reported = & $compiler /VERSION
    if ($LASTEXITCODE -ne 0 -or "$reported".Trim() -ne "v$version") { throw 'Unexpected NSIS compiler version.' }
    return $compiler
}
$downloadRoot = Join-Path $root '.tools\downloads'
New-Item -ItemType Directory -Path $downloadRoot -Force | Out-Null
$archive = Join-Path $downloadRoot "nsis-$version-verified.zip"
$uri = "https://downloads.sourceforge.net/project/nsis/NSIS%203/$version/nsis-$version.zip"
Invoke-WebRequest -Uri $uri -OutFile $archive -TimeoutSec 90 -UserAgent 'Wget/1.21.4'
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $sha256) {
    # SourceForge sometimes returns its HTML download page instead of the archive.
    # Follow only its official signed downloads URL, then validate the pinned hash.
    $html = Get-Content -LiteralPath $archive -Raw
    $link = [regex]::Match($html, 'https://downloads\.sourceforge\.net/project/nsis/[^"<>\s]+\?ts=[^"<>\s]+')
    if (!$link.Success) { throw 'NSIS download is not the pinned archive. No files were executed.' }
    $redirect = [Net.WebUtility]::HtmlDecode($link.Value)
    Invoke-WebRequest -Uri $redirect -OutFile $archive -TimeoutSec 90 -UserAgent 'Wget/1.21.4'
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $sha256) { throw 'NSIS SHA-256 verification failed.' }
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    foreach ($entry in $zip.Entries) {
        if ($entry.FullName -match '(^[/\\]|(^|[/\\])\.\.([/\\]|$)|:)') { throw 'Unsafe archive entry.' }
        if (!$entry.FullName.StartsWith("nsis-$version/", [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected archive root.' }
    }
} finally { $zip.Dispose() }
if (Test-Path -LiteralPath $toolRoot) { throw "Incomplete tool directory already exists: $toolRoot. Inspect it before retrying." }
Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $root '.tools')
if (!(Test-Path -LiteralPath $compiler)) { throw 'NSIS compiler not found after extraction.' }
return $compiler
