param(
    [string]$ExePath,
    [string]$FixturePath
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath("$PSScriptRoot\..")
if (!$ExePath) {
    [xml]$project = Get-Content -LiteralPath "$projectRoot\PrivateTimeTrace.csproj" -Raw
    $version = ($project.Project.PropertyGroup | Where-Object Version | Select-Object -First 1).Version
    $ExePath = "$projectRoot\artifacts\releases\v$version\work\app\PrivateTimeTrace.exe"
}
if (!$FixturePath) { $FixturePath = "$projectRoot\artifacts\qa\release-verification.db" }
$FixturePath = [IO.Path]::GetFullPath($FixturePath)
$qaRoot = [IO.Path]::GetFullPath("$projectRoot\artifacts\qa\")
if (!$FixturePath.StartsWith($qaRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'UI verification is restricted to artifacts/qa databases.' }
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$helper = "$PSScriptRoot\ui-smoke.ps1"
$powershell = 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe'
$checks = "$projectRoot\tests\PrivateTimeTrace.Checks\bin\Release\net10.0\PrivateTimeTrace.Checks.dll"
if (Test-Path -LiteralPath $FixturePath) { throw 'Choose a new fixture filename; existing data is never reset.' }
& $dotnet $checks --seed $FixturePath
if ($LASTEXITCODE) { throw 'Fixture generation failed.' }
$script:preview = $null
function Open-Preview {
    $script:preview = Start-Process -FilePath $ExePath -ArgumentList '--data-file', ('"' + $FixturePath + '"') -PassThru
    for ($attempt = 0; $attempt -lt 25; $attempt++) {
        Start-Sleep -Milliseconds 200
        $script:preview.Refresh()
        if ($script:preview.HasExited -or $script:preview.MainWindowHandle) { break }
    }
    if ($script:preview.HasExited -or !$script:preview.MainWindowHandle) { throw 'Release has no visible window.' }
    Start-Sleep -Milliseconds 1000
    "Opened release PID $($script:preview.Id)"
}
function Ui([string]$Action, [string]$Id, [string]$Value) {
    $arguments = @('-NoProfile','-ExecutionPolicy','Bypass','-File',$helper,'-AppProcessId', $script:preview.Id, '-Action', $Action)
    if ($Id) { $arguments += @('-AutomationId', $Id) }
    if ($Value) { $arguments += @('-Value', $Value) }
    $result = & $powershell @arguments
    if ($LASTEXITCODE) { throw "UI action failed: $Action $Id" }
    return $result
}
function State {
    $result = & $dotnet $checks --inspect $FixturePath
    if ($LASTEXITCODE) { throw 'Could not read fixture.' }
    return $result | ConvertFrom-Json
}
function Capture([string]$File) {
    & $powershell -NoProfile -ExecutionPolicy Bypass -File $helper -AppProcessId $script:preview.Id -Action Capture -OutputPath "$qaRoot$File"
    if ($LASTEXITCODE) { throw 'Capture failed.' }
}
function Restart-Preview {
    Ui 'Close'
    $script:preview.WaitForExit(5000) | Out-Null
    if (!$script:preview.HasExited) { throw 'Window did not close gracefully.' }
    if ($script:preview.ExitCode -ne 0) { throw "Release exited abnormally: $($script:preview.ExitCode)" }
    Start-Sleep -Milliseconds 500
    Open-Preview
}
try {
    Open-Preview
    Ui 'Click' 'NavAnalytics'
    foreach ($style in @('Liquid','Frosted')) {
        foreach ($trend in @('Line','Bar')) {
            foreach ($topic in @('Line','Bar')) {
                Ui 'Click' "Style$style"
                Ui 'Click' "Trend$trend"
                Ui 'Click' "Topic$topic"
                $saved = State
                if ($saved.Style -ne $style -or $saved.Trend -ne $trend -or $saved.Topic -ne $topic) { throw 'Style or chart choice did not persist independently.' }
                "PASS release UI combination: $style / $trend / $topic"
                if ($style -eq 'Liquid' -and $trend -eq 'Line' -and $topic -eq 'Bar') { Capture 'release-liquid.png' }
                if ($style -eq 'Frosted' -and $trend -eq 'Bar' -and $topic -eq 'Line') { Capture 'release-frosted.png' }
            }
        }
    }
    Ui 'Click' 'TopicLine'
    Restart-Preview
    Ui 'AssertSelected' 'StyleFrosted'
    Ui 'AssertSelected' 'TrendBar'
    Ui 'AssertSelected' 'TopicLine'
    'PASS release UI preference restoration'
    $originalCount = (State).Count
    Ui 'Text' 'SessionNote' 'Release UI verification'
    Ui 'Click' 'TimerAction'
    $active = State
    if (!$active.Active -or $active.ActiveNote -ne 'Release UI verification') { throw 'Timer or note failed to persist.' }
    Restart-Preview
    $tree = Ui 'Dump' | ConvertFrom-Json
    $note = $tree | Where-Object Id -eq 'SessionNote'
    $timer = $tree | Where-Object Id -eq 'ElapsedTime'
    if ($note.Enabled -or $timer.Name -eq '00:00:00') { throw 'Active timer was not restored in UI.' }
    Ui 'Click' 'TimerAction'
    $finished = State
    if ($finished.Active -or $finished.Count -ne ($originalCount + 1) -or $finished.LatestNote -ne 'Release UI verification') { throw 'Timer completion did not save exactly once.' }
    'PASS release timer note, recovery and completion'
    Ui 'Click' 'NavAnalytics'
    foreach ($period in @('Day','Month','Year','All','Week')) {
        Ui 'Click' "Period$period"
        Ui 'AssertSelected' "Period$period"
    }
    'PASS all five reporting periods'
    & $powershell -NoProfile -ExecutionPolicy Bypass -File $helper -AppProcessId $script:preview.Id -Action Resize -Width 950 -Height 700 -LogicalSize
    if ($LASTEXITCODE) { throw 'Could not resize the verification window.' }
    Ui 'Scroll' 'MainScroll' '100'
    Ui 'Click' 'StyleLiquid'
    Ui 'AssertSelected' 'StyleLiquid'
    Capture 'release-narrow-scrolled.png'
    'PASS theme switch remains reachable in a small scrolled window'
    'PASS release UI smoke completed; all records used an isolated database.'
} catch {
    $originalError = $_
    if ($script:preview -and !$script:preview.HasExited) {
        try { Capture 'release-failure-diagnostic.png'; State | ConvertTo-Json } catch { Write-Warning "Diagnostic capture unavailable: $_" }
    }
    throw $originalError
} finally {
    if ($script:preview -and !$script:preview.HasExited) { $script:preview.CloseMainWindow() | Out-Null }
}
