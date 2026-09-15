param(
    [string]$FixturePath = "$PSScriptRoot\..\artifacts\qa\release-verification-2.db",
    [ValidateRange(1,10)][int]$Iterations = 6
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath("$PSScriptRoot\..")
$FixturePath = [IO.Path]::GetFullPath($FixturePath)
if (!$FixturePath.StartsWith("$projectRoot\artifacts\qa\", [StringComparison]::OrdinalIgnoreCase) -or !(Test-Path -LiteralPath $FixturePath)) { throw 'An existing isolated QA database is required.' }
$exePath = "$projectRoot\artifacts\TimeTrace-win-x64\PrivateTimeTrace.exe"
for ($index = 1; $index -le $Iterations; $index++) {
    $preview = Start-Process -FilePath $exePath -ArgumentList '--data-file', ('"' + $FixturePath + '"') -PassThru
    try {
        Start-Sleep -Seconds 2
        $preview.Refresh()
        if ($preview.HasExited -or !$preview.MainWindowHandle) { throw "Cycle $index has no visible window." }
        $snapshot = & 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\ui-smoke.ps1" -AppProcessId $preview.Id -Action Dump | ConvertFrom-Json
        if ($LASTEXITCODE -or !($snapshot | Where-Object { $_.Id -eq 'ElapsedTime' -and $_.Name -ne '00:00:00' })) { throw 'Active session did not restore.' }
        if (!$preview.CloseMainWindow()) { throw 'Close was not accepted.' }
        if (!$preview.WaitForExit(10000)) { throw 'Process did not exit after window closed.' }
        if ($preview.ExitCode -ne 0) { throw "Abnormal exit: $($preview.ExitCode)" }
        "PASS lifecycle $index/$Iterations, PID $($preview.Id), active timer restored, exit code 0"
    } finally {
        if (!$preview.HasExited) { $preview.CloseMainWindow() | Out-Null }
    }
}
