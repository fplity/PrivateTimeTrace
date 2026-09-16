param(
    [Parameter(Mandatory=$true)][string]$ExePath,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath("$PSScriptRoot\..")
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (!$output.StartsWith("$root\artifacts\qa\", [StringComparison]::OrdinalIgnoreCase)) { throw 'Use a new artifacts/qa subdirectory.' }
if (Test-Path -LiteralPath $output) { throw 'Existing evidence is never overwritten.' }
New-Item -ItemType Directory -Path $output | Out-Null
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, WindowsBase
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class ChartVisibilityNative {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr w, out RECT r);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr w, IntPtr dc, uint flags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr w, int command);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr w, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr w);
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
}
'@
[ChartVisibilityNative]::SetThreadDpiAwarenessContext([IntPtr](-4)) | Out-Null
$checks = "$root\tests\PrivateTimeTrace.Checks\bin\Release\net10.0\PrivateTimeTrace.Checks.dll"
$results = [Collections.Generic.List[object]]::new()
$preview = $null
function Find-Id([string]$Id) {
    $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $Id)
    $found = $script:window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    if (!$found) { throw "Missing control: $Id" }
    return $found
}
function Select-Id([string]$Id) {
    (Find-Id $Id).GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 450
}
try {
    foreach ($period in @('Day','Month')) {
        $fixture = "$output\$period.db"
        & dotnet $checks --seed-chart-visibility $fixture $period
        if ($LASTEXITCODE) { throw 'Fixture generation failed.' }
        $preview = Start-Process -FilePath $ExePath -ArgumentList '--data-file', ('"' + $fixture + '"'), '--background-test' -PassThru
        for ($attempt=0; $attempt -lt 50; $attempt++) {
            Start-Sleep -Milliseconds 200
            $preview.Refresh()
            if ($preview.HasExited) { throw 'Preview exited during startup.' }
            if ($preview.MainWindowHandle -ne [IntPtr]::Zero) { break }
        }
        $handle = $preview.MainWindowHandle
        if ($handle -eq [IntPtr]::Zero) { throw 'Preview window did not appear.' }
        [ChartVisibilityNative]::ShowWindow($handle, 4) | Out-Null
        $scale = [ChartVisibilityNative]::GetDpiForWindow($handle) / 96.0
        $script:window = [System.Windows.Automation.AutomationElement]::FromHandle($handle)
        Start-Sleep -Milliseconds 800
        Select-Id 'NavAnalytics'
        $lastLabel = if ($period -eq 'Day') { '23' } else { [DateTime]::DaysInMonth([DateTime]::Today.Year, [DateTime]::Today.Month).ToString() }
        foreach ($size in @(1380,950)) {
            [ChartVisibilityNative]::SetWindowPos($handle, [IntPtr]::Zero, 24, 24, [int]($size*$scale), [int](880*$scale), 0x14) | Out-Null
            Start-Sleep -Milliseconds 600
            foreach ($style in @('Liquid','Frosted')) {
                Select-Id "Style$style"
                # Exercise transitions as well as the initially selected period.
                Select-Id 'PeriodYear'
                Select-Id "Period$period"
                foreach ($kind in @('Line','Bar')) {
                    Select-Id "Trend$kind"
                    $chart = Find-Id 'TrendChart'
                    $viewport = [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($chart)
                    $scroll = $viewport.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
                    if ($scroll.Current.HorizontallyScrollable) { throw "$period chart still requires horizontal scrolling." }
                    $viewBounds = $viewport.Current.BoundingRectangle
                    $pointName = '{0} {1} 14{2}{3}' -f $lastLabel, [char]0xB7, [char]0x5206, [char]0x949F
                    $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $pointName)
                    $mark = $chart.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
                    if (!$mark) { throw 'The last data point is missing.' }
                    $markBounds = $mark.Current.BoundingRectangle
                    if ($mark.Current.IsOffscreen -or $markBounds.Width -le 0 -or $markBounds.Height -le 0 -or !$viewBounds.Contains($markBounds)) {
                        throw "The last data point is not visible inside the viewport: $markBounds / $viewBounds"
                    }
                    $rect = New-Object ChartVisibilityNative+RECT
                    [ChartVisibilityNative]::GetWindowRect($handle, [ref]$rect) | Out-Null
                    if ($rect.Left -lt -10000) { throw 'Preview is minimized; cannot verify rendered pixels.' }
                    $bitmap = [Drawing.Bitmap]::new($rect.Right-$rect.Left, $rect.Bottom-$rect.Top)
                    $graphics = [Drawing.Graphics]::FromImage($bitmap)
                    try {
                        $dc = $graphics.GetHdc()
                        try { $printed = [ChartVisibilityNative]::PrintWindow($handle, $dc, 2) }
                        finally { $graphics.ReleaseHdc($dc) }
                        if (!$printed) { throw 'Could not capture the app window.' }
                        $bluePixels = 0
                        $x0 = [Math]::Max(0, [int][Math]::Floor($markBounds.Left-$rect.Left))
                        $x1 = [Math]::Min($bitmap.Width-1, [int][Math]::Ceiling($markBounds.Right-$rect.Left))
                        $y0 = [Math]::Max(0, [int][Math]::Floor($markBounds.Top-$rect.Top))
                        $y1 = [Math]::Min($bitmap.Height-1, [int][Math]::Ceiling($markBounds.Bottom-$rect.Top))
                        for ($x=$x0; $x -le $x1; $x++) {
                            for ($y=$y0; $y -le $y1; $y++) {
                                $pixel = $bitmap.GetPixel($x,$y)
                                if ($pixel.B -gt $pixel.R+35 -and $pixel.B -gt $pixel.G+15 -and $pixel.G -gt 75) { $bluePixels++ }
                            }
                        }
                        $imagePath = "$output\$period-$kind-$style-$size.png"
                        $bitmap.Save($imagePath, [Drawing.Imaging.ImageFormat]::Png)
                        if ($bluePixels -lt 4) { throw "No rendered blue data mark in $imagePath (pixels=$bluePixels)." }
                        $results.Add([pscustomobject]@{ Period=$period; Kind=$kind; Style=$style; WindowWidth=$size; LastLabel=$lastLabel; HorizontallyScrollable=$false; PointBounds=$markBounds.ToString(); ViewportBounds=$viewBounds.ToString(); BluePixels=$bluePixels; Screenshot=$imagePath; Status='PASS' })
                        "PASS $period / $kind / $style / $size : last point visible, $bluePixels rendered blue pixels"
                    } finally { $graphics.Dispose(); $bitmap.Dispose() }
                }
            }
        }
        $preview.CloseMainWindow() | Out-Null
        if (!$preview.WaitForExit(5000)) { throw 'Preview did not close gracefully.' }
        if ($preview.ExitCode -ne 0) { throw "Preview exit code: $($preview.ExitCode)" }
        $preview = $null
    }
    "PASS $($results.Count) chart visibility combinations"
} finally {
    $results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath "$output\results.json" -Encoding UTF8
    if ($preview -and !$preview.HasExited) { $preview.CloseMainWindow() | Out-Null }
}
