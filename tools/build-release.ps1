#requires -Version 7.0
[CmdletBinding()]
param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath("$PSScriptRoot\..")
[xml]$project = Get-Content -LiteralPath "$root\PrivateTimeTrace.csproj" -Raw
$version = ($project.Project.PropertyGroup | Where-Object Version | Select-Object -First 1).Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Release requires a three-part numeric version.' }
if (!$OutputDirectory) { $OutputDirectory = "$root\artifacts\releases\v$version" }
$output = [IO.Path]::GetFullPath($OutputDirectory)
$allowedRoot = [IO.Path]::GetFullPath("$root\artifacts\")
if (!$output.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Release output must be a new directory under project artifacts/.' }
if (Test-Path -LiteralPath $output) { throw 'Output already exists. Choose a new directory; previous artifacts are never overwritten.' }
foreach ($file in @('LICENSE','PRIVACY.md','THIRD_PARTY_NOTICES.md','CHANGELOG.md','Assets\AppIcon.ico')) {
    if (!(Test-Path -LiteralPath "$root\$file")) { throw "Missing release input: $file" }
}
$compiler = & "$PSScriptRoot\bootstrap-nsis.ps1"
$work = Join-Path $output 'work'
$app = Join-Path $work 'app'
New-Item -ItemType Directory -Path $app -Force | Out-Null
Push-Location $root
try {
    & dotnet run --project tests/PrivateTimeTrace.Checks/PrivateTimeTrace.Checks.csproj -c Release
    if ($LASTEXITCODE) { throw 'Core checks failed.' }
    & dotnet restore PrivateTimeTrace.csproj -p:Platform=x64 -r win-x64 --locked-mode --nologo
    if ($LASTEXITCODE) { throw 'Locked dependency restore failed.' }
    & dotnet publish PrivateTimeTrace.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained true --no-restore -p:DebugType=None -p:DebugSymbols=false "-p:PublishDir=$app\" --nologo -v:minimal
    if ($LASTEXITCODE) { throw 'Self-contained publish failed.' }
    & "$PSScriptRoot\collect-licenses.ps1" -PublishDirectory $app -OutputDirectory "$app\licenses"
    foreach ($file in @('LICENSE','PRIVACY.md','THIRD_PARTY_NOTICES.md','CHANGELOG.md')) {
        Copy-Item -LiteralPath "$root\$file" -Destination "$app\$file"
    }
    Copy-Item -LiteralPath "$root\packaging\README.txt" -Destination "$app\README.txt"
    $forbidden = Get-ChildItem -LiteralPath $app -Recurse -File | Where-Object { $_.Name -match '(\.(db|sqlite|sqlite3)(-.*)?$|\.(pfx|p12|pem|key|cer|pdb|log|dmp)$|^\.env)' }
    if ($forbidden) { throw 'Private data, symbols, credentials or logs found in release staging.' }
    # Present original terms before installation, without replacing upstream notices.
    $terms = [Collections.Generic.List[string]]::new()
    $terms.Add('时间迹 / PrivateTimeTrace - application and bundled component terms')
    $terms.Add('Copyright (c) 2026 fplity. Application code: MIT. Third-party components retain their own terms.')
    $terms.Add('For Microsoft distributable code, external end users and distributors agree to the applicable original Microsoft terms below. Open-source rights are unaffected.')
    $terms.Add((Get-Content -LiteralPath "$root\LICENSE" -Raw))
    $seen = [Collections.Generic.HashSet[string]]::new()
    $licenseFiles = @(Get-Item -LiteralPath "$app\licenses\upstream\dotnet-windows-LICENSE.txt")
    $licenseFiles += Get-ChildItem -LiteralPath "$app\licenses\packages" -Recurse -File | Where-Object { $_.Name -match '^licen[cs]e\.' -and $_.Extension -in @('.txt','.md') }
    foreach ($file in $licenseFiles) {
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        if ($seen.Add($hash)) {
            $terms.Add("`r`n--- $([IO.Path]::GetRelativePath($app,$file.FullName)) ---`r`n")
            $terms.Add((Get-Content -LiteralPath $file.FullName -Raw -Encoding utf8))
        }
    }
    $terms.Add('Additional Windows SDK terms are shown on the following page. Full notices and license inventory are included in the installed licenses folder.')
    $termsFile = "$work\installer-terms.txt"
    $terms | Set-Content -LiteralPath $termsFile -Encoding unicode
    $files = @(Get-ChildItem -LiteralPath $app -Recurse -File | Sort-Object FullName)
    $installLines = [Collections.Generic.List[string]]::new()
    $uninstallLines = [Collections.Generic.List[string]]::new()
    $directories = [Collections.Generic.HashSet[string]]::new()
    $totalBytes = 0L
    foreach ($file in $files) {
        $relative = [IO.Path]::GetRelativePath($app, $file.FullName)
        if ($relative -match '[\$"\r\n]') { throw 'Unsafe installer filename.' }
        $directory = Split-Path -Parent $relative
        $totalBytes += $file.Length
        $installLines.Add('SetOutPath "$INSTDIR' + $(if ($directory) { '\' + $directory } else { '' }) + '"')
        $installLines.Add('ClearErrors')
        $installLines.Add('File "' + $file.FullName + '"')
        $installLines.Add('IfErrors 0 +3')
        $installLines.Add('SetErrorLevel 7')
        $installLines.Add('Abort "A file could not be installed. Close the app and retry."')
        $uninstallLines.Add('Delete "$INSTDIR\' + $relative + '"')
        while ($directory) {
            [void]$directories.Add($directory)
            $directory = Split-Path -Parent $directory
        }
    }
    foreach ($directory in ($directories | Sort-Object { $_.Length } -Descending)) { $uninstallLines.Add('RMDir "$INSTDIR\' + $directory + '"') }
    $installInclude = "$work\install-files.nsh"
    $uninstallInclude = "$work\uninstall-files.nsh"
    $installLines | Set-Content -LiteralPath $installInclude -Encoding utf8
    $uninstallLines | Set-Content -LiteralPath $uninstallInclude -Encoding utf8
    $installer = Join-Path $output "PrivateTimeTrace-$version-win-x64-Setup.exe"
    $parameters = @('/V3','/INPUTCHARSET','UTF8',"/DVERSION=$version","/DAPP_DIR=$app","/DOUTPUT_FILE=$installer","/DTERMS_FILE=$termsFile","/DINSTALL_INCLUDE=$installInclude","/DUNINSTALL_INCLUDE=$uninstallInclude","/DSIZE_KB=$([int][Math]::Ceiling($totalBytes/1024))","$root\packaging\installer.nsi")
    & $compiler @parameters
    if ($LASTEXITCODE) { throw 'NSIS installer compilation failed.' }
    $archive = Join-Path $output "PrivateTimeTrace-$version-win-x64.zip"
    [IO.Compression.ZipFile]::CreateFromDirectory($app, $archive, [IO.Compression.CompressionLevel]::Optimal, $false)
    @($installer, $archive) | ForEach-Object {
        $hash = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $([IO.Path]::GetFileName($_))"
    } | Set-Content -LiteralPath "$output\SHA256SUMS.txt" -Encoding ascii
    Write-Host "Release artifacts: $output"
    Get-ChildItem -LiteralPath $output -File | Select-Object Name,Length
} finally { Pop-Location }
