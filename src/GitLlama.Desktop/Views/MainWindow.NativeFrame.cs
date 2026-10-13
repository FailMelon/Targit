using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace GitLlama.Desktop.Views;

public partial class MainWindow
{
    // Windows draws this outside the client surface, so window captures have no shadow padding.
    private void UpdateNativeShadow()
    {
        if (!OperatingSystem.IsWindows() || TryGetPlatformHandle() is not { HandleDescriptor: "HWND" } handle) return;
        var policy = 2; // DWMNCRP_ENABLED
        var margins = new FrameMargins { Left = WindowState == WindowState.Normal ? 1 : 0 };
        DwmSetWindowAttribute(handle.Handle, 2, ref policy, sizeof(int));
        DwmExtendFrameIntoClientArea(handle.Handle, ref margins);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FrameMargins { public int Left, Right, Top, Bottom; }
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(nint window, ref FrameMargins margins);
}
