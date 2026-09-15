param(
    [Parameter(Mandatory=$true)][int]$AppProcessId,
    [ValidateSet('Dump','Capture','Click','Text','Resize','Close','AssertSelected','Scroll')][string]$Action = 'Dump',
    [string]$AutomationId,
    [string]$Name,
    [string]$Value,
    [string]$OutputPath,
    [int]$Width = 1280,
    [int]$Height = 900,
    [switch]$LogicalSize
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class TimeTraceNative {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int command);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int w, int h, uint flags);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
}
'@
[TimeTraceNative]::SetThreadDpiAwarenessContext([IntPtr](-4)) | Out-Null
$appProcess = Get-Process -Id $AppProcessId
$windowHandle = $appProcess.MainWindowHandle
if ($windowHandle -eq [IntPtr]::Zero) { throw 'Application has no visible main window.' }
if ($Action -eq 'Close') { $appProcess.CloseMainWindow(); return }
# Native UI Automation and PrintWindow do not need to steal foreground focus.
# Only coordinate-based fallbacks below activate the test window.
Start-Sleep -Milliseconds 350
$rootElement = [System.Windows.Automation.AutomationElement]::FromHandle($windowHandle)
function Find-Control {
    if ($AutomationId) {
        $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId)
    } elseif ($Name) {
        $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    } else { throw 'Supply AutomationId or Name.' }
    $found = $rootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    if ($null -eq $found) { throw "Control not found: $AutomationId $Name" }
    return $found
}
switch ($Action) {
    'Dump' {
        $rootElement.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition) | ForEach-Object {
            $element = $_.Current
            [pscustomobject]@{ Type=$element.ControlType.ProgrammaticName; Id=$element.AutomationId; Name=$element.Name; Offscreen=$element.IsOffscreen; Enabled=$element.IsEnabled; Bounds=$element.BoundingRectangle.ToString() }
        } | ConvertTo-Json -Depth 2
    }
    'Click' {
        $control = Find-Control
        if (!$control.Current.IsEnabled -or $control.Current.IsOffscreen) { throw 'Control is disabled or offscreen.' }
        if ($AutomationId -match '^(Style|Trend(Line|Bar)|Topic(Line|Bar))') {
            $control.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
            Start-Sleep -Milliseconds 450
            "Selected through native accessibility: $AutomationId"
            break
        }
        $invoke = $null
        if ($control.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invoke)) {
            $invoke.Invoke()
            Start-Sleep -Milliseconds 450
            "Invoked native control: $AutomationId $Name"
            break
        }
        $bounds = $control.Current.BoundingRectangle
        [TimeTraceNative]::ShowWindow($windowHandle, 9) | Out-Null
        [TimeTraceNative]::SetForegroundWindow($windowHandle) | Out-Null
        Start-Sleep -Milliseconds 200
        [TimeTraceNative]::SetCursorPos([int]($bounds.X + $bounds.Width/2), [int]($bounds.Y + $bounds.Height/2)) | Out-Null
        [TimeTraceNative]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
        [TimeTraceNative]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 450
        "Clicked: $AutomationId $Name"
    }
    'Text' {
        $control = Find-Control
        $pattern = $control.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
        $pattern.SetValue($Value)
        "Text entered: $AutomationId"
    }
    'AssertSelected' {
        $control = Find-Control
        $pattern = $control.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
        if (!$pattern.Current.IsSelected) { throw "Not selected: $AutomationId $Name" }
        "PASS selected: $AutomationId $Name"
    }
    'Resize' {
        if ($LogicalSize) {
            $scale = [TimeTraceNative]::GetDpiForWindow($windowHandle) / 96.0
            $Width = [int]($Width * $scale)
            $Height = [int]($Height * $scale)
        }
        [TimeTraceNative]::SetWindowPos($windowHandle, [IntPtr]::Zero, 24, 24, $Width, $Height, 4) | Out-Null
        Start-Sleep -Milliseconds 500
        "Resized: $Width x $Height"
    }
    'Scroll' {
        $control = Find-Control
        $pattern = $control.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
        if (!$pattern.Current.VerticallyScrollable) { throw 'This viewport is not vertically scrollable; choose a smaller logical window before testing scrolling.' }
        $pattern.SetScrollPercent(-1, [double]$Value)
        Start-Sleep -Milliseconds 500
        "Scrolled: $AutomationId to $Value percent"
    }
    'Capture' {
        if (!$OutputPath) { throw 'Supply OutputPath.' }
        $rect = New-Object TimeTraceNative+RECT
        [TimeTraceNative]::GetWindowRect($windowHandle, [ref]$rect) | Out-Null
        # Capture only this app's visible rectangle, never the surrounding desktop.
        $image = New-Object System.Drawing.Bitmap(($rect.Right-$rect.Left), ($rect.Bottom-$rect.Top))
        $graphics = [System.Drawing.Graphics]::FromImage($image)
        try {
            $deviceContext = $graphics.GetHdc()
            try { $printed = [TimeTraceNative]::PrintWindow($windowHandle, $deviceContext, 2) }
            finally { $graphics.ReleaseHdc($deviceContext) }
            if (!$printed) { $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $image.Size) }
            $parent = Split-Path -Parent $OutputPath
            if (!(Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent | Out-Null }
            $image.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
            "Captured app window: $OutputPath"
        } finally { $graphics.Dispose(); $image.Dispose() }
    }
}
