#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$InstallerPath, [switch]$SkipUi)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath("$PSScriptRoot\..")
$InstallerPath = [IO.Path]::GetFullPath($InstallerPath)
$artifactRoot = [IO.Path]::GetFullPath("$root\artifacts\")
if (!$InstallerPath.StartsWith($artifactRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Only project artifact installers can be tested.' }
$localData = [Environment]::GetFolderPath('LocalApplicationData')
$testInstall = [IO.Path]::GetFullPath((Join-Path $localData 'Programs\PrivateTimeTrace.InstallTest'))
$expectedTarget = Join-Path $localData 'Programs\PrivateTimeTrace.InstallTest'
if ($testInstall -ne $expectedTarget) { throw 'Unexpected test installation target.' }
$testRegistry = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PrivateTimeTrace.InstallTest'
$menu = Join-Path ([Environment]::GetFolderPath('Programs')) '时间迹（安装验证）'
if ((Test-Path -LiteralPath $testInstall) -or (Test-Path -LiteralPath $testRegistry) -or (Test-Path -LiteralPath $menu)) { throw 'Installer test target is not empty. Inspect the previous run; no existing files are removed.' }
$checks = "$root\tests\PrivateTimeTrace.Checks\bin\Release\net10.0\PrivateTimeTrace.Checks.dll"
$fixture = "$root\artifacts\qa\installer-$([Guid]::NewGuid().ToString('N')).db"
New-Item -ItemType Directory -Path (Split-Path -Parent $fixture) -Force | Out-Null
$realDb = Join-Path $localData 'PrivateTimeTrace\private-time-trace.db'
$realExisted = Test-Path -LiteralPath $realDb
function Fingerprint([string]$Database) {
    $result = & dotnet $checks --fingerprint $Database
    if ($LASTEXITCODE) { throw 'Database fingerprint failed.' }
    return ($result -join "`n")
}
$beforeReal = if ($realExisted) { Fingerprint $realDb } else { '' }
function Run-Installer([string]$Path, [string[]]$Arguments, [int]$ExpectedCode = 0) {
    $process = Start-Process -FilePath $Path -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(60000)) { throw "Installer did not finish within 60 seconds. Inspect PID $($process.Id); it was not killed." }
    if ($process.ExitCode -ne $ExpectedCode) { throw "Unexpected installer exit code $($process.ExitCode), expected $ExpectedCode." }
}
$active = $null
try {
    Run-Installer $InstallerPath @('/S','/TESTINSTALL') 3
    if (Test-Path -LiteralPath $testInstall) { throw 'Unaccepted silent install wrote files.' }
    'PASS silent install requires explicit license acceptance'
    Run-Installer $InstallerPath @('/S','/ACCEPTLICENSES','/TESTINSTALL')
    $installedExe = Join-Path $testInstall 'PrivateTimeTrace.exe'
    foreach ($relative in @('PrivateTimeTrace.exe','PrivateTimeTrace.dll','Uninstall.exe','LICENSE','PRIVACY.md','THIRD_PARTY_NOTICES.md','licenses\dependencies.json','Assets\AppIcon.ico')) {
        if (!(Test-Path -LiteralPath (Join-Path $testInstall $relative))) { throw "Missing installed file: $relative" }
    }
    $registry = Get-ItemProperty -LiteralPath $testRegistry
    if ($registry.Publisher -ne 'fplity' -or $registry.InstallLocation -ne $testInstall) { throw 'Wrong uninstall registration.' }
    if (!(Test-Path -LiteralPath "$menu\时间迹（安装验证）.lnk")) { throw 'Start menu shortcut missing.' }
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut("$menu\时间迹（安装验证）.lnk")
    if ($shortcut.TargetPath -ne $installedExe) { throw 'Start menu target is incorrect.' }
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
    if ((Get-FileHash -LiteralPath "$testInstall\Assets\AppIcon.ico").Hash -ne (Get-FileHash -LiteralPath "$root\Assets\AppIcon.ico").Hash) { throw 'Installer did not include the designed icon.' }
    'PASS current-user installation, license files, icon and start menu registration'
    if ($SkipUi) {
        & dotnet $checks --seed $fixture
        if ($LASTEXITCODE) { throw 'Could not create isolated fixture.' }
    } else {
        & "$PSScriptRoot\verify-ui.ps1" -ExePath $installedExe -FixturePath $fixture
        Get-Process PrivateTimeTrace -ErrorAction SilentlyContinue | Where-Object Path -eq $installedExe | ForEach-Object {
            if (!$_.WaitForExit(10000)) { throw 'Installed UI did not close after verification.' }
        }
        'PASS full UI regression on the installed executable'
    }
    $beforeUpgrade = Fingerprint $fixture
    $stateBefore = (& dotnet $checks --inspect $fixture) -join "`n"
    $sentinel = Join-Path $testInstall 'user-file-preserve-test.txt'
    Copy-Item -LiteralPath "$root\LICENSE" -Destination $sentinel
    $sentinelHash = (Get-FileHash -LiteralPath $sentinel).Hash
    $active = Start-Process -FilePath $installedExe -ArgumentList '--data-file', ('"' + $fixture + '"') -PassThru
    for ($attempt=0; $attempt -lt 40; $attempt++) {
        Start-Sleep -Milliseconds 250
        $active.Refresh()
        if ($active.HasExited -or $active.MainWindowHandle) { break }
    }
    if ($active.HasExited -or !$active.MainWindowHandle) { throw 'Installed app did not open a native window.' }
    Run-Installer $InstallerPath @('/S','/ACCEPTLICENSES','/TESTINSTALL') 6
    Run-Installer "$testInstall\Uninstall.exe" @('/S',("_?=$testInstall")) 6
    'PASS install and uninstall reject a running application without force-killing it'
    [void]$active.CloseMainWindow()
    if (!$active.WaitForExit(10000) -or $active.ExitCode -ne 0) { throw 'Installed app did not exit cleanly.' }
    $active = $null
    Run-Installer $InstallerPath @('/S','/ACCEPTLICENSES','/TESTINSTALL')
    if ((Fingerprint $fixture) -ne $beforeUpgrade -or ((& dotnet $checks --inspect $fixture) -join "`n") -ne $stateBefore) { throw 'Upgrade changed study data or settings.' }
    'PASS overwrite upgrade preserves isolated study data and preferences'
    # _?= runs the uninstaller in place so its actual exit status is observable.
    Run-Installer "$testInstall\Uninstall.exe" @('/S',("_?=$testInstall"))
    for ($attempt=0; $attempt -lt 20 -and (Test-Path -LiteralPath $installedExe); $attempt++) { Start-Sleep -Milliseconds 100 }
    if ((Test-Path -LiteralPath $installedExe) -or (Test-Path -LiteralPath $testRegistry) -or (Test-Path -LiteralPath $menu)) { throw 'Installed program, registry or start menu entry was not removed.' }
    if ((Fingerprint $fixture) -ne $beforeUpgrade) { throw 'Uninstall changed study data.' }
    if (!(Test-Path -LiteralPath $sentinel) -or (Get-FileHash -LiteralPath $sentinel).Hash -ne $sentinelHash) { throw 'Uninstaller removed an unknown user file.' }
    if ($realExisted) {
        if ((Fingerprint $realDb) -ne $beforeReal) { throw 'Real data changed during verification; inspect before claiming data preservation.' }
    } elseif (Test-Path -LiteralPath $realDb) { throw 'Test unexpectedly created a real database.' }
    'PASS uninstall removes installed application but preserves extra files and study data'
    # Narrow cleanup: only our known fixture copy and in-place test uninstaller.
    # No recursive delete, and the resolved target was verified above.
    Remove-Item -LiteralPath $sentinel
    $remainingUninstaller = Join-Path $testInstall 'Uninstall.exe'
    if (Test-Path -LiteralPath $remainingUninstaller) { Remove-Item -LiteralPath $remainingUninstaller }
    if (@(Get-ChildItem -LiteralPath $testInstall -Force).Count -eq 0) { Remove-Item -LiteralPath $testInstall }
    "PASS installer verification completed. Preserved isolated database: $fixture"
} finally {
    if ($active -and !$active.HasExited) { [void]$active.CloseMainWindow() }
}
