using System.Runtime.InteropServices;
using QuickDock.Models;

namespace QuickDock.Services;

public sealed class MouseHotkeyHook : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_MBUTTONUP = 0x0208;
    private const int WM_XBUTTONDOWN = 0x020B;
    private const int WM_XBUTTONUP = 0x020C;
    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;

    private readonly LowLevelMouseProc _callback;
    private IntPtr _hookHandle;
    private DateTime _lastTrigger = DateTime.MinValue;
    private bool _suppressNextUp;
    private bool _disposed;

    public MouseHotkeyHook(HotkeyConfig config)
    {
        Config = config;
        _callback = HookCallback;
    }

    public HotkeyConfig Config { get; set; }

    public event Action? Triggered;

    public void Start()
    {
        if (_hookHandle != IntPtr.Zero) return;
        _hookHandle = SetWindowsHookEx(WH_MOUSE_LL, _callback, GetModuleHandle(null), 0);
        if (_hookHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("安装全局鼠标钩子失败，错误码 " + Marshal.GetLastWin32Error());
        }
    }

    public void UpdateConfig(HotkeyConfig config) => Config = config;

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && !_disposed)
        {
            try
            {
                var msg = wParam.ToInt32();
                if (_suppressNextUp && IsTriggerUp(msg, lParam))
                {
                    _suppressNextUp = false;
                    return new IntPtr(1);
                }

                if (Matches(wParam.ToInt32(), lParam))
                {
                    var now = DateTime.UtcNow;
                    if ((now - _lastTrigger).TotalMilliseconds > 350)
                    {
                        _lastTrigger = now;
                        _suppressNextUp = true;
                        Triggered?.Invoke();
                    }

                    return new IntPtr(1);
                }
            }
            catch
            {
                // 钩子回调中不可抛出异常，否则会拖垮整个鼠标输入队列
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private bool Matches(int message, IntPtr lParam)
    {
        var trigger = Config.Trigger;
        var matched = trigger switch
        {
            MouseTrigger.Middle => message == WM_MBUTTONDOWN,
            MouseTrigger.Right => message == WM_RBUTTONDOWN,
            MouseTrigger.XButton1 => IsXButton(message, lParam, 1),
            MouseTrigger.XButton2 => IsXButton(message, lParam, 2),
            _ => false
        };
        if (!matched) return false;

        var altDown = IsKeyDown(VK_MENU);
        var ctrlDown = IsKeyDown(VK_CONTROL);
        var shiftDown = IsKeyDown(VK_SHIFT);
        var winDown = IsKeyDown(VK_LWIN) || IsKeyDown(VK_RWIN);

        return altDown == Config.Alt
               && ctrlDown == Config.Ctrl
               && shiftDown == Config.Shift
               && winDown == Config.Win;
    }

    private static bool IsXButton(int message, IntPtr lParam, int button)
    {
        if (message != WM_XBUTTONDOWN) return false;
        var info = Marshal.PtrToStructure<MsllHookStruct>(lParam);
        return ((info.mouseData >> 16) & 0xFFFF) == button;
    }

    private bool IsTriggerUp(int message, IntPtr lParam) => Config.Trigger switch
    {
        MouseTrigger.Middle => message == WM_MBUTTONUP,
        MouseTrigger.Right => message == WM_RBUTTONUP,
        MouseTrigger.XButton1 => IsXButton(message, lParam, 1, WM_XBUTTONUP),
        MouseTrigger.XButton2 => IsXButton(message, lParam, 2, WM_XBUTTONUP),
        _ => false
    };

    private static bool IsXButton(int message, IntPtr lParam, int button, int expectedMessage)
    {
        if (message != expectedMessage) return false;
        var info = Marshal.PtrToStructure<MsllHookStruct>(lParam);
        return ((info.mouseData >> 16) & 0xFFFF) == button;
    }

    private static bool IsKeyDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MsllHookStruct
    {
        public Point pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
