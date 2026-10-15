using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinDock.Services;

public static class WinKeys
{
    const int KeyboardLowLevel = 13, KeyDown = 0x100, SysKeyDown = 0x104;
    const int LeftWin = 0x5B, RightWin = 0x5C, Shift = 0x10, Control = 0x11, Alt = 0x12;
    const ushort Mask = 0xE8;

    delegate nint HookProc(int code, nint wParam, nint lParam);

    static readonly HookProc Proc = Callback;
    static nint hook;

    public static Func<int, bool, bool>? Pressed { get; set; }

    public static void Enable(bool on)
    {
        if (on && hook == 0)
        {
            using var module = Process.GetCurrentProcess().MainModule;
            hook = SetWindowsHookEx(KeyboardLowLevel, Proc, GetModuleHandle(module?.ModuleName), 0);
        }
        else if (!on && hook != 0)
        {
            UnhookWindowsHookEx(hook);
            hook = 0;
        }
    }

    static nint Callback(int code, nint wParam, nint lParam)
    {
        if (code >= 0 && (wParam == KeyDown || wParam == SysKeyDown) && Pressed is { } pressed)
        {
            var key = Marshal.ReadInt32(lParam);
            if (key is >= 0x31 and <= 0x39 && (Down(LeftWin) || Down(RightWin)) && !Down(Control) && !Down(Alt)
                && pressed(key - 0x30, Down(Shift)))
            {
                SendMask();
                return 1;
            }
        }
        return CallNextHookEx(hook, code, wParam, lParam);
    }

    static bool Down(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;

    static void SendMask()
    {
        var inputs = new Input[2];
        inputs[0] = new Input { Type = 1, Key = new KeyInput { Vk = Mask } };
        inputs[1] = new Input { Type = 1, Key = new KeyInput { Vk = Mask, Flags = 2 } };
        SendInput(2, inputs, Marshal.SizeOf<Input>());
    }

    [StructLayout(LayoutKind.Sequential)]
    struct KeyInput
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public nint Extra;
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    struct Input
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public KeyInput Key;
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern nint SetWindowsHookEx(int id, HookProc proc, nint module, uint thread);

    [DllImport("user32.dll")]
    static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int key);

    [DllImport("user32.dll")]
    static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern nint GetModuleHandle(string? name);
}
