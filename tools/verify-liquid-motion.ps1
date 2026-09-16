param(
    [Parameter(Mandatory=$true)][string]$ExePath,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath("$PSScriptRoot\..").TrimEnd('\')
if (!$OutputDirectory) { $OutputDirectory = "$projectRoot\artifacts\qa\liquid-motion-evidence" }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (!$output.StartsWith("$projectRoot\artifacts\qa\", [StringComparison]::OrdinalIgnoreCase)) { throw "Motion verification only uses isolated artifacts/qa data. Root=$projectRoot Output=$output" }
if (Test-Path -LiteralPath $output) { throw 'Choose a new evidence directory; prior evidence is retained.' }
New-Item -ItemType Directory -Path $output | Out-Null
$fixture = "$output\motion.db"
$checks = "$projectRoot\tests\PrivateTimeTrace.Checks\bin\Release\net10.0\PrivateTimeTrace.Checks.dll"
& dotnet $checks --seed $fixture
if ($LASTEXITCODE) { throw 'Could not seed isolated fixture.' }
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,System.Drawing,System.Windows.Forms
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Runtime.InteropServices;
public static class MotionCapture {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT point);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr dc, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
    public static Bitmap Frame(IntPtr hwnd, Rectangle crop) {
        RECT rect;
        if (!GetWindowRect(hwnd, out rect)) throw new InvalidOperationException("No window rectangle");
        using (var image = new Bitmap(rect.Right - rect.Left, rect.Bottom - rect.Top)) {
            using (var graphics = Graphics.FromImage(image)) {
                var dc = graphics.GetHdc();
                try { if (!PrintWindow(hwnd, dc, 2)) throw new InvalidOperationException("PrintWindow failed"); }
                finally { graphics.ReleaseHdc(dc); }
            }
            if (crop.IsEmpty) crop = new Rectangle(0, 0, image.Width, image.Height);
            else crop.Offset(-rect.Left, -rect.Top);
            return image.Clone(crop, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        }
    }
    public static double Difference(Bitmap a, Bitmap b) {
        if (a.Size != b.Size) throw new InvalidOperationException("Frame dimensions changed");
        double total = 0;
        for (int y = 0; y < a.Height; y += 2) for (int x = 0; x < a.Width; x += 2) {
            var p = a.GetPixel(x,y); var q = b.GetPixel(x,y);
            total += Math.Abs(p.R-q.R) + Math.Abs(p.G-q.G) + Math.Abs(p.B-q.B);
        }
        return total / (Math.Ceiling(a.Width/2d) * Math.Ceiling(a.Height/2d) * 3 * 255);
    }
}
'@
[MotionCapture]::SetThreadDpiAwarenessContext([IntPtr](-4)) | Out-Null
$cursor = New-Object MotionCapture+POINT
[MotionCapture]::GetCursorPos([ref]$cursor) | Out-Null
$preview = $null
$frames = New-Object 'Collections.Generic.List[System.Drawing.Bitmap]'
$timings = New-Object 'Collections.Generic.List[long]'
$result = [ordered]@{ ExePath=[IO.Path]::GetFullPath($ExePath); Fixture=$fixture; CapturedAt=(Get-Date).ToString('o') }
function Find([string]$id) {
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    $element = $script:root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    if (!$element) { throw "Control not found: $id" }
    return $element
}
function Select-Radio([string]$id) { (Find $id).GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() }
function Assert-Selected([string]$id) {
    if (!(Find $id).GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected) { throw "Wrong selected state: $id" }
}
function Get-Crop([string]$first, [string]$last) {
    $a = (Find $first).Current.BoundingRectangle
    $b = (Find $last).Current.BoundingRectangle
    $left = [Math]::Floor([Math]::Min($a.Left,$b.Left))-9
    $top = [Math]::Floor([Math]::Min($a.Top,$b.Top))-9
    $right = [Math]::Ceiling([Math]::Max($a.Right,$b.Right))+9
    $bottom = [Math]::Ceiling([Math]::Max($a.Bottom,$b.Bottom))+9
    return New-Object Drawing.Rectangle($left,$top,($right-$left),($bottom-$top))
}
function Capture-Transition([string]$first, [string]$last, [string]$prefix, [switch]$Physical) {
    Select-Radio $first
    Start-Sleep -Milliseconds 850
    $crop = Get-Crop $first $last
    $frames.Clear(); $timings.Clear()
    $frames.Add([MotionCapture]::Frame($preview.MainWindowHandle,$crop)); $timings.Add(0)
    $clock = [Diagnostics.Stopwatch]::StartNew()
    if ($Physical) {
        $bounds = (Find $last).Current.BoundingRectangle
        [MotionCapture]::SetForegroundWindow($preview.MainWindowHandle) | Out-Null
        [MotionCapture]::SetCursorPos([int]($bounds.X+$bounds.Width/2),[int]($bounds.Y+$bounds.Height/2)) | Out-Null
        $hit = New-Object MotionCapture+POINT
        [MotionCapture]::GetCursorPos([ref]$hit) | Out-Null
        if ([MotionCapture]::GetAncestor([MotionCapture]::WindowFromPoint($hit),2) -ne $preview.MainWindowHandle) { throw 'Physical input target is not the isolated test app.' }
        [MotionCapture]::mouse_event(2,0,0,0,[UIntPtr]::Zero)
        Start-Sleep -Milliseconds 65
        [MotionCapture]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
    } else { Select-Radio $last }
    foreach ($at in @(25,70,135,220,350,600,1000)) {
        $delay = $at - $clock.ElapsedMilliseconds
        if ($delay -gt 0) { Start-Sleep -Milliseconds $delay }
        $frames.Add([MotionCapture]::Frame($preview.MainWindowHandle,$crop)); $timings.Add($clock.ElapsedMilliseconds)
    }
    Assert-Selected $last
    $samples = @()
    for ($index=0; $index -lt $frames.Count; $index++) {
        $frames[$index].Save("$output\$prefix-$index.png",[Drawing.Imaging.ImageFormat]::Png)
        $samples += [pscustomobject]@{ Frame=$index; ElapsedMs=$timings[$index]; FromStart=[MotionCapture]::Difference($frames[0],$frames[$index]); FromFinal=[MotionCapture]::Difference($frames[$frames.Count-1],$frames[$index]) }
    }
    # Require multiple actual intermediate images, not only a changed label or a final screenshot.
    $intermediates = @($samples | Where-Object { $_.Frame -gt 0 -and $_.FromStart -gt .003 -and $_.FromFinal -gt .003 })
    if ($intermediates.Count -lt 2) { throw "${prefix}: no multi-frame liquid transition was captured." }
    $strip = New-Object Drawing.Bitmap(($crop.Width*4),($crop.Height*2))
    $graphics = [Drawing.Graphics]::FromImage($strip)
    try {
        for ($index=0; $index -lt $frames.Count; $index++) { $graphics.DrawImageUnscaled($frames[$index],($index%4)*$crop.Width,[int][Math]::Floor($index/4)*$crop.Height) }
        $strip.Save("$output\$prefix-filmstrip.png",[Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $strip.Dispose() }
    foreach ($frame in $frames) { $frame.Dispose() }
    $frames.Clear()
    $result[$prefix] = $samples
    "PASS ${prefix}: $($intermediates.Count) rendered intermediate states"
}
try {
    $preview = Start-Process -FilePath $ExePath -ArgumentList '--data-file', ('"'+$fixture+'"') -PassThru
    Start-Sleep -Milliseconds 2600
    $preview.Refresh()
    if ($preview.HasExited -or !$preview.MainWindowHandle) { throw 'No visible test window.' }
    $script:root = [System.Windows.Automation.AutomationElement]::FromHandle($preview.MainWindowHandle)
    [MotionCapture]::SetForegroundWindow($preview.MainWindowHandle) | Out-Null
    $bounds = (Find 'SessionNote').Current.BoundingRectangle
    [MotionCapture]::SetCursorPos([int]$bounds.Left,[int]($bounds.Top-40)) | Out-Null
    Select-Radio 'StyleLiquid'
    Capture-Transition 'TrendLine' 'TrendBar' 'trend'
    Capture-Transition 'TopicBar' 'TopicLine' 'topic-mouse' -Physical
    Capture-Transition 'StyleFrosted' 'StyleLiquid' 'style'
    # Only the switch gets pointer light. The timer card must remain pixel-stable.
    $timer = (Find 'ElapsedTime').Current.BoundingRectangle
    $total = (Find 'TodayTotal').Current.BoundingRectangle
    $sceneCrop = New-Object Drawing.Rectangle([int]$timer.Left,[int]($timer.Top-30),[int]($total.Right-$timer.Left),[int]($bounds.Bottom-$timer.Top+40))
    $lightCrop = Get-Crop 'TrendLine' 'TrendBar'
    [MotionCapture]::SetForegroundWindow($preview.MainWindowHandle) | Out-Null
    [MotionCapture]::SetCursorPos($lightCrop.Left+18,$lightCrop.Top+15) | Out-Null
    Start-Sleep -Milliseconds 850
    $pointerCheck = New-Object MotionCapture+POINT
    [MotionCapture]::GetCursorPos([ref]$pointerCheck) | Out-Null
    $result.PointerBefore = @($pointerCheck.X,$pointerCheck.Y)
    $result.PointerBeforeInApp = [MotionCapture]::GetAncestor([MotionCapture]::WindowFromPoint($pointerCheck),2) -eq $preview.MainWindowHandle
    $leftLight = [MotionCapture]::Frame($preview.MainWindowHandle,$lightCrop)
    $sceneBefore = [MotionCapture]::Frame($preview.MainWindowHandle,$sceneCrop)
    [MotionCapture]::SetCursorPos($lightCrop.Right-18,$lightCrop.Bottom-15) | Out-Null
    Start-Sleep -Milliseconds 850
    [MotionCapture]::GetCursorPos([ref]$pointerCheck) | Out-Null
    $result.PointerAfter = @($pointerCheck.X,$pointerCheck.Y)
    $result.PointerAfterInApp = [MotionCapture]::GetAncestor([MotionCapture]::WindowFromPoint($pointerCheck),2) -eq $preview.MainWindowHandle
    $result.PointerCrop = $lightCrop.ToString()
    $rightLight = [MotionCapture]::Frame($preview.MainWindowHandle,$lightCrop)
    $sceneAfter = [MotionCapture]::Frame($preview.MainWindowHandle,$sceneCrop)
    try {
        $difference = [MotionCapture]::Difference($leftLight,$rightLight)
        $leftLight.Save("$output\light-left.png",[Drawing.Imaging.ImageFormat]::Png)
        $rightLight.Save("$output\light-right.png",[Drawing.Imaging.ImageFormat]::Png)
        $result.PointerLightDifference = $difference
        if ($difference -lt .005) { throw 'Pointer-driven light did not visibly change.' }
        $sceneDifference = [MotionCapture]::Difference($sceneBefore,$sceneAfter)
        $sceneBefore.Save("$output\static-scene-before.png",[Drawing.Imaging.ImageFormat]::Png)
        $sceneAfter.Save("$output\static-scene-after.png",[Drawing.Imaging.ImageFormat]::Png)
        $result.StaticSceneDifference = $sceneDifference
        if ($sceneDifference -gt .001) { throw 'Pointer light escaped the buttons into the content area.' }
    } finally { $leftLight.Dispose(); $rightLight.Dispose(); $sceneBefore.Dispose(); $sceneAfter.Dispose() }
    'PASS button-local pointer light; content card remains pixel-stable'
    for ($index=0; $index -lt 20; $index++) {
        if ($index%2 -eq 0) { Select-Radio 'TrendLine'; Select-Radio 'TopicBar' }
        else { Select-Radio 'TrendBar'; Select-Radio 'TopicLine' }
        Start-Sleep -Milliseconds 32
    }
    Start-Sleep -Milliseconds 900
    Assert-Selected 'TrendBar'; Assert-Selected 'TopicLine'; Assert-Selected 'StyleLiquid'
    $saved = (& dotnet $checks --inspect $fixture) | ConvertFrom-Json
    if ($saved.Trend -ne 'Bar' -or $saved.Topic -ne 'Line' -or $saved.Style -ne 'Liquid' -or $saved.Count -ne 24 -or $saved.Active) { throw 'Rapid switching altered data or lost independent preferences.' }
    $result.RapidSwitches = 40
    'PASS 40 rapid selections settle correctly and preserve all 24 fixture records'
    Select-Radio 'StyleFrosted'
    (Find 'StyleFrosted').SetFocus()
    Start-Sleep -Milliseconds 250
    $result.KeyboardFocusBefore = [System.Windows.Automation.AutomationElement]::FocusedElement.Current.AutomationId
    if ([MotionCapture]::GetForegroundWindow() -ne $preview.MainWindowHandle -or $result.KeyboardFocusBefore -ne 'StyleFrosted') { throw 'Keyboard input is not focused on the isolated test switch.' }
    [Windows.Forms.SendKeys]::SendWait('{RIGHT}')
    Start-Sleep -Milliseconds 750
    Assert-Selected 'StyleLiquid'
    $result.KeyboardSelection = 'PASS'
    'PASS keyboard selection'
    $shot = [MotionCapture]::Frame($preview.MainWindowHandle,[Drawing.Rectangle]::Empty)
    try { $shot.Save("$output\liquid-window.png",[Drawing.Imaging.ImageFormat]::Png) } finally { $shot.Dispose() }
    Start-Sleep -Milliseconds 1200
    $preview.Refresh()
    $cpuStart = $preview.TotalProcessorTime.TotalMilliseconds
    Start-Sleep -Milliseconds 1000
    $preview.Refresh()
    $result.IdleCpuMillisecondsPerSecond = $preview.TotalProcessorTime.TotalMilliseconds-$cpuStart
    if (!$preview.CloseMainWindow() -or !$preview.WaitForExit(7000) -or $preview.ExitCode -ne 0) { throw "Motion window did not close cleanly: $($preview.ExitCode)" }
    $result.ExitCode = $preview.ExitCode
    $result.Status = 'PASS'
    'PASS animated window closed with exit code 0'
} catch {
    $result.Failure = $_.Exception.Message
    if ($preview -and !$preview.HasExited) {
        $diagnostic = [MotionCapture]::Frame($preview.MainWindowHandle,[Drawing.Rectangle]::Empty)
        try { $diagnostic.Save("$output\failure.png",[Drawing.Imaging.ImageFormat]::Png) } finally { $diagnostic.Dispose() }
        $result.VisibleControls = @($script:root.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition) | Where-Object { !$_.Current.IsOffscreen -and $_.Current.AutomationId } | ForEach-Object { $_.Current.AutomationId })
    }
    throw
} finally {
    foreach ($frame in $frames) { $frame.Dispose() }
    if ($preview -and !$preview.HasExited) { $preview.CloseMainWindow() | Out-Null }
    [MotionCapture]::SetCursorPos($cursor.X,$cursor.Y) | Out-Null
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$output\evidence.json" -Encoding UTF8
}
