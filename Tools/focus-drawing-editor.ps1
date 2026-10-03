param([switch]$Refresh, [switch]$Capture)
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class DrawingEditorWindow {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int command);
}
'@
$editorProcess = Get-Process Unity | Where-Object { $_.MainWindowTitle -like 'MagicTouch*' } | Select-Object -First 1
if (-not $editorProcess) { throw 'MagicTouch Editor window not found' }
[DrawingEditorWindow]::ShowWindow($editorProcess.MainWindowHandle, 5) | Out-Null
[DrawingEditorWindow]::SetForegroundWindow($editorProcess.MainWindowHandle) | Out-Null
if ($Refresh) {
    Start-Sleep -Milliseconds 300
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.SendKeys]::SendWait('^r')
}
Write-Output 'Focused MagicTouch Unity Editor.'
if ($Capture) {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    Start-Sleep -Milliseconds 500
    $rect = New-Object DrawingEditorWindow+Rect
    [DrawingEditorWindow]::GetWindowRect($editorProcess.MainWindowHandle, [ref]$rect) | Out-Null
    $bounds = New-Object System.Drawing.Rectangle $rect.Left,$rect.Top,($rect.Right-$rect.Left),($rect.Bottom-$rect.Top)
    $bitmap = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
    $bitmap.Save((Join-Path (Get-Location) 'Logs/drawing-editor.png'))
    $graphics.Dispose()
    $bitmap.Dispose()
}
